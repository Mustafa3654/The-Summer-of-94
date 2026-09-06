using System.Collections;
using UnityEngine;

/// <summary>
/// Interactive dashboard radio with quiet static, harsher static, and a brief eerie transmission.
/// Press E while looking at the radio to cycle channels.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public sealed class RadioStatic : MonoBehaviour, IPlayerInteractable
{
    [Header("References")]
    [SerializeField] private AudioSource radioAudioSource;
    [SerializeField] private AudioClip quietStaticClip;
    [SerializeField] private AudioClip harshStaticClip;
    [SerializeField] private AudioClip eerieTransmissionClip;

    [Header("Playback")]
    [SerializeField, Range(0f, 1f)] private float quietStaticVolume = 0.16f;
    [SerializeField, Range(0f, 1f)] private float harshStaticVolume = 0.24f;
    [SerializeField, Range(0f, 1f)] private float transmissionVolume = 0.34f;
    [SerializeField] private float interactionCooldown = 0.25f;
    [SerializeField] private float transmissionFadeDuration = 0.9f;

    private int currentChannel = -1;
    private float nextInteractionTime;
    private Coroutine transmissionRoutine;

    private void Awake()
    {
        if (radioAudioSource == null)
        {
            radioAudioSource = GetComponent<AudioSource>();
        }

        radioAudioSource.playOnAwake = false;
        radioAudioSource.loop = true;
        radioAudioSource.spatialBlend = 0.35f;
        radioAudioSource.minDistance = 1f;
        radioAudioSource.maxDistance = 8f;
    }

    private void Start()
    {
        EnsureFallbackClips();
        SetChannel(0);
    }

    public void Interact()
    {
        if (Time.time < nextInteractionTime)
        {
            return;
        }

        nextInteractionTime = Time.time + interactionCooldown;
        SetChannel((currentChannel + 1) % 3);
    }

    private void SetChannel(int channel)
    {
        currentChannel = channel;

        if (transmissionRoutine != null)
        {
            StopCoroutine(transmissionRoutine);
            transmissionRoutine = null;
        }

        if (channel == 1)
        {
            transmissionRoutine = StartCoroutine(PlayTransmissionThenStatic());
            return;
        }

        PlayStatic(channel == 0);
    }

    private void PlayStatic(bool quiet)
    {
        radioAudioSource.Stop();
        radioAudioSource.loop = true;
        radioAudioSource.clip = quiet ? quietStaticClip : harshStaticClip;
        radioAudioSource.volume = quiet ? quietStaticVolume : harshStaticVolume;
        radioAudioSource.Play();
    }

    private IEnumerator PlayTransmissionThenStatic()
    {
        radioAudioSource.Stop();
        radioAudioSource.loop = false;
        radioAudioSource.clip = eerieTransmissionClip;
        radioAudioSource.volume = transmissionVolume;
        radioAudioSource.Play();

        float transmissionLength = eerieTransmissionClip != null ? eerieTransmissionClip.length : 2f;
        float fadeStart = Mathf.Max(0f, transmissionLength - transmissionFadeDuration);
        yield return new WaitForSeconds(fadeStart);

        float startingVolume = radioAudioSource.volume;
        float elapsed = 0f;
        while (elapsed < transmissionFadeDuration)
        {
            elapsed += Time.deltaTime;
            radioAudioSource.volume = Mathf.Lerp(startingVolume, 0f, elapsed / transmissionFadeDuration);
            yield return null;
        }

        // Settle back into quiet static without rewinding the channel counter, so the
        // next E press still advances to the harsh-static channel.
        PlayStatic(true);
        transmissionRoutine = null;
    }

    private void EnsureFallbackClips()
    {
        if (quietStaticClip == null)
        {
            quietStaticClip = CreateStaticClip("Procedural Quiet Radio Static", 6f, 0.06f, 0.2f);
        }

        if (harshStaticClip == null)
        {
            harshStaticClip = CreateStaticClip("Procedural Harsh Radio Static", 6f, 0.14f, 0.75f);
        }

        if (eerieTransmissionClip == null)
        {
            eerieTransmissionClip = CreateEerieTransmissionClip();
        }
    }

    private static AudioClip CreateStaticClip(string clipName, float duration, float amplitude, float modulation)
    {
        const int sampleRate = 22050;
        int sampleCount = Mathf.CeilToInt(duration * sampleRate);
        float[] samples = new float[sampleCount];
        System.Random random = new System.Random(9407 + Mathf.RoundToInt(amplitude * 1000f));

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;
            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            float carrier = Mathf.Sin(time * Mathf.PI * 2f * 110f) * modulation * 0.12f;
            float envelope = 0.72f + Mathf.Sin(time * Mathf.PI * 2f * 0.7f) * 0.18f;
            samples[i] = (noise * 0.72f + carrier) * amplitude * envelope;
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreateEerieTransmissionClip()
    {
        const int sampleRate = 22050;
        const float duration = 3.2f;
        int sampleCount = Mathf.CeilToInt(duration * sampleRate);
        float[] samples = new float[sampleCount];
        System.Random random = new System.Random(1994);

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;
            float progress = time / duration;
            float envelope = Mathf.Sin(progress * Mathf.PI);
            float syllable = Mathf.Abs(Mathf.Sin(time * Mathf.PI * 2f * 2.15f));
            float voice = Mathf.Sin(time * Mathf.PI * 2f * (132f + Mathf.Sin(time * 8f) * 18f));
            float whisperNoise = (float)(random.NextDouble() * 2.0 - 1.0);
            float reversedTail = Mathf.Sin((duration - time) * Mathf.PI * 2f * 96f);
            float grain = Mathf.Sin(time * Mathf.PI * 2f * 680f) * 0.12f;

            // A low-volume, radio-distorted whisper-like burst rather than a clean voice sample.
            samples[i] = envelope * (voice * syllable * 0.21f + whisperNoise * 0.12f + reversedTail * 0.08f + grain) * 0.65f;
        }

        AudioClip clip = AudioClip.Create("Procedural Distorted Whisper", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void OnDisable()
    {
        if (transmissionRoutine != null)
        {
            StopCoroutine(transmissionRoutine);
            transmissionRoutine = null;
        }

        if (radioAudioSource != null)
        {
            radioAudioSource.Stop();
        }
    }
}
