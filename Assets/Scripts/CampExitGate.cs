using UnityEngine;

/// <summary>
/// The automated camp gate. Sealed until <see cref="PowerGenerator"/> supplies power, then it
/// grinds open. Walking through the trigger ends the run.
/// </summary>
public sealed class CampExitGate : MonoBehaviour
{
    [Header("Gate Halves")]
    [SerializeField] private Transform leftGate;
    [SerializeField] private Transform rightGate;
    [SerializeField] private float slideDistance = 3.1f;
    [SerializeField] private float slideSpeed = 1.3f;

    [Header("State")]
    [SerializeField] private bool powered;

    [Header("Escape")]
    [SerializeField] private Collider escapeTrigger;
    [SerializeField] private bool freezePlayerOnEscape = true;

    [Header("Audio")]
    [SerializeField] private AudioSource gateAudioSource;
    [SerializeField] private AudioClip openingClip;
    [SerializeField, Range(0f, 1f)] private float openingVolume = 0.85f;

    [Header("Victory Screen")]
    [SerializeField] private ScreenOverlayController screenOverlay;
    [SerializeField] private string victoryTitle = "SURVIVED";
    [SerializeField] private string victorySubtitle = "The Summer of '94";
    [SerializeField] private Color victoryColor = new Color(0.88f, 0.86f, 0.8f);

    private Vector3 leftClosedPosition;
    private Vector3 rightClosedPosition;
    private float openAmount;
    private bool escaped;
    private bool openingSoundPlayed;

    public bool IsPowered => powered;
    public bool HasEscaped => escaped;

    private void Awake()
    {
        if (leftGate != null) leftClosedPosition = leftGate.localPosition;
        if (rightGate != null) rightClosedPosition = rightGate.localPosition;

        if (gateAudioSource == null)
        {
            gateAudioSource = GetComponent<AudioSource>();
        }

        if (gateAudioSource == null)
        {
            gateAudioSource = gameObject.AddComponent<AudioSource>();
        }

        gateAudioSource.playOnAwake = false;
        gateAudioSource.loop = false;
        gateAudioSource.spatialBlend = 1f;
        gateAudioSource.rolloffMode = AudioRolloffMode.Linear;
        gateAudioSource.minDistance = 4f;
        gateAudioSource.maxDistance = 45f;

        if (openingClip == null)
        {
            openingClip = ProceduralAudio.CreateGateRumble();
        }

        if (escapeTrigger != null)
        {
            escapeTrigger.isTrigger = true;
            escapeTrigger.enabled = false;
        }
    }

    private void Start()
    {
        if (screenOverlay == null)
        {
            screenOverlay = ScreenOverlayController.Find();
        }
    }

    private void Update()
    {
        float target = powered ? 1f : 0f;
        if (Mathf.Approximately(openAmount, target))
        {
            return;
        }

        openAmount = Mathf.MoveTowards(openAmount, target, Time.deltaTime * slideSpeed);

        if (leftGate != null)
        {
            leftGate.localPosition = leftClosedPosition + Vector3.left * (slideDistance * openAmount);
        }

        if (rightGate != null)
        {
            rightGate.localPosition = rightClosedPosition + Vector3.right * (slideDistance * openAmount);
        }

        // The way out only counts once the halves have actually parted.
        if (escapeTrigger != null)
        {
            escapeTrigger.enabled = openAmount > 0.45f;
        }
    }

    public void SetPowered(bool isPowered)
    {
        if (powered == isPowered)
        {
            return;
        }

        powered = isPowered;

        if (powered && !openingSoundPlayed)
        {
            openingSoundPlayed = true;
            if (gateAudioSource != null && openingClip != null)
            {
                gateAudioSource.PlayOneShot(openingClip, openingVolume);
            }

            SubtitleManager.Caption("[Camp gate grinding open...]", 4f, SubtitleManager.Priority.Critical);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (escaped || !powered)
        {
            return;
        }

        FirstPersonController player = other.GetComponentInParent<FirstPersonController>();
        if (player == null)
        {
            return;
        }

        escaped = true;

        if (freezePlayerOnEscape)
        {
            player.SetMovementLocked(true);
        }

        if (screenOverlay != null)
        {
            screenOverlay.SetVignette(0f);
            screenOverlay.SetHaze(0f);
            screenOverlay.ShowMessage(victoryTitle, victorySubtitle, victoryColor);
        }

        SubtitleManager.Caption("[You step through the gate into the dark]", 6f, SubtitleManager.Priority.Critical);
        Debug.Log("Escaped Camp Hollow. Survived The Summer of '94.");
    }
}
