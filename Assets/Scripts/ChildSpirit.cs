using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A child spirit that fades into view in dark parts of the cabin, laughs or whispers, and
/// is banished if the player holds the flashlight beam on it.
///
/// Every instance registers itself in <see cref="Active"/>, which the radio detector and the
/// sanity system read to find the nearest manifested spirit.
/// </summary>
public sealed class ChildSpirit : MonoBehaviour
{
    public enum SpiritState
    {
        Dormant,
        Manifesting,
        Present,
        Fading,
        Banished
    }

    private static readonly List<ChildSpirit> ActiveSpirits = new List<ChildSpirit>();

    public static IReadOnlyList<ChildSpirit> Active => ActiveSpirits;

    /// <summary>
    /// Raised with the world position and carrying distance every time a spirit laughs or
    /// whispers. This is the audio aggro link: the spirits reacting to the player are what
    /// give the player away to the Teacher.
    /// </summary>
    public static event System.Action<Vector3, float> OnLaughter;

    [Header("Appearance")]
    [SerializeField] private Renderer spiritRenderer;
    [SerializeField, Range(0f, 1f)] private float presentAlpha = 0.38f;
    [SerializeField] private float fadeInDuration = 1.4f;
    [SerializeField] private float fadeOutDuration = 1.1f;
    [SerializeField] private Light spiritGlow;
    [SerializeField] private float glowIntensity = 0.35f;

    [Header("Manifestation")]
    [SerializeField] private float triggerRadius = 11f;
    [SerializeField] private float minimumPlayerDistance = 2.4f;
    [SerializeField] private Vector2 presentDuration = new Vector2(4.5f, 8f);
    [SerializeField] private Vector2 dormantDelay = new Vector2(9f, 19f);
    [SerializeField] private bool requireDarkness = true;
    [SerializeField] private bool faceThePlayer = true;

    [Header("Flashlight Banishing")]
    [SerializeField] private float litSecondsToBanish = 1.8f;
    [SerializeField] private float litMemoryDecay = 1.2f;
    [SerializeField] private bool banishPermanently;
    [SerializeField] private float banishedCooldown = 25f;

    [Header("Audio")]
    [SerializeField] private AudioSource spiritAudioSource;
    [SerializeField] private AudioClip laughClip;
    [SerializeField] private AudioClip whisperClip;
    [SerializeField, Range(0f, 1f)] private float voiceVolume = 0.5f;
    [SerializeField] private Vector2 voiceInterval = new Vector2(2.2f, 5f);

    [Header("Fear")]
    [SerializeField] private float fearRadius = 7f;
    [SerializeField] private float sanityDrainPerSecond = 5.5f;

    [Header("Audio Aggro Link")]
    [SerializeField] private bool alertsTheTeacher = true;
    [SerializeField] private float voiceCarryDistance = 26f;

    private SpiritState state = SpiritState.Dormant;
    private Transform playerTransform;
    private Flashlight playerFlashlight;
    private Material spiritMaterial;
    private float stateTimer;
    private float alpha;
    private float litTime;
    private float nextVoiceTime;

    public SpiritState State => state;
    public bool IsManifested => state == SpiritState.Manifesting || state == SpiritState.Present;
    public float FearRadius => fearRadius;
    public float SanityDrainPerSecond => sanityDrainPerSecond;

    /// <summary>How strongly this spirit is showing itself, 0 to 1. Drives detector and fear.</summary>
    public float Presence => Mathf.Clamp01(presentAlpha > 0f ? alpha / presentAlpha : 0f);

