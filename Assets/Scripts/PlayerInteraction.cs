using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sends E-key interactions to the collider the player is looking at.
/// </summary>
public sealed class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float interactionDistance = 3f;
    // Everything except the built-in Ignore Raycast layer, so a carried prop such as the
    // transistor radio never intercepts interaction rays.
    [SerializeField] private LayerMask interactionMask = ~(1 << 2);

    private void Awake()
    {
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame || playerCamera == null)
        {
            return;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactionMask, QueryTriggerInteraction.Collide))
        {
            return;
        }

        MonoBehaviour[] behaviours = hit.collider.GetComponentsInParent<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is IPlayerInteractable interactable)
            {
                interactable.Interact();
                return;
            }
        }
    }
}

/// <summary>
/// Interface implemented by objects that can be activated by PlayerInteraction.
/// </summary>
public interface IPlayerInteractable
{
    void Interact();
}
