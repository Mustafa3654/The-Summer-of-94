using UnityEngine;

/// <summary>
/// Interactive ignition key that manually starts the car breakdown when pressed with E.
/// </summary>
public sealed class IgnitionKey : MonoBehaviour, IPlayerInteractable
{
    [SerializeField] private CarBreakdownSequence breakdownSequence;

    private void Awake()
    {
        if (breakdownSequence == null)
        {
            breakdownSequence = GetComponentInParent<CarBreakdownSequence>();
        }
    }

    public void Interact()
    {
        if (breakdownSequence != null)
        {
            breakdownSequence.TriggerBreakdown();
        }
    }
}
