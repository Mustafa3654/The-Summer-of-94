using UnityEngine;

/// <summary>
/// A world pickup. Press E while looking at it to add its item ID to the
/// <see cref="PlayerInventory"/> and remove the object from the scene.
/// </summary>
public sealed class KeyItem : MonoBehaviour, IPlayerInteractable
{
    [Header("Item")]
    [SerializeField] private string itemId = "CabinKey";
    [SerializeField] private string displayName = "Rusted Cabin Key";

    [Header("Idle Motion")]
    [SerializeField] private bool idleAnimation = true;
    [SerializeField] private float spinDegreesPerSecond = 42f;
    [SerializeField] private float bobHeight = 0.06f;
    [SerializeField] private float bobFrequency = 1.1f;

    [Header("Pickup")]
    [SerializeField] private AudioClip pickupClip;
    [SerializeField, Range(0f, 1f)] private float pickupVolume = 0.6f;
    [SerializeField] private bool logPickup = true;

    private Vector3 restPosition;
    private float bobSeed;
    private bool collected;

    public string ItemId => itemId;
    public string DisplayName => displayName;

    private void Awake()
    {
        restPosition = transform.localPosition;
        bobSeed = Random.Range(0f, Mathf.PI * 2f);

        if (pickupClip == null)
        {
            pickupClip = ProceduralAudio.CreatePickupChime();
        }
    }

    private void Update()
    {
        if (!idleAnimation || collected)
        {
            return;
        }

        transform.Rotate(Vector3.up, spinDegreesPerSecond * Time.deltaTime, Space.Self);
        float bob = Mathf.Sin(Time.time * Mathf.PI * 2f * bobFrequency + bobSeed) * bobHeight;
        transform.localPosition = restPosition + Vector3.up * bob;
    }

    public void Interact()
    {
        if (collected)
        {
            return;
        }

        PlayerInventory inventory = PlayerInventory.Find();
        if (inventory == null)
        {
            Debug.LogWarning($"{name} could not find a PlayerInventory to receive '{itemId}'.");
            return;
        }

        if (!inventory.AddItem(itemId, displayName))
        {
            return;
        }

        collected = true;

        if (pickupClip != null)
        {
            // Played detached, because this object is destroyed on the same frame.
            AudioSource.PlayClipAtPoint(pickupClip, transform.position, pickupVolume);
        }

        if (logPickup)
        {
            Debug.Log($"Picked up: {displayName} ({itemId})");
        }

        Destroy(gameObject);
    }
}
