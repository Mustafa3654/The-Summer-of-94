using System.Collections;
using UnityEngine;

/// <summary>
/// Runs the car failure beat: stall audio, dashboard flicker, engine-vibration stop,
/// louder storm ambience, and driver-door unlock.
/// </summary>
public sealed class CarBreakdownSequence : MonoBehaviour
{
    [SerializeField] private CarController carController;
    [SerializeField] private EngineVibration engineVibration;
    [SerializeField] private StormAmbience stormAmbience;
    [SerializeField] private AudioSource engineAudioSource;
    [SerializeField] private AudioClip idleEngineClip;
    [SerializeField] private AudioClip stallEngineClip;
    [SerializeField] private Light[] dashboardLights;

    [Header("Intro Timing")]
    [SerializeField] private bool autoBreakdown = true;
    [SerializeField] private Vector2 automaticBreakdownDelay = new Vector2(10f, 15f);
    [SerializeField] private float doorUnlockDelay = 0.8f;

    [Header("Dashboard Flicker")]
    [SerializeField] private int flickerPulseCount = 7;
    [SerializeField] private Vector2 flickerPulseInterval = new Vector2(0.035f, 0.13f);

    private Coroutine breakdownRoutine;
    private float automaticBreakdownTimer;
    private bool hasBrokenDown;

    public bool HasBrokenDown => hasBrokenDown;

    private void Awake()
    {
        ResolveReferences();
        ConfigureEngineSource();
    }

    private void Start()
    {
        EnsureFallbackClips();
        SetDashboardLights(true);
        if (engineAudioSource != null && idleEngineClip != null)
        {
            engineAudioSource.clip = idleEngineClip;
            engineAudioSource.loop = true;
            engineAudioSource.volume = 0.28f;
            engineAudioSource.Play();
        }

        automaticBreakdownTimer = Random.Range(
            Mathf.Min(automaticBreakdownDelay.x, automaticBreakdownDelay.y),
            Mathf.Max(automaticBreakdownDelay.x, automaticBreakdownDelay.y));
    }

    private void Update()
    {
        if (!autoBreakdown || hasBrokenDown || breakdownRoutine != null)
        {
            return;
        }

        automaticBreakdownTimer -= Time.deltaTime;
        if (automaticBreakdownTimer <= 0f)
        {
            TriggerBreakdown();
        }
    }

    public void TriggerBreakdown()
    {
        if (!hasBrokenDown && breakdownRoutine == null)
        {
            breakdownRoutine = StartCoroutine(BreakdownRoutine());
        }
    }

    private IEnumerator BreakdownRoutine()
    {
        hasBrokenDown = true;
        if (engineAudioSource != null)
        {
            engineAudioSource.Stop();
            engineAudioSource.loop = false;
            engineAudioSource.clip = stallEngineClip;
            engineAudioSource.volume = 0.82f;
            engineAudioSource.Play();
        }

        yield return StartCoroutine(FlickerDashboard());
        if (engineVibration != null) engineVibration.SetVibrating(false);
        if (stormAmbience != null) stormAmbience.SetBreakdownIntensity();
        yield return new WaitForSeconds(doorUnlockDelay);

        SetDashboardLights(false);
        if (carController != null) carController.UnlockDriverExit();
        breakdownRoutine = null;
    }

    private IEnumerator FlickerDashboard()
    {
        int pulses = Mathf.Max(1, flickerPulseCount);
        for (int i = 0; i < pulses; i++)
        {
            SetDashboardLights(i < pulses - 1 && Random.value > 0.18f);
            yield return new WaitForSeconds(Random.Range(
                Mathf.Min(flickerPulseInterval.x, flickerPulseInterval.y),
                Mathf.Max(flickerPulseInterval.x, flickerPulseInterval.y)));
        }
        SetDashboardLights(false);
    }

    private void SetDashboardLights(bool lightsOn)
    {
        if (dashboardLights == null) return;
        foreach (Light dashboardLight in dashboardLights)
        {
            if (dashboardLight != null) dashboardLight.enabled = lightsOn;
        }
    }

    private void ResolveReferences()
    {
        if (carController == null) carController = GetComponent<CarController>();
        if (engineVibration == null)             engineVibration = FindAnyObjectByType<EngineVibration>();

        if (stormAmbience == null)             stormAmbience = FindAnyObjectByType<StormAmbience>();

        if (engineAudioSource == null) engineAudioSource = GetComponent<AudioSource>();
        if (engineAudioSource == null) engineAudioSource = gameObject.AddComponent<AudioSource>();
    }

    private void ConfigureEngineSource()
    {
        if (engineAudioSource == null) return;
        engineAudioSource.playOnAwake = false;
        engineAudioSource.loop = true;
        engineAudioSource.spatialBlend = 0.1f;
        engineAudioSource.minDistance = 1f;
        engineAudioSource.maxDistance = 8f;
    }

    private void EnsureFallbackClips()
    {
        if (idleEngineClip == null) idleEngineClip = CreateEngineClip("Procedural Idling Engine", 3f, 42f, 0.13f, 0.03f);
        if (stallEngineClip == null) stallEngineClip = CreateEngineClip("Procedural Engine Stall", 2.2f, 56f, 0.34f, 0.16f);
    }

    private static AudioClip CreateEngineClip(string clipName, float duration, float baseFrequency, float toneAmount, float sputterAmount)
    {
        const int sampleRate = 22050;
        int sampleCount = Mathf.CeilToInt(duration * sampleRate);
        float[] samples = new float[sampleCount];
        System.Random random = new System.Random(1994 + Mathf.RoundToInt(baseFrequency));

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;
            float progress = time / duration;
            float envelope = clipName.Contains("Stall") ? Mathf.Exp(-progress * 3.6f) : 1f;
            float tone = Mathf.Sin(time * Mathf.PI * 2f * baseFrequency) * toneAmount;
            tone += Mathf.Sin(time * Mathf.PI * 2f * baseFrequency * 2f) * toneAmount * 0.35f;
            tone += Mathf.Sin(time * Mathf.PI * 2f * (6f + progress * 17f)) * sputterAmount;
            float noise = (float)(random.NextDouble() * 2.0 - 1.0) * sputterAmount * 0.3f;
            samples[i] = (tone + noise) * envelope;
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void OnDisable()
    {
        if (breakdownRoutine != null) StopCoroutine(breakdownRoutine);
        breakdownRoutine = null;
        if (engineAudioSource != null) engineAudioSource.Stop();
    }
}
