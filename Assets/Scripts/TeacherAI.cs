using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The Teacher: the mascot-headed supervisor who hunts the camp.
///
/// Patrol -> InvestigateNoise -> Chase -> Kill. Sight is a cone plus a line-of-sight raycast;
/// hearing comes from <see cref="NoiseEvents"/> and <see cref="ChildSpirit.OnLaughter"/>, which
/// is the aggro link the design calls for: the spirits reacting to the player betray the player.
///
/// Requires a baked NavMesh. <see cref="NavMeshBaker"/> bakes one at runtime, and this component
/// waits until the agent is actually on it before issuing any commands.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public sealed class TeacherAI : MonoBehaviour
{
    public enum TeacherState
    {
        Patrol,
        InvestigateNoise,
        Chase,
        Kill
    }

    [Header("Waypoints")]
    [SerializeField] private Transform[] patrolWaypoints;
    [SerializeField] private float waypointArrivalDistance = 1.2f;
    [SerializeField] private Vector2 waypointPause = new Vector2(1.5f, 4f);

    [Header("Speeds")]
    [SerializeField] private float patrolSpeed = 1.5f;
    [SerializeField] private float investigateSpeed = 2.6f;
    [SerializeField] private float chaseSpeed = 4.1f;
    [SerializeField] private float hardChaseSpeed = 5.2f;

    [Header("Senses")]
    [SerializeField] private float sightRange = 18f;
    [SerializeField] private float sightAngle = 105f;
    [SerializeField] private float closeAwarenessRadius = 3.5f;
    [SerializeField] private Transform eyeTransform;
    [SerializeField] private LayerMask sightBlockingMask = ~0;

    [Header("Hiding")]
    [SerializeField, Range(0f, 1f)] private float eyesClosedSightMultiplier = 0.45f;
    [SerializeField, Range(0f, 1f)] private float eyesClosedHearingMultiplier = 0.6f;

    [Header("Chase")]
    [SerializeField] private float loseSightGraceSeconds = 6f;
    [SerializeField] private float killDistance = 1.7f;
    [SerializeField] private float investigateDuration = 9f;

    [Header("Kill")]
    [SerializeField] private float jumpscareDuration = 0.5f;
    [SerializeField] private Transform playerRespawnPoint;
    [SerializeField] private bool reloadSceneOnKill;
    [SerializeField] private float postKillRecoverySeconds = 2.5f;
    [SerializeField] private float sanityLostOnKill = 45f;

    [Header("Audio")]
    [SerializeField] private AudioSource teacherAudioSource;
    [SerializeField] private AudioClip footstepClip;
    [SerializeField] private AudioClip screamClip;
    [SerializeField] private float footstepInterval = 0.62f;
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.5f;

    private NavMeshAgent agent;
    private TeacherState state = TeacherState.Patrol;
    private Transform playerTransform;
    private FirstPersonController playerController;
    private EyeCloseMechanic playerEyes;
    private SanitySystem playerSanity;
    private ScreenOverlayController screenOverlay;

    private int waypointIndex;
    private float waypointWaitTimer;
    private float investigateTimer;
    private float lostSightTimer;
    private float nextFootstepTime;
    private float recoveryTimer;
    private Vector3 lastKnownPosition;
    private bool hardChase;
    private bool navMeshWarningLogged;

    public TeacherState State => state;
    public bool IsHardChasing => hardChase;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = patrolSpeed;
        agent.angularSpeed = 220f;
        agent.acceleration = 12f;
        agent.stoppingDistance = 0.4f;
        agent.autoBraking = false;

        if (eyeTransform == null)
        {
            eyeTransform = transform;
        }

        if (teacherAudioSource == null)
        {
            teacherAudioSource = GetComponent<AudioSource>();
        }

        if (teacherAudioSource == null)
        {
            teacherAudioSource = gameObject.AddComponent<AudioSource>();
        }

        teacherAudioSource.playOnAwake = false;
        teacherAudioSource.loop = false;
        teacherAudioSource.spatialBlend = 1f;
        teacherAudioSource.rolloffMode = AudioRolloffMode.Linear;
        teacherAudioSource.minDistance = 3f;
        teacherAudioSource.maxDistance = 34f;

        if (footstepClip == null) footstepClip = ProceduralAudio.CreateHeavyFootstep();
        if (screamClip == null) screamClip = ProceduralAudio.CreateScream();
    }

    private void OnEnable()
    {
        NoiseEvents.NoiseMade += OnNoiseHeard;
        ChildSpirit.OnLaughter += OnNoiseHeard;
    }

    private void OnDisable()
    {
        NoiseEvents.NoiseMade -= OnNoiseHeard;
        ChildSpirit.OnLaughter -= OnNoiseHeard;
    }

    private void Start()
    {
        ResolvePlayer();
        screenOverlay = ScreenOverlayController.Find();
    }

    private void Update()
    {
        ResolvePlayer();

        if (!IsAgentReady())
        {
            return;
        }

        if (recoveryTimer > 0f)
        {
            recoveryTimer -= Time.deltaTime;
            return;
        }

        bool canSeePlayer = CanSeePlayer();

        switch (state)
        {
            case TeacherState.Patrol:
                TickPatrol(canSeePlayer);
                break;
            case TeacherState.InvestigateNoise:
                TickInvestigate(canSeePlayer);
                break;
            case TeacherState.Chase:
                TickChase(canSeePlayer);
                break;
            case TeacherState.Kill:
                break;
        }

        PlayFootsteps();
    }

    /// <summary>
    /// A NavMeshAgent with no NavMesh under it throws on every SetDestination call, so nothing
    /// is issued until the bake has landed.
    /// </summary>
    private bool IsAgentReady()
    {
        if (agent.isOnNavMesh)
        {
            return true;
        }

        // Try to drop onto the mesh, in case the bake finished after this agent spawned.
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 6f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            return agent.isOnNavMesh;
        }

        if (!navMeshWarningLogged)
        {
            navMeshWarningLogged = true;
            Debug.LogWarning(
                "TeacherAI is not on a NavMesh. Add a NavMeshSurface (the builder does this) or " +
                "bake one from Window > AI > Navigation, then re-enter Play mode.",
                this);
        }

        return false;
    }

    private void TickPatrol(bool canSeePlayer)
    {
        if (canSeePlayer)
        {
            BeginChase(false);
            return;
        }

        agent.speed = patrolSpeed;

        if (patrolWaypoints == null || patrolWaypoints.Length == 0)
        {
            return;
        }

        if (waypointWaitTimer > 0f)
        {
            waypointWaitTimer -= Time.deltaTime;
            return;
        }

        Transform waypoint = patrolWaypoints[waypointIndex % patrolWaypoints.Length];
        if (waypoint == null)
        {
            waypointIndex++;
            return;
        }

        agent.SetDestination(waypoint.position);

        if (!agent.pathPending && agent.remainingDistance <= waypointArrivalDistance)
        {
            waypointIndex = (waypointIndex + 1) % patrolWaypoints.Length;
            waypointWaitTimer = Random.Range(
                Mathf.Min(waypointPause.x, waypointPause.y),
                Mathf.Max(waypointPause.x, waypointPause.y));
        }
    }

    private void TickInvestigate(bool canSeePlayer)
    {
        if (canSeePlayer)
        {
            BeginChase(false);
            return;
        }

        agent.speed = investigateSpeed;
        agent.SetDestination(lastKnownPosition);

        investigateTimer -= Time.deltaTime;
        bool arrived = !agent.pathPending && agent.remainingDistance <= waypointArrivalDistance;

        if (investigateTimer <= 0f || arrived)
        {
            state = TeacherState.Patrol;
            waypointWaitTimer = 0.5f;
        }
    }

    private void TickChase(bool canSeePlayer)
    {
        agent.speed = hardChase ? hardChaseSpeed : chaseSpeed;

        if (canSeePlayer)
        {
            lostSightTimer = loseSightGraceSeconds;
            lastKnownPosition = playerTransform.position;
        }
        else
        {
            lostSightTimer -= Time.deltaTime;
            if (lostSightTimer <= 0f)
            {
                hardChase = false;
                state = TeacherState.InvestigateNoise;
                investigateTimer = investigateDuration;
                return;
            }
        }

        agent.SetDestination(lastKnownPosition);

        if (playerTransform != null &&
            Vector3.Distance(transform.position, playerTransform.position) <= killDistance)
        {
            Kill();
        }
    }

    private void BeginChase(bool hard)
    {
        state = TeacherState.Chase;
        hardChase = hardChase || hard;
        lostSightTimer = loseSightGraceSeconds;

        if (playerTransform != null)
        {
            lastKnownPosition = playerTransform.position;
        }
    }

    /// <summary>
    /// Sight test: range, cone, then a raycast. Holding the eyes shut shrinks the range, which
    /// is what makes hiding behind cover with right-click work.
    /// </summary>
    private bool CanSeePlayer()
    {
        if (playerTransform == null)
        {
            return false;
        }

        float range = sightRange;
        if (playerEyes != null && playerEyes.EyesFullyClosed)
        {
            range *= eyesClosedSightMultiplier;
        }

        Vector3 eyePosition = eyeTransform.position;
        Vector3 target = playerTransform.position + Vector3.up * 1.4f;
        Vector3 toPlayer = target - eyePosition;
        float distance = toPlayer.magnitude;

        if (distance > range)
        {
            return false;
        }

        // Right on top of the player, the Teacher notices regardless of facing.
        bool withinCone = Vector3.Angle(transform.forward, toPlayer) <= sightAngle * 0.5f;
        if (!withinCone && distance > closeAwarenessRadius)
        {
            return false;
        }

        if (Physics.Raycast(eyePosition, toPlayer.normalized, out RaycastHit hit, distance, sightBlockingMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform != playerTransform && !hit.transform.IsChildOf(playerTransform))
            {
                return false;
            }
        }

        return true;
    }

    private void OnNoiseHeard(Vector3 position, float loudness)
    {
        if (state == TeacherState.Chase || state == TeacherState.Kill || recoveryTimer > 0f)
        {
            return;
        }

        float effectiveLoudness = loudness;
        if (playerEyes != null && playerEyes.EyesFullyClosed)
        {
            effectiveLoudness *= eyesClosedHearingMultiplier;
        }

        if (Vector3.Distance(transform.position, position) > effectiveLoudness)
        {
            return;
        }

        lastKnownPosition = position;
        investigateTimer = investigateDuration;
        state = TeacherState.InvestigateNoise;
    }

    /// <summary>
    /// Called by <see cref="PowerGenerator"/>: the Teacher knows exactly where the noise came
    /// from and closes in at full speed.
    /// </summary>
    public void AlertHardChase(Vector3 position)
    {
        lastKnownPosition = position;
        hardChase = true;
        investigateTimer = investigateDuration;
        lostSightTimer = loseSightGraceSeconds;
        state = TeacherState.Chase;
    }

    private void Kill()
    {
        if (state == TeacherState.Kill)
        {
            return;
        }

        state = TeacherState.Kill;

        if (screenOverlay != null)
        {
            screenOverlay.ShowJumpscare(jumpscareDuration);
        }

        if (teacherAudioSource != null && screamClip != null)
        {
            teacherAudioSource.PlayOneShot(screamClip, 1f);
        }

        if (reloadSceneOnKill)
        {
            UnityEngine.SceneManagement.Scene active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(active.buildIndex >= 0 ? active.buildIndex : 0);
            return;
        }

        RespawnPlayer();
    }

    /// <summary>Drops the player back at the respawn point and sends the Teacher back to patrol.</summary>
    private void RespawnPlayer()
    {
        if (playerTransform != null && playerRespawnPoint != null)
        {
            CharacterController characterController = playerTransform.GetComponent<CharacterController>();
            bool wasEnabled = characterController != null && characterController.enabled;
            if (wasEnabled)
            {
                characterController.enabled = false;
            }

            playerTransform.SetPositionAndRotation(playerRespawnPoint.position, playerRespawnPoint.rotation);

            if (wasEnabled)
            {
                characterController.enabled = true;
            }

            if (playerController != null)
            {
                playerController.ResetVerticalVelocity();
            }
        }

        if (playerSanity != null)
        {
            playerSanity.ModifySanity(-sanityLostOnKill);
        }

        // Retreat to the far end of the patrol route so the player is not killed on landing.
        hardChase = false;
        state = TeacherState.Patrol;
        recoveryTimer = postKillRecoverySeconds;
        waypointWaitTimer = 0f;

        if (patrolWaypoints != null && patrolWaypoints.Length > 0)
        {
            int farthestIndex = 0;
            float farthestDistance = -1f;
            for (int i = 0; i < patrolWaypoints.Length; i++)
            {
                if (patrolWaypoints[i] == null || playerTransform == null)
                {
                    continue;
                }

                float candidate = Vector3.Distance(patrolWaypoints[i].position, playerTransform.position);
                if (candidate > farthestDistance)
                {
                    farthestDistance = candidate;
                    farthestIndex = i;
                }
            }

            waypointIndex = farthestIndex;
            if (agent.isOnNavMesh && patrolWaypoints[farthestIndex] != null)
            {
                agent.Warp(patrolWaypoints[farthestIndex].position);
            }
        }
    }

    private void PlayFootsteps()
    {
        if (agent.velocity.sqrMagnitude < 0.35f || Time.time < nextFootstepTime)
        {
            return;
        }

        float speedScale = Mathf.Clamp(agent.velocity.magnitude / Mathf.Max(0.01f, patrolSpeed), 0.6f, 3f);
        nextFootstepTime = Time.time + footstepInterval / speedScale;

        if (teacherAudioSource != null && footstepClip != null)
        {
            teacherAudioSource.pitch = Random.Range(0.88f, 1.06f);
            teacherAudioSource.PlayOneShot(footstepClip, footstepVolume);
        }
    }

    private void ResolvePlayer()
    {
        if (playerController == null)
        {
            playerController = FindAnyObjectByType<FirstPersonController>();
            if (playerController != null)
            {
                playerTransform = playerController.transform;
            }
        }

        if (playerEyes == null)
        {
            playerEyes = FindAnyObjectByType<EyeCloseMechanic>();
        }

        if (playerSanity == null)
        {
            playerSanity = FindAnyObjectByType<SanitySystem>();
        }
    }
}
