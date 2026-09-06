using UnityEngine;

/// <summary>
/// The jerry can that fuels the camp generator. Press E to take it.
///
/// Kept separate from <see cref="KeyItem"/> because it is a heavy object rather than a pocket
/// item: it sloshes when lifted, and carrying it makes the player noisier.
/// </summary>
public sealed class FuelCanister : MonoBehaviour, IPlayerInteractable
{
    [Header("Item")]
    [SerializeField] private string itemId = "FuelCan";
    [SerializeField] private string displayName = "Jerry Can of Fuel";

    [Header("Pickup")]
    [SerializeField] private AudioClip sloshClip;
    [SerializeField, Range(0f, 1f)] private float sloshVolume = 0.7f;
    [SerializeField] private bool logPickup = true;

    [Header("Noise")]
    [SerializeField] private float pickupNoiseRadius = 8f;

    private bool collected;

    public string ItemId => itemId;

    private void Awake()
    {
        if (sloshClip == null)
        {
            sloshClip = ProceduralAudio.CreateFuelSlosh();
        }
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
            Debug.LogWarning($"{name} could not find a PlayerInventory to receive '{itemId}'.", this);
            return;
        }

        if (!inventory.AddItem(itemId, displayName))
        {
            return;
        }

        collected = true;

        if (sloshClip != null)
        {
            // Detached, because this object is destroyed on the same frame.
            AudioSource.PlayClipAtPoint(sloshClip, transform.position, sloshVolume);
        }

        // Hauling a metal can about is not quiet.
        NoiseEvents.Emit(transform.position, pickupNoiseRadius);

        if (logPickup)
        {
            Debug.Log($"Picked up: {displayName} ({itemId}). The generator is out in the yard.");
        }

        Destroy(gameObject);
    }
}
