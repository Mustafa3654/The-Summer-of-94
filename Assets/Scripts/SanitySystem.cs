using UnityEngine;

/// <summary>
/// Tracks player sanity from 100 (composed) down to 0 (breaking down).
///
/// Sanity falls while the player stands in near-total darkness and while a child spirit is
/// manifested nearby, and recovers in lit areas. Holding the eyes shut
/// (<see cref="EyeCloseMechanic"/>) stabilises it. Low sanity feeds the camera wobble,
/// breathing audio, and the screen vignette.
/// </summary>
public sealed class SanitySystem : MonoBehaviour
{
    private const string ShakeSourceId = "LowSanity";

    [Header("Sanity")]
    [SerializeField] private float maximumSanity = 100f;
    [SerializeField] private float startingSanity = 100f;
    [SerializeField] private float darknessDrainPerSecond = 2.4f;
    [SerializeField] private float lightRecoveryPerSecond = 4.5f;
    [SerializeField] private float eyesClosedRecoveryPerSecond = 1.6f;

    [Header("Darkness Test")]
    [SerializeField] private Transform headTransform;
    [SerializeField] private float darknessThreshold = 0.12f;
    [SerializeField] private float lightSampleInterval = 0.35f;

    [Header("Effects")]
    [SerializeField] private CameraShake cameraShake;
    [SerializeField] private ScreenOverlayController screenOverlay;
    [SerializeField] private float maximumWobblePosition = 0.022f;
    [SerializeField] private float maximumWobbleRotation = 0.55f;
    [SerializeField] private float wobbleFrequency = 1.6f;
    [SerializeField] private float maximumVignette = 0.85f;
    [SerializeField] private float maximumHaze = 0.6f;
    [SerializeField] private float effectSmoothing = 2.5f;

    [Header("Breathing Audio")]
    [SerializeField] private AudioSource breathingAudioSource;
    [SerializeField] private AudioClip breathingClip;
    [SerializeField, Range(0f, 1f)] private float maximumBreathingVolume = 0.55f;
    [SerializeField] private float breathingStartsBelow = 65f;

    private float sanity;
    private float smoothedStress;
    private bool eyesClosed;
    private Light[] cachedLights = System.Array.Empty<Light>();
    private float nextLightSampleTime;
    private float illumination;

    /// <summary>Current sanity, 0 to <see cref="maximumSanity"/>.</summary>
    public float Sanity => sanity;

    /// <summary>Sanity as 0 (broken) to 1 (composed).</summary>
    public float SanityNormalized => maximumSanity > 0f ? Mathf.Clamp01(sanity / maximumSanity) : 0f;

    /// <summary>Inverse of <see cref="SanityNormalized"/>, the value that drives the effects.</summary>
    public float Stress => 1f - SanityNormalized;

    public bool IsInDarkness => illumination < darknessThreshold;
    public float Illumination => illumination;

    public event System.Action<float> SanityChanged;

    private void Awake()
    {
        sanity = Mathf.Clamp(startingSanity, 0f, maximumSanity);

        if (headTransform == null)
        {
            Camera childCamera = GetComponentInChildren<Camera>();
            headTransform = childCamera != null ? childCamera.transform : transform;
        }

        if (cameraShake == null)
        {
            cameraShake = CameraShake.FindOrCreate(transform);
        }

        if (screenOverlay == null)
        {
            screenOverlay = ScreenOverlayController.Find();
        }

        if (breathingAudioSource == null)
        {
            breathingAudioSource = gameObject.AddComponent<AudioSource>();
        }

        if (breathingClip == null)
        {
            breathingClip = ProceduralAudio.CreateBreathing();
        }

        breathingAudioSource.clip = breathingClip;
        breathingAudioSource.loop = true;
        breathingAudioSource.playOnAwake = false;
        breathingAudioSource.spatialBlend = 0f;
        breathingAudioSource.volume = 0f;
        breathingAudioSource.Play();
    }

    private void Update()
    {
        SampleIllumination();
        UpdateSanity();
        UpdateEffects();
    }

    private void UpdateSanity()
    {
        float previousSanity = sanity;
        float delta = 0f;

        if (eyesClosed)
        {
            // Eyes shut: the darkness and the spirits stop registering.
            delta += eyesClosedRecoveryPerSecond;
        }
        else
        {
            float spiritDrain = GetSpiritDrainPerSecond();
            if (spiritDrain > 0f)
            {
                delta -= spiritDrain;
            }

            if (IsInDarkness)
            {
                delta -= darknessDrainPerSecond;
            }
            else if (spiritDrain <= 0f)
            {
                delta += lightRecoveryPerSecond;
            }
        }

        sanity = Mathf.Clamp(sanity + delta * Time.deltaTime, 0f, maximumSanity);

        if (!Mathf.Approximately(previousSanity, sanity))
        {
            SanityChanged?.Invoke(sanity);
        }
    }

