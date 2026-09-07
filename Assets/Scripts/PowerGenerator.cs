using UnityEngine;

/// <summary>
/// The camp generator. Press E with the fuel can to start it.
///
/// Starting it is the point of no return: the floodlights come up, the exit gate unlocks, and
/// the noise brings the Teacher straight to the yard in a hard chase.
/// </summary>
public sealed class PowerGenerator : MonoBehaviour, IPlayerInteractable
{
    [Header("Fuel")]
    [SerializeField] private string requiredItemId = "FuelCan";
    [SerializeField] private bool consumeFuel = true;

    [Header("Powered Systems")]
    [SerializeField] private Light[] floodlights;
    [SerializeField] private CampExitGate exitGate;
    [SerializeField] private float floodlightRampSeconds = 1.6f;
    [SerializeField] private float floodlightIntensity = 2.4f;

    [Header("Alerting the Teacher")]
    [SerializeField] private TeacherAI teacher;
    [SerializeField] private bool alertTeacherOnStart = true;
    [SerializeField] private float startupNoiseRadius = 90f;

    [Header("Audio")]
    [SerializeField] private AudioSource generatorAudioSource;
    [SerializeField] private AudioClip runningLoopClip;
    [SerializeField] private AudioClip startupClip;
    [SerializeField, Range(0f, 1f)] private float runningVolume = 0.6f;

    [Header("Feedback")]
    [SerializeField] private ScreenOverlayController screenOverlay;
    [SerializeField] private float messageSeconds = 4f;

    private bool powered;
    private float rampProgress;
    private float messageTimer;

    public bool IsPowered => powered;

    private void Awake()
    {
        if (generatorAudioSource == null)
        {
            generatorAudioSource = GetComponent<AudioSource>();
        }

        if (generatorAudioSource == null)
        {
            generatorAudioSource = gameObject.AddComponent<AudioSource>();
        }

        generatorAudioSource.playOnAwake = false;
        generatorAudioSource.loop = true;
        generatorAudioSource.spatialBlend = 1f;
        generatorAudioSource.rolloffMode = AudioRolloffMode.Linear;
        generatorAudioSource.minDistance = 4f;
        generatorAudioSource.maxDistance = 55f;
        generatorAudioSource.volume = 0f;

        if (runningLoopClip == null) runningLoopClip = ProceduralAudio.CreateGeneratorLoop();
        if (startupClip == null) startupClip = ProceduralAudio.CreateDoorSlam("Procedural Generator Kick", 0.9f, 4321);

        SetFloodlights(0f);
    }

    private void Start()
    {
        if (screenOverlay == null)
        {
            screenOverlay = ScreenOverlayController.Find();
        }

        if (teacher == null)
        {
            teacher = FindAnyObjectByType<TeacherAI>();
        }
    }

    private void Update()
    {
        if (powered && rampProgress < 1f)
        {
            rampProgress = Mathf.Clamp01(rampProgress + Time.deltaTime / Mathf.Max(0.01f, floodlightRampSeconds));
            SetFloodlights(rampProgress);
            generatorAudioSource.volume = runningVolume * rampProgress;
        }

        if (messageTimer > 0f)
        {
            messageTimer -= Time.deltaTime;
            if (messageTimer <= 0f && screenOverlay != null)
            {
                screenOverlay.ClearMessage();
            }
        }
    }

    public void Interact()
    {
        if (powered)
        {
            return;
        }

        PlayerInventory inventory = PlayerInventory.Find();
        if (inventory == null || !inventory.HasItem(requiredItemId))
        {
            ShowMessage("The tank is dry.", "Find the fuel can.", new Color(0.85f, 0.8f, 0.72f));
            return;
        }

        if (consumeFuel)
        {
            inventory.ConsumeItem(requiredItemId);
        }

        StartGenerator();
    }

    private void StartGenerator()
    {
        powered = true;
        rampProgress = 0f;

        SubtitleManager.Caption("[Pouring fuel into generator...]", 2.5f, SubtitleManager.Priority.Critical);

        if (startupClip != null)
        {
            generatorAudioSource.PlayOneShot(startupClip, 0.9f);
        }

        generatorAudioSource.clip = runningLoopClip;
        generatorAudioSource.loop = true;
        generatorAudioSource.volume = 0f;
        generatorAudioSource.Play();

        if (exitGate != null)
        {
            exitGate.SetPowered(true);
        }

        if (alertTeacherOnStart)
        {
            // Deafening at this range: the Teacher does not need to hear anything else.
            NoiseEvents.Emit(transform.position, startupNoiseRadius);

            if (teacher != null)
            {
                teacher.AlertHardChase(transform.position);
            }
        }

        SubtitleManager.Caption("[Generator roaring to life!]", 4f, SubtitleManager.Priority.Critical);
        ShowMessage("The gate is open.", "He heard that. Run.", new Color(0.95f, 0.55f, 0.4f));
        Debug.Log("Generator started: floodlights up, exit gate unlocked, Teacher alerted.");
    }

    private void SetFloodlights(float blend)
    {
        if (floodlights == null)
        {
            return;
        }

        foreach (Light floodlight in floodlights)
        {
            if (floodlight == null)
            {
                continue;
            }

            floodlight.enabled = blend > 0.01f;

            // A cold sodium flicker while the lights come up to full.
            float flicker = blend < 1f ? Random.Range(0.55f, 1f) : 1f;
            floodlight.intensity = floodlightIntensity * blend * flicker;
        }
    }

    private void ShowMessage(string title, string subtitle, Color color)
    {
        if (screenOverlay == null)
        {
            return;
        }

        screenOverlay.ShowMessage(title, subtitle, color);
        messageTimer = messageSeconds;
    }
}
