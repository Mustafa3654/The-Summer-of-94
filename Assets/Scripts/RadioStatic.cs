using System.Collections;
using UnityEngine;

/// <summary>
/// The 1994 transistor radio: quiet static, harsher static, and a brief eerie transmission.
///
/// Press E while looking at it to lift it off the dashboard, then E again to cycle channels.
/// Once carried it doubles as a spirit detector, clicking faster and hissing louder as a
/// manifested child spirit closes in.
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

    [Header("Spirit Detector")]
    [SerializeField] private bool detectorEnabled = true;
    [SerializeField] private bool detectorRequiresCarrying = true;
    [SerializeField] private float detectionRange = 18f;
    [SerializeField] private AudioSource detectorAudioSource;
    [SerializeField] private AudioClip detectorClickClip;
    [SerializeField, Range(0f, 1f)] private float detectorClickVolume = 0.55f;
    [SerializeField] private float slowestClickInterval = 1.6f;
    [SerializeField] private float fastestClickInterval = 0.06f;
    [SerializeField, Range(0f, 3f)] private float proximityStaticBoost = 1.6f;

    [Header("Carrying")]
    [SerializeField] private bool canBePickedUp = true;
    [SerializeField] private Vector3 carriedLocalPosition = new Vector3(0.34f, -0.29f, 0.52f);
    [SerializeField] private Vector3 carriedLocalEulerAngles = new Vector3(14f, -22f, 6f);

    private int currentChannel = -1;
    private float nextInteractionTime;
    private Coroutine transmissionRoutine;
    private bool carried;
    private float channelBaseVolume;
    private float nextClickTime;
    private float detectorProximity;

    /// <summary>0 when no spirit is in range, 1 when one is right on top of the player.</summary>
    public float DetectorProximity => detectorProximity;

    public bool IsCarried => carried;

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

        if (canBePickedUp && !carried)
        {
            PickUp();
            return;
        }

        SetChannel((currentChannel + 1) % 3);
    }

    /// <summary>
    /// Clips the radio to the player camera, off to the side of the crosshair. Everything on it
    /// moves to the built-in Ignore Raycast layer so the held prop never blocks interaction.
    /// </summary>
    private void PickUp()
    {
        Camera playerCamera = Camera.main;
        if (playerCamera == null)
        {
            return;
        }

        carried = true;
        transform.SetParent(playerCamera.transform, false);
        transform.SetLocalPositionAndRotation(
            carriedLocalPosition,
            Quaternion.Euler(carriedLocalEulerAngles));

        SetLayerRecursively(gameObject, 2); // 2 = Ignore Raycast.

        radioAudioSource.spatialBlend = 0.1f;
        if (detectorAudioSource != null)
        {
            detectorAudioSource.spatialBlend = 0.1f;
        }

        Debug.Log("Took the transistor radio. Press E to change channel; its static reacts to spirits.");
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;
        foreach (Transform child in target.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private void Update()
    {
        UpdateDetector();
    }

    /// <summary>
    /// Geiger behaviour: the clicks speed up and the static swells as a spirit gets closer.
    /// </summary>
    private void UpdateDetector()
    {
        bool detecting = detectorEnabled && (carried || !detectorRequiresCarrying);
        detectorProximity = detecting ? MeasureProximity() : 0f;

        // The transmission coroutine owns the volume while it runs, so leave it alone.
        if (transmissionRoutine == null && radioAudioSource != null && channelBaseVolume > 0f)
        {
            radioAudioSource.volume = channelBaseVolume * (1f + proximityStaticBoost * detectorProximity);
            radioAudioSource.pitch = Mathf.Lerp(1f, 1.22f, detectorProximity);
        }

        if (!detecting || detectorProximity <= 0.001f || detectorAudioSource == null || detectorClickClip == null)
        {
            return;
        }

        if (Time.time < nextClickTime)
        {
            return;
        }

        float interval = Mathf.Lerp(slowestClickInterval, fastestClickInterval, detectorProximity);
        nextClickTime = Time.time + Mathf.Max(0.03f, interval);
        detectorAudioSource.pitch = Random.Range(0.94f, 1.1f);
        detectorAudioSource.PlayOneShot(detectorClickClip, detectorClickVolume * Mathf.Lerp(0.45f, 1f, detectorProximity));
    }

    private float MeasureProximity()
    {
        if (!ChildSpirit.TryGetNearestManifested(transform.position, out float distance, out float presence))
        {
            return 0f;
        }

        if (distance > detectionRange)
        {
            return 0f;
        }

        float closeness = 1f - Mathf.Clamp01(distance / Mathf.Max(0.01f, detectionRange));
        return Mathf.Clamp01(closeness * Mathf.Max(0.35f, presence));
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
            channelBaseVolume = 0f; // The coroutine drives the volume for the transmission.
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
        channelBaseVolume = quiet ? quietStaticVolume : harshStaticVolume;
        radioAudioSource.volume = channelBaseVolume;
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

        if (detectorClickClip == null)
        {
            detectorClickClip = ProceduralAudio.CreateGeigerClick();
        }

        if (detectorAudioSource == null)
        {
            detectorAudioSource = gameObject.AddComponent<AudioSource>();
            detectorAudioSource.playOnAwake = false;
            detectorAudioSource.loop = false;
            detectorAudioSource.spatialBlend = radioAudioSource.spatialBlend;
            detectorAudioSource.minDistance = 1f;
            detectorAudioSource.maxDistance = 8f;
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