    /// <summary>Drain contributed by the nearest manifested spirit, scaled by distance.</summary>
    private float GetSpiritDrainPerSecond()
    {
        float total = 0f;

        for (int i = 0; i < ChildSpirit.Active.Count; i++)
        {
            ChildSpirit spirit = ChildSpirit.Active[i];
            if (spirit == null || !spirit.IsManifested)
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, spirit.transform.position);
            if (distance > spirit.FearRadius)
            {
                continue;
            }

            float proximity = 1f - Mathf.Clamp01(distance / Mathf.Max(0.01f, spirit.FearRadius));
            total += spirit.SanityDrainPerSecond * proximity * spirit.Presence;
        }

        return total;
    }

    private void UpdateEffects()
    {
        smoothedStress = Mathf.MoveTowards(smoothedStress, Stress, Time.deltaTime * effectSmoothing);

        if (cameraShake != null)
        {
            float curve = smoothedStress * smoothedStress; // Stays subtle until sanity is genuinely low.
            cameraShake.SetSource(
                ShakeSourceId,
                maximumWobblePosition * curve,
                maximumWobbleRotation * curve,
                wobbleFrequency);
        }

        if (screenOverlay != null)
        {
            screenOverlay.SetVignette(maximumVignette * smoothedStress);
            screenOverlay.SetHaze(maximumHaze * Mathf.Max(0f, smoothedStress - 0.35f) / 0.65f);
        }

        if (breathingAudioSource != null)
        {
            float breathingBlend = Mathf.InverseLerp(breathingStartsBelow, 0f, sanity);
            breathingAudioSource.volume = maximumBreathingVolume * breathingBlend;
            breathingAudioSource.pitch = Mathf.Lerp(0.92f, 1.22f, breathingBlend);
        }
    }

    /// <summary>
    /// Approximates the light reaching the player by summing the contribution of every enabled
    /// light plus the ambient term. Cheaper and more predictable than reading the framebuffer.
    /// </summary>
    private void SampleIllumination()
    {
        if (Time.time >= nextLightSampleTime)
        {
            nextLightSampleTime = Time.time + lightSampleInterval;
            cachedLights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }

        Vector3 samplePosition = headTransform != null ? headTransform.position : transform.position;
        float total = RenderSettings.ambientLight.grayscale * RenderSettings.ambientIntensity;

        for (int i = 0; i < cachedLights.Length; i++)
        {
            Light light = cachedLights[i];
            if (light == null || !light.isActiveAndEnabled || light.intensity <= 0f)
            {
                continue;
            }

            total += GetLightContribution(light, samplePosition);
        }

        illumination = total;
    }

    private static float GetLightContribution(Light light, Vector3 position)
    {
        if (light.type == LightType.Directional)
        {
            return light.intensity * light.color.grayscale;
        }

        Vector3 toPosition = position - light.transform.position;
        float distance = toPosition.magnitude;
        if (distance > light.range)
        {
            return 0f;
        }

        float falloff = 1f - Mathf.Clamp01(distance / Mathf.Max(0.01f, light.range));

        if (light.type == LightType.Spot)
        {
            float angle = Vector3.Angle(light.transform.forward, toPosition);
            if (angle > light.spotAngle * 0.5f)
            {
                return 0f;
            }

            falloff *= 1f - Mathf.Clamp01(angle / Mathf.Max(0.01f, light.spotAngle * 0.5f));
        }

        return light.intensity * light.color.grayscale * falloff;
    }

    /// <summary>Called by <see cref="EyeCloseMechanic"/> while the player holds their eyes shut.</summary>
    public void SetEyesClosed(bool closed)
    {
        eyesClosed = closed;
    }

    public void ModifySanity(float amount)
    {
        float previousSanity = sanity;
        sanity = Mathf.Clamp(sanity + amount, 0f, maximumSanity);

        if (!Mathf.Approximately(previousSanity, sanity))
        {
            SanityChanged?.Invoke(sanity);
        }
    }

    private void OnDisable()
    {
        if (cameraShake != null)
        {
            cameraShake.ClearSource(ShakeSourceId);
        }

        if (screenOverlay != null)
        {
            screenOverlay.SetVignette(0f);
            screenOverlay.SetHaze(0f);
        }
    }
}
