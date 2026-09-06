using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight key/item bag carried by the player. Items are plain string IDs so doors,
/// pickups and later puzzle scripts can agree on them without shared asset references.
/// </summary>
public sealed class PlayerInventory : MonoBehaviour
{
    [SerializeField] private List<string> startingItemIds = new List<string>();

    private readonly HashSet<string> itemIds = new HashSet<string>();

    /// <summary>Raised with the item ID and its readable name whenever something is collected.</summary>
    public event System.Action<string, string> ItemCollected;

    public IReadOnlyCollection<string> ItemIds => itemIds;

    private void Awake()
    {
        foreach (string startingItemId in startingItemIds)
        {
            if (!string.IsNullOrWhiteSpace(startingItemId))
            {
                itemIds.Add(startingItemId.Trim());
            }
        }
    }

    public bool HasItem(string itemId)
    {
        return !string.IsNullOrWhiteSpace(itemId) && itemIds.Contains(itemId.Trim());
    }

    public bool AddItem(string itemId, string displayName = null)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        if (!itemIds.Add(itemId.Trim()))
        {
            return false;
        }

        ItemCollected?.Invoke(itemId.Trim(), string.IsNullOrWhiteSpace(displayName) ? itemId.Trim() : displayName);
        return true;
    }

    /// <summary>Removes an item, for one-use keys that should be consumed by their lock.</summary>
    public bool ConsumeItem(string itemId)
    {
        return !string.IsNullOrWhiteSpace(itemId) && itemIds.Remove(itemId.Trim());
    }

    /// <summary>Convenience lookup so doors and pickups do not each need a player reference.</summary>
    public static PlayerInventory Find()
    {
        return FindAnyObjectByType<PlayerInventory>();
    }
}
