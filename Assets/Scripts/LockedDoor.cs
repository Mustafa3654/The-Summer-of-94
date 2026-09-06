using UnityEngine;

/// <summary>
/// A door that stays shut until the player inventory contains a specific key ID.
/// Rattles its handle and refuses to open until then.
/// </summary>
public sealed class LockedDoor : Door
{
    [Header("Key")]
    [SerializeField] private string requiredKeyId = "CabinKey";
    [SerializeField] private bool consumeKeyOnUnlock;
    [SerializeField] private bool stayUnlockedOnceOpened = true;

    [Header("Unlock Audio")]
    [SerializeField] private AudioClip unlockClip;
    [SerializeField, Range(0f, 1f)] private float unlockVolume = 0.7f;

    private PlayerInventory playerInventory;
    private bool keyAccepted;

    public string RequiredKeyId => requiredKeyId;
    public bool KeyAccepted => keyAccepted;

    protected override void Awake()
    {
        base.Awake();
        SetLocked(true);

        if (unlockClip == null)
        {
            unlockClip = ProceduralAudio.CreateUnlockClick();
        }
    }

    protected override bool CanOpen()
    {
        if (keyAccepted && stayUnlockedOnceOpened)
        {
            return true;
        }

        if (playerInventory == null)
        {
            playerInventory = PlayerInventory.Find();
        }

        if (playerInventory == null || !playerInventory.HasItem(requiredKeyId))
        {
            return false;
        }

        if (!keyAccepted)
        {
            keyAccepted = true;
            SetLocked(false);
            PlayClip(unlockClip, unlockVolume);

            if (consumeKeyOnUnlock)
            {
                playerInventory.ConsumeItem(requiredKeyId);
            }
        }

        return true;
    }
}
