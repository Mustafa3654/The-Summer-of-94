using UnityEngine;

/// <summary>
/// An interactive hinged door. The component lives on the hinge pivot and rotates that pivot,
/// so the visible panel should be a child offset by half its width.
/// Press E while looking at it to swing it open or closed.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class Door : MonoBehaviour, IPlayerInteractable
{
    [Header("Swing")]
    [SerializeField] private float openAngle = 96f;
    [SerializeField] private float openDegreesPerSecond = 130f;
    [SerializeField] private float slamDegreesPerSecond = 900f;
    [SerializeField] private bool swingAwayFromPlayer = true;
    [SerializeField] private bool startOpen;

    [Header("Lock")]
    [SerializeField] private bool locked;

    [Header("Audio")]
    [SerializeField] private AudioSource doorAudioSource;
    [SerializeField] private AudioClip creakClip;
    [SerializeField] private AudioClip slamClip;
    [SerializeField] private AudioClip lockedRattleClip;
    [SerializeField, Range(0f, 1f)] private float creakVolume = 0.55f;
    [SerializeField, Range(0f, 1f)] private float slamVolume = 0.9f;
    [SerializeField, Range(0f, 1f)] private float lockedVolume = 0.7f;

    [Header("Locked Feedback")]
    [SerializeField] private float jiggleAngle = 2.2f;
    [SerializeField] private float jiggleDuration = 0.45f;
    [SerializeField] private float jiggleFrequency = 15f;

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private bool isOpen;
    private float currentDegreesPerSecond;
    private float jiggleTimeRemaining;

    public bool IsOpen => isOpen;
    public bool IsLocked => locked;

    protected virtual void Awake()
    {
        closedRotation = transform.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
        currentDegreesPerSecond = openDegreesPerSecond;

        if (doorAudioSource == null)
        {
            doorAudioSource = GetComponent<AudioSource>();
        }

        doorAudioSource.playOnAwake = false;
        doorAudioSource.loop = false;
        doorAudioSource.spatialBlend = 1f;
        doorAudioSource.rolloffMode = AudioRolloffMode.Linear;
        doorAudioSource.minDistance = 1.5f;
        doorAudioSource.maxDistance = 22f;

        if (creakClip == null) creakClip = ProceduralAudio.CreateDoorCreak();
        if (slamClip == null) slamClip = ProceduralAudio.CreateDoorSlam();
        if (lockedRattleClip == null) lockedRattleClip = ProceduralAudio.CreateHandleJiggle();

        if (startOpen)
        {
            isOpen = true;
            transform.localRotation = openRotation;
        }
    }

    private void Update()
    {
        if (jiggleTimeRemaining > 0f)
        {
            jiggleTimeRemaining -= Time.deltaTime;
            float falloff = Mathf.Clamp01(jiggleTimeRemaining / Mathf.Max(0.0001f, jiggleDuration));
            float offset = Mathf.Sin(Time.time * Mathf.PI * 2f * jiggleFrequency) * jiggleAngle * falloff;
            transform.localRotation = closedRotation * Quaternion.Euler(0f, offset, 0f);

            if (jiggleTimeRemaining <= 0f)
            {
                transform.localRotation = closedRotation;
            }

            return;
        }

        Quaternion target = isOpen ? openRotation : closedRotation;
        if (transform.localRotation != target)
        {
            transform.localRotation = Quaternion.RotateTowards(
                transform.localRotation,
                target,
                currentDegreesPerSecond * Time.deltaTime);
        }
    }

    public void Interact()
    {
        if (!CanOpen())
        {
            OnBlocked();
            return;
        }

        if (isOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    /// <summary>Overridden by <see cref="LockedDoor"/> to add a key check.</summary>
    protected virtual bool CanOpen()
    {
        return !locked;
    }

    /// <summary>Called instead of opening when <see cref="CanOpen"/> refuses.</summary>
    protected virtual void OnBlocked()
    {
        PlayLockedFeedback();
    }

    public void Open()
    {
        if (isOpen)
        {
            return;
        }

        if (swingAwayFromPlayer)
        {
            AimSwingAwayFromPlayer();
        }

        isOpen = true;
        currentDegreesPerSecond = openDegreesPerSecond;
        PlayClip(creakClip, creakVolume);
    }

    public void Close()
    {
        if (!isOpen)
        {
            return;
        }

        isOpen = false;
        currentDegreesPerSecond = openDegreesPerSecond;
        PlayClip(creakClip, creakVolume);
    }

    /// <summary>Snaps the door shut fast with the slam sound. Used by <see cref="DoorSlamTrigger"/>.</summary>
    public void Slam(bool lockAfterwards)
    {
        isOpen = false;
        jiggleTimeRemaining = 0f;
        currentDegreesPerSecond = slamDegreesPerSecond;
        PlayClip(slamClip, slamVolume);

        if (lockAfterwards)
        {
            SetLocked(true);
        }
    }

    public void SetLocked(bool shouldBeLocked)
    {
        locked = shouldBeLocked;
    }

    protected void PlayLockedFeedback()
    {
        if (isOpen)
        {
            return;
        }

        jiggleTimeRemaining = jiggleDuration;
        PlayClip(lockedRattleClip, lockedVolume);
    }

    protected void PlayClip(AudioClip clip, float volume)
    {
        if (doorAudioSource != null && clip != null)
        {
            doorAudioSource.PlayOneShot(clip, volume);
        }
    }

    /// <summary>Flips the hinge direction so the door never swings through the player.</summary>
    private void AimSwingAwayFromPlayer()
    {
        PlayerInventory player = PlayerInventory.Find();
        if (player == null)
        {
            return;
        }

        Vector3 toPlayer = player.transform.position - transform.position;
        float side = Vector3.Dot(transform.forward, toPlayer);
        float signedAngle = side > 0f ? -Mathf.Abs(openAngle) : Mathf.Abs(openAngle);
        openRotation = closedRotation * Quaternion.Euler(0f, signedAngle, 0f);
    }
}
