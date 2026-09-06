using UnityEngine;

/// <summary>
/// One-shot trigger volume that slams a door shut behind the player and locks it,
/// used for the moment the player steps into the cabin foyer.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class DoorSlamTrigger : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Door targetDoor;
    [SerializeField] private bool lockAfterSlam = true;
    [SerializeField] private bool disableAfterTriggering = true;
    [SerializeField] private float slamDelay = 0.35f;

    [Header("Reaction")]
    [SerializeField] private LightningEffect lightningEffect;
    [SerializeField] private bool flashLightningOnSlam = true;

    private bool triggered;
    private float slamCountdown = -1f;

    public bool Triggered => triggered;

    private void Reset()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void Awake()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null && !triggerCollider.isTrigger)
        {
            triggerCollider.isTrigger = true;
        }

        if (lightningEffect == null)
        {
            lightningEffect = FindAnyObjectByType<LightningEffect>();
        }
    }

    private void Update()
    {
        if (slamCountdown < 0f)
        {
            return;
        }

        slamCountdown -= Time.deltaTime;
        if (slamCountdown > 0f)
        {
            return;
        }

        slamCountdown = -1f;
        SlamNow();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered || targetDoor == null)
        {
            return;
        }

        if (other.GetComponentInParent<FirstPersonController>() == null)
        {
            return;
        }

        triggered = true;

        if (slamDelay > 0f)
        {
            slamCountdown = slamDelay;
        }
        else
        {
            SlamNow();
        }
    }

    private void SlamNow()
    {
        targetDoor.Slam(lockAfterSlam);

        if (flashLightningOnSlam && lightningEffect != null)
        {
            lightningEffect.TriggerFlash();
        }

        if (disableAfterTriggering)
        {
            enabled = false;
            Collider triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null)
            {
                triggerCollider.enabled = false;
            }
        }
    }
}
