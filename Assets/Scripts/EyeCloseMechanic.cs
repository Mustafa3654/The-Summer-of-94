using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Hold the right mouse button to squeeze both eyes shut.
///
/// The screen fades to black, the player slows to a shuffle, sanity stops draining, and a
/// heartbeat takes over the mix. It is the way to ride out a close encounter: you cannot see
/// the threat, so you track it by sound alone.
/// </summary>
public sealed class EyeCloseMechanic : MonoBehaviour
{
    private const string ShakeSourceId = "Heartbeat";

    [Header("References")]
    [SerializeField] private FirstPersonController playerController;
    [SerializeField] private SanitySystem sanitySystem;
    [SerializeField] private ScreenOverlayController screenOverlay;
    [SerializeField] private CameraShake cameraShake;

    [Header("Eyelids")]
    [SerializeField] private float closeDuration = 0.45f;
    [SerializeField] private float openDuration = 0.3f;
    [SerializeField, Range(0f, 1f)] private float fullyClosedThreshold = 0.9f;
    [SerializeField, Range(0f, 1f)] private float maximumDarkness = 1f;

    [Header("Movement")]
    [SerializeField, Range(0f, 1f)] private float closedMoveSpeedMultiplier = 0.35f;

    [Header("Heartbeat")]
    [SerializeField] private AudioSource heartbeatAudioSource;
    [SerializeField] private AudioClip heartbeatClip;
    [SerializeField, Range(0f, 1f)] private float maximumHeartbeatVolume = 0.75f;
    [SerializeField] private float calmBeatsPerMinute = 62f;
    [SerializeField] private float panicBeatsPerMinute = 132f;
    [SerializeField] private float threatRadius = 12f;

    [Header("Heartbeat Camera Pulse")]
    [SerializeField] private float pulseRotationAmplitude = 0.18f;

    private float closedAmount;
    private bool eyesClosed;
    private float nextHeartbeatCaptionTime;

    /// <summary>0 when the eyes are open, 1 when fully shut.</summary>
    public float ClosedAmount => closedAmount;

    public bool EyesFullyClosed => closedAmount >= fullyClosedThreshold;

    private void Awake()
    {
        if (playerController == null)
        {
            playerController = GetComponent<FirstPersonController>();
        }

        if (sanitySystem == null)
        {
            sanitySystem = GetComponent<SanitySystem>();
        }

        if (screenOverlay == null)
        {
            screenOverlay = ScreenOverlayController.Find();
        }

        if (cameraShake == null)
        {
            cameraShake = CameraShake.FindOrCreate(transform);
        }

        if (heartbeatAudioSource == null)
        {
            heartbeatAudioSource = gameObject.AddComponent<AudioSource>();
        }

        if (heartbeatClip == null)
        {
            heartbeatClip = ProceduralAudio.CreateHeartbeat();
        }

        // The clip is exactly one beat long, so pitch sets the tempo directly.
        heartbeatAudioSource.clip = heartbeatClip;
        heartbeatAudioSource.loop = true;
        heartbeatAudioSource.playOnAwake = false;
        heartbeatAudioSource.spatialBlend = 0f;
        heartbeatAudioSource.volume = 0f;
        heartbeatAudioSource.Play();
    }

    private void Update()
    {
        bool holding = Mouse.current != null && Mouse.current.rightButton.isPressed;

        float duration = holding ? closeDuration : openDuration;
        float direction = holding ? 1f : -1f;
        closedAmount = Mathf.Clamp01(closedAmount + direction * Time.deltaTime / Mathf.Max(0.01f, duration));

        ApplyEyelids();
        ApplyMovementPenalty();
        ApplyHeartbeat();
        UpdateSanityState();
    }

    private void ApplyEyelids()
    {
        if (screenOverlay != null)
        {
            screenOverlay.SetBlackout(closedAmount * maximumDarkness);
        }
    }

    private void ApplyMovementPenalty()
    {
        if (playerController == null)
        {
            return;
        }

        playerController.SetMoveSpeedMultiplier(Mathf.Lerp(1f, closedMoveSpeedMultiplier, closedAmount));
    }

    private void UpdateSanityState()
    {
        bool shouldBeClosed = EyesFullyClosed;
        if (shouldBeClosed == eyesClosed)
        {
            return;
        }

        eyesClosed = shouldBeClosed;
        if (sanitySystem != null)
        {
            sanitySystem.SetEyesClosed(eyesClosed);
        }

        SubtitleManager.Caption(
            eyesClosed ? "[You squeeze your eyes shut]" : "[You open your eyes]",
            2f,
            SubtitleManager.Priority.Interaction);
    }

    private void ApplyHeartbeat()
    {
        float threat = GetThreatProximity();
        float stress = sanitySystem != null ? sanitySystem.Stress : 0f;
        float intensity = Mathf.Clamp01(Mathf.Max(threat, stress * 0.75f));

        if (heartbeatAudioSource != null)
        {
            // Audible mainly while the eyes are shut, with a faint pulse when a spirit is close.
            float volumeBlend = Mathf.Max(closedAmount, threat * 0.45f);
            heartbeatAudioSource.volume = maximumHeartbeatVolume * volumeBlend;

            float beatsPerMinute = Mathf.Lerp(calmBeatsPerMinute, panicBeatsPerMinute, intensity);
            heartbeatAudioSource.pitch = beatsPerMinute / 60f;

            // The heartbeat is the only cue while the eyes are shut, so it must be captioned.
            if (volumeBlend > 0.3f && Time.time >= nextHeartbeatCaptionTime)
            {
                nextHeartbeatCaptionTime = Time.time + 2f;
                SubtitleManager.Caption(
                    intensity > 0.5f
                        ? "[Heartbeat thumping rapidly...]"
                        : "[Heartbeat thudding steadily...]",
                    2.6f,
                    SubtitleManager.Priority.Threat);
            }
        }

        if (cameraShake != null)
        {
            float pulse = pulseRotationAmplitude * intensity * Mathf.Max(closedAmount, 0.25f);
            cameraShake.SetSource(ShakeSourceId, 0f, pulse, Mathf.Lerp(1.1f, 2.6f, intensity));
        }
    }

    /// <summary>1 when a manifested spirit is on top of the player, 0 when none is in range.</summary>
    private float GetThreatProximity()
    {
        if (!ChildSpirit.TryGetNearestManifested(transform.position, out float distance, out float presence))
        {
            return 0f;
        }

        if (distance > threatRadius)
        {
            return 0f;
        }

        return (1f - Mathf.Clamp01(distance / Mathf.Max(0.01f, threatRadius))) * presence;
    }

    private void OnDisable()
    {
        if (screenOverlay != null)
        {
            screenOverlay.SetBlackout(0f);
        }

        if (playerController != null)
        {
            playerController.SetMoveSpeedMultiplier(1f);
        }

        if (sanitySystem != null)
        {
            sanitySystem.SetEyesClosed(false);
        }

        if (cameraShake != null)
        {
            cameraShake.ClearSource(ShakeSourceId);
        }
    }
}
