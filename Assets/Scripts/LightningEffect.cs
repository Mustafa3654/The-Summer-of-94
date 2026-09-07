using System.Collections;
using UnityEngine;

/// <summary>
/// Produces occasional cold, rapid directional-light flashes against the otherwise black scene.
///
/// Each strike also drives <see cref="NightSkyController"/>, so the skybox, ambient light, and fog
/// spike to icy white-blue at the same moment. Without that the ground lit up while the open sky
/// above stayed flat black, which read as a light switch rather than lightning.
/// </summary>
public sealed class LightningEffect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Light lightningLight;

    [Header("Storm Timing")]
    [SerializeField] private Vector2 strikeInterval = new Vector2(7f, 18f);
    [SerializeField] private Vector2 flashDuration = new Vector2(0.05f, 0.15f);
    [SerializeField, Range(0f, 1f)] private float secondPulseChance = 0.45f;

    [Header("Light")]
    [SerializeField] private float peakIntensity = 2.8f;
    [SerializeField] private Color lightningColor = new Color(0.62f, 0.76f, 1f);
    [SerializeField] private float directionalAngle = 42f;

    [Header("Sky Sync")]
    [SerializeField] private NightSkyController nightSky;
    [SerializeField] private bool flashTheSky = true;
    [SerializeField, Range(0f, 1f)] private float skyFlashStrength = 1f;
    [SerializeField, Range(0f, 1f)] private float secondPulseSkyStrength = 0.55f;
    [SerializeField] private float skyFadeOutSeconds = 0.22f;

    private float nextStrikeTime;
    private Coroutine skyFadeRoutine;

    private void Awake()
    {
        if (lightningLight == null)
        {
            lightningLight = GetComponentInChildren<Light>();
        }

        if (nightSky == null)
        {
            nightSky = NightSkyController.Find();
        }

        if (lightningLight != null)
        {
            lightningLight.type = LightType.Directional;
            lightningLight.color = lightningColor;
            lightningLight.intensity = 0f;
            lightningLight.enabled = false;
        }
    }

    private void OnEnable()
    {
        ScheduleNextStrike(true);
    }

    private void Update()
    {
        if (lightningLight == null || Time.time < nextStrikeTime)
        {
            return;
        }

        ScheduleNextStrike(false);
        StartCoroutine(StrikeRoutine());
    }

    /// <summary>
    /// Fires a strike immediately, for scripted beats such as the foyer door slamming shut.
    /// The random schedule is pushed back so the cued flash is not doubled up.
    /// </summary>
    public void TriggerFlash()
    {
        if (lightningLight == null || !isActiveAndEnabled)
        {
            return;
        }

        ScheduleNextStrike(false);
        StartCoroutine(StrikeRoutine());
    }

    private IEnumerator StrikeRoutine()
    {
        float elevation = Mathf.Clamp(directionalAngle + Random.Range(-14f, 14f), 15f, 75f);
        transform.rotation = Quaternion.Euler(
            elevation,
            Random.Range(0f, 360f),
            0f);

        float brightness = Random.Range(0.72f, 1.12f);
        lightningLight.enabled = true;
        lightningLight.intensity = peakIntensity * brightness;
        SetSkyFlash(skyFlashStrength * Mathf.Clamp01(brightness));

        yield return new WaitForSeconds(Random.Range(
            Mathf.Min(flashDuration.x, flashDuration.y),
            Mathf.Max(flashDuration.x, flashDuration.y)));

        lightningLight.enabled = false;
        BeginSkyFadeOut();

        if (Random.value <= secondPulseChance)
        {
            yield return new WaitForSeconds(Random.Range(0.025f, 0.08f));
            lightningLight.enabled = true;
            lightningLight.intensity = peakIntensity * Random.Range(0.35f, 0.75f);
            SetSkyFlash(secondPulseSkyStrength);

            yield return new WaitForSeconds(Random.Range(0.02f, 0.055f));
            lightningLight.enabled = false;
            BeginSkyFadeOut();
        }
    }

    private void SetSkyFlash(float intensity)
    {
        if (!flashTheSky || nightSky == null)
        {
            return;
        }

        if (skyFadeRoutine != null)
        {
            StopCoroutine(skyFadeRoutine);
            skyFadeRoutine = null;
        }

        nightSky.SetFlashIntensity(intensity);
    }

    /// <summary>
    /// Rolls the sky back down rather than cutting it. A hard cut to black reads as a rendering
    /// glitch; a short decay reads as the afterglow of a strike.
    /// </summary>
    private void BeginSkyFadeOut()
    {
        if (!flashTheSky || nightSky == null)
        {
            return;
        }

        if (skyFadeRoutine != null)
        {
            StopCoroutine(skyFadeRoutine);
        }

        skyFadeRoutine = StartCoroutine(SkyFadeOutRoutine());
    }

    private IEnumerator SkyFadeOutRoutine()
    {
        float startingIntensity = nightSky.FlashIntensity;
        float elapsed = 0f;

        while (elapsed < skyFadeOutSeconds)
        {
            elapsed += Time.deltaTime;
            nightSky.SetFlashIntensity(Mathf.Lerp(startingIntensity, 0f, elapsed / skyFadeOutSeconds));
            yield return null;
        }

        nightSky.SetFlashIntensity(0f);
        skyFadeRoutine = null;
    }

    private void OnDisable()
    {
        skyFadeRoutine = null;

        if (nightSky != null)
        {
            nightSky.SetFlashIntensity(0f);
        }
    }

    private void ScheduleNextStrike(bool initial)
    {
        float minimum = Mathf.Min(strikeInterval.x, strikeInterval.y);
        float maximum = Mathf.Max(strikeInterval.x, strikeInterval.y);
        float delay = Random.Range(minimum, maximum);

        // A shorter first delay makes the effect easy to verify while testing the sandbox.
        if (initial)
        {
            delay = Mathf.Min(delay, 4f);
        }

        nextStrikeTime = Time.time + delay;
    }
}
