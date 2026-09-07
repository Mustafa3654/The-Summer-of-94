using UnityEngine;

/// <summary>
/// Provides lightweight procedural rain and thunder ambience when no imported audio assets are assigned.
/// </summary>
public sealed class StormAmbience : MonoBehaviour
{
    [Header("Audio Sources")]
    [SerializeField] private AudioSource rainAudioSource;
    [SerializeField] private AudioSource thunderAudioSource;
    [SerializeField] private AudioClip rainClip;
    [SerializeField] private AudioClip thunderClip;

    [Header("Storm Levels")]
    [SerializeField, Range(0f, 1f)] private float introRainVolume = 0.16f;
    [SerializeField, Range(0f, 1f)] private float breakdownRainVolume = 0.82f;
    [SerializeField, Range(0f, 1f)] private float introThunderVolume = 0.18f;
    [SerializeField, Range(0f, 1f)] private float breakdownThunderVolume = 0.75f;
    [SerializeField] private Vector2 thunderInterval = new Vector2(5f, 13f);

    private float thunderTimer;
    private float thunderVolume;

    private void Awake()
    {
        if (rainAudioSource == null)
        {
            rainAudioSource = gameObject.AddComponent<AudioSource>();
        }

        if (thunderAudioSource == null)
        {
            thunderAudioSource = gameObject.AddComponent<AudioSource>();
        }

        ConfigureSource(rainAudioSource, true);
        ConfigureSource(thunderAudioSource, false);
        EnsureFallbackClips();
    }

    private void Start()
    {
        rainAudioSource.clip = rainClip;
        rainAudioSource.volume = introRainVolume;
        rainAudioSource.Play();

        thunderVolume = introThunderVolume;
        thunderTimer = Random.Range(3f, 7f);
    }

    private void Update()
    {
        thunderTimer -= Time.deltaTime;
        if (thunderTimer > 0f)
        {
            return;
        }

        thunderTimer = Random.Range(
            Mathf.Min(thunderInterval.x, thunderInterval.y),
            Mathf.Max(thunderInterval.x, thunderInterval.y));
        thunderAudioSource.PlayOneShot(thunderClip, thunderVolume);
        SubtitleManager.Caption("[Thunder rumbles]", 2.5f, SubtitleManager.Priority.Ambient);
    }

    public void SetBreakdownIntensity()
    {
        if (rainAudioSource != null)
        {
            rainAudioSource.volume = breakdownRainVolume;
        }

        thunderVolume = breakdownThunderVolume;
        SubtitleManager.Caption("[Rain and thunder roaring outside...]", 4f, SubtitleManager.Priority.Ambient);
    }

    private static void ConfigureSource(AudioSource source, bool loop)
    {
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
    }

    private void EnsureFallbackClips()
    {
        if (rainClip == null)
        {
            rainClip = CreateRainClip();
        }

        if (thunderClip == null)
        {
            thunderClip = CreateThunderClip();
        }
    }

    private static AudioClip CreateRainClip()
    {
        const int sampleRate = 22050;
        const float duration = 8f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        System.Random random = new System.Random(199407);

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;
            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            float filteredNoise = noise * 0.17f;
            float distantWash = Mathf.Sin(time * Mathf.PI * 2f * 42f) * 0.035f;
            float dropEnvelope = Mathf.Pow(Mathf.Abs(Mathf.Sin(time * Mathf.PI * 2f * 8.7f)), 18f);
            float drop = Mathf.Sin(time * Mathf.PI * 2f * 3200f) * dropEnvelope * 0.17f;
            samples[i] = filteredNoise + distantWash + drop;
        }

        AudioClip clip = AudioClip.Create("Procedural Rain Outside Windshield", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreateThunderClip()
    {
        const int sampleRate = 22050;
        const float duration = 3.4f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        System.Random random = new System.Random(94);

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;
            float progress = time / duration;
            float envelope = Mathf.Pow(1f - progress, 1.5f) * Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI);
            float rumble = Mathf.Sin(time * Mathf.PI * 2f * 34f) * 0.42f;
            rumble += Mathf.Sin(time * Mathf.PI * 2f * 57f) * 0.22f;
            float noise = (float)(random.NextDouble() * 2.0 - 1.0) * 0.25f;
            float crack = time < 0.11f ? Mathf.Sin(time * Mathf.PI * 2f * 1250f) * (1f - time / 0.11f) * 0.3f : 0f;
            samples[i] = (rumble + noise + crack) * envelope * 0.8f;
        }

        AudioClip clip = AudioClip.Create("Procedural Distant Thunder", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void OnDisable()
    {
        if (rainAudioSource != null)
        {
            rainAudioSource.Stop();
        }

        if (thunderAudioSource != null)
        {
            thunderAudioSource.Stop();
        }
    }
}