    private void Awake()
    {
        if (spiritRenderer == null)
        {
            spiritRenderer = GetComponentInChildren<Renderer>();
        }

        if (spiritRenderer != null)
        {
            // An instanced material, so fading one spirit does not fade them all.
            spiritMaterial = spiritRenderer.material;
        }

        if (spiritAudioSource == null)
        {
            spiritAudioSource = GetComponent<AudioSource>();
        }

        if (spiritAudioSource == null)
        {
            spiritAudioSource = gameObject.AddComponent<AudioSource>();
        }

        spiritAudioSource.playOnAwake = false;
        spiritAudioSource.loop = false;
        spiritAudioSource.spatialBlend = 1f;
        spiritAudioSource.rolloffMode = AudioRolloffMode.Linear;
        spiritAudioSource.minDistance = 2f;
        spiritAudioSource.maxDistance = 26f;

        if (laughClip == null) laughClip = ProceduralAudio.CreateChildLaugh();
        if (whisperClip == null) whisperClip = ProceduralAudio.CreateWhisper();

        SetAlpha(0f);
        stateTimer = RandomInRange(dormantDelay);
    }

    private void OnEnable()
    {
        if (!ActiveSpirits.Contains(this))
        {
            ActiveSpirits.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveSpirits.Remove(this);
    }

    private void OnDestroy()
    {
        if (spiritMaterial != null)
        {
            Destroy(spiritMaterial);
        }
    }

    private void Update()
    {
        ResolvePlayer();
        UpdateFlashlightExposure();

        switch (state)
        {
            case SpiritState.Dormant:
                TickDormant();
                break;
            case SpiritState.Manifesting:
                TickManifesting();
                break;
            case SpiritState.Present:
                TickPresent();
                break;
            case SpiritState.Fading:
                TickFading();
                break;
            case SpiritState.Banished:
                TickBanished();
                break;
        }

        if (faceThePlayer && IsManifested && playerTransform != null)
        {
            Vector3 toPlayer = playerTransform.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(toPlayer),
                    Time.deltaTime * 2.2f);
            }
        }
    }

    private void TickDormant()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer > 0f || playerTransform == null)
        {
            return;
        }

        float distance = Vector3.Distance(playerTransform.position, transform.position);
        if (distance > triggerRadius || distance < minimumPlayerDistance)
        {
            stateTimer = 1f;
            return;
        }

        if (requireDarkness && playerFlashlight != null && playerFlashlight.IsOn && IsInFlashlightBeam())
        {
            // Do not appear straight into the beam; wait until the player looks away.
            stateTimer = 1.5f;
            return;
        }

        state = SpiritState.Manifesting;
        stateTimer = 0f;
        litTime = 0f;
        nextVoiceTime = Time.time + 0.4f;
    }

    private void TickManifesting()
    {
        stateTimer += Time.deltaTime;
        SetAlpha(Mathf.Lerp(0f, presentAlpha, stateTimer / Mathf.Max(0.01f, fadeInDuration)));
        PlayVoiceIfDue();

        if (stateTimer >= fadeInDuration)
        {
            state = SpiritState.Present;
            stateTimer = RandomInRange(presentDuration);
        }
    }

    private void TickPresent()
    {
        SetAlpha(presentAlpha);
        PlayVoiceIfDue();

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            BeginFade();
        }
    }

    private void TickFading()
    {
        stateTimer += Time.deltaTime;
        SetAlpha(Mathf.Lerp(presentAlpha, 0f, stateTimer / Mathf.Max(0.01f, fadeOutDuration)));

        if (stateTimer >= fadeOutDuration)
        {
            SetAlpha(0f);
            state = SpiritState.Dormant;
            stateTimer = RandomInRange(dormantDelay);
        }
    }

    private void TickBanished()
    {
        if (banishPermanently)
        {
            return;
        }

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            state = SpiritState.Dormant;
            stateTimer = RandomInRange(dormantDelay);
            litTime = 0f;
        }
    }

    private void BeginFade()
    {
        state = SpiritState.Fading;
        stateTimer = 0f;
    }

    /// <summary>Accumulates time spent inside the flashlight cone and banishes on overflow.</summary>
    private void UpdateFlashlightExposure()
    {
        if (!IsManifested)
        {
            return;
        }

        if (playerFlashlight != null && playerFlashlight.IsOn && IsInFlashlightBeam())
        {
            litTime += Time.deltaTime;
            if (litTime >= litSecondsToBanish)
            {
                Banish();
            }
        }
        else
        {
            litTime = Mathf.Max(0f, litTime - Time.deltaTime * litMemoryDecay);
        }
    }

    public void Banish()
    {
        PlayClip(whisperClip, voiceVolume * 0.7f);
        SetAlpha(0f);
        state = SpiritState.Banished;
        stateTimer = banishedCooldown;
        litTime = 0f;
    }

    private bool IsInFlashlightBeam()
    {
        Light beam = playerFlashlight != null ? playerFlashlight.Beam : null;
        if (beam == null)
        {
            return false;
        }

        Vector3 target = transform.position + Vector3.up * 0.9f;
        Vector3 toSpirit = target - beam.transform.position;
        float distance = toSpirit.magnitude;

        if (distance > beam.range)
        {
            return false;
        }

        float angle = Vector3.Angle(beam.transform.forward, toSpirit);
        if (angle > beam.spotAngle * 0.5f)
        {
            return false;
        }

        // Walls block the beam, so a spirit through a door is not being lit.
        if (Physics.Raycast(beam.transform.position, toSpirit.normalized, out RaycastHit hit, distance - 0.15f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!hit.transform.IsChildOf(transform))
            {
                return false;
            }
        }

        return true;
    }

    private void PlayVoiceIfDue()
    {
        if (Time.time < nextVoiceTime)
        {
            return;
        }

        nextVoiceTime = Time.time + RandomInRange(voiceInterval);

        bool laughing = Random.value > 0.45f;
        PlayClip(laughing ? laughClip : whisperClip, voiceVolume);

        if (alertsTheTeacher)
        {
            // A laugh carries further than a whisper, and both give the player away.
            float carry = voiceCarryDistance * (laughing ? 1f : 0.55f);
            OnLaughter?.Invoke(transform.position, carry);
            NoiseEvents.Emit(transform.position, carry);
        }
    }

    private void PlayClip(AudioClip clip, float volume)
    {
        if (spiritAudioSource != null && clip != null)
        {
            spiritAudioSource.pitch = Random.Range(0.92f, 1.12f);
            spiritAudioSource.PlayOneShot(clip, volume);
        }
    }

    private void SetAlpha(float newAlpha)
    {
        alpha = Mathf.Clamp01(newAlpha);

        if (spiritMaterial != null)
        {
            Color color = spiritMaterial.HasProperty("_BaseColor")
                ? spiritMaterial.GetColor("_BaseColor")
                : spiritMaterial.color;
            color.a = alpha;

            if (spiritMaterial.HasProperty("_BaseColor")) spiritMaterial.SetColor("_BaseColor", color);
            if (spiritMaterial.HasProperty("_Color")) spiritMaterial.SetColor("_Color", color);
        }

        if (spiritRenderer != null)
        {
            spiritRenderer.enabled = alpha > 0.002f;
        }

        if (spiritGlow != null)
        {
            spiritGlow.intensity = glowIntensity * Presence;
            spiritGlow.enabled = alpha > 0.002f;
        }
    }

    private void ResolvePlayer()
    {
        if (playerTransform == null)
        {
            FirstPersonController controller = FindAnyObjectByType<FirstPersonController>();
            if (controller != null)
            {
                playerTransform = controller.transform;
            }
        }

        if (playerFlashlight == null)
        {
            playerFlashlight = FindAnyObjectByType<Flashlight>();
        }
    }

    private static float RandomInRange(Vector2 range)
    {
        return Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
    }

    /// <summary>
    /// Distance to the closest manifested spirit, and how strongly it is showing itself.
    /// Returns false when nothing is currently manifested.
    /// </summary>
    public static bool TryGetNearestManifested(Vector3 position, out float distance, out float presence)
    {
        distance = float.MaxValue;
        presence = 0f;
        bool found = false;

        for (int i = 0; i < ActiveSpirits.Count; i++)
        {
            ChildSpirit spirit = ActiveSpirits[i];
            if (spirit == null || !spirit.IsManifested)
            {
                continue;
            }

            float candidate = Vector3.Distance(position, spirit.transform.position);
            if (candidate < distance)
            {
                distance = candidate;
                presence = spirit.Presence;
                found = true;
            }
        }

        return found;
    }
}
