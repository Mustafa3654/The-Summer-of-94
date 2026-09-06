using UnityEngine;

/// <summary>
/// Coordinates the generated car intro state and controls when the driver can leave.
/// </summary>
public sealed class CarController : MonoBehaviour, IPlayerInteractable
{
    [Header("Intro References")]
    [SerializeField] private FirstPersonController playerController;
    [SerializeField] private Transform playerSeat;
    [SerializeField] private Transform playerCamera;
    [SerializeField] private Transform driverDoor;
    [SerializeField] private Collider driverDoorCollider;

    [Header("Player Placement")]
    [SerializeField] private bool lockPlayerInSeatOnStart = true;
    [SerializeField] private Vector3 seatedLocalPosition = new Vector3(-0.72f, 0.41f, 0.15f);
    [SerializeField] private Vector3 seatedLocalEulerAngles = new Vector3(0f, 0f, 0f);
    [SerializeField] private Vector3 exitLocalPosition = new Vector3(-3.1f, 0.05f, 0.05f);

    private bool exitUnlocked;
    private bool playerHasExited;
    private bool seatReferenceApplied;

    public bool ExitUnlocked => exitUnlocked;
    public bool PlayerHasExited => playerHasExited;
    public Transform DriverDoor => driverDoor;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();

        if (lockPlayerInSeatOnStart)
        {
            PlacePlayerInSeat();
        }

        SetExitUnlocked(false);
    }

    public void Interact()
    {
        if (!exitUnlocked || playerHasExited)
        {
            return;
        }

        ExitVehicle();
    }

    public void UnlockDriverExit()
    {
        SetExitUnlocked(true);
    }

    public void ExitVehicle()
    {
        if (!exitUnlocked || playerHasExited || playerController == null)
        {
            return;
        }

        playerHasExited = true;
        Transform playerTransform = playerController.transform;
        playerTransform.SetParent(null, true);

        if (driverDoor != null)
        {
            driverDoor.localRotation = Quaternion.Euler(0f, -72f, 0f);
        }

        if (driverDoorCollider != null)
        {
            driverDoorCollider.enabled = false;
        }

        // A CharacterController overrides transform writes while enabled, so teleport with it off.
        CharacterController characterController = playerController.GetComponent<CharacterController>();
        bool controllerWasEnabled = characterController != null && characterController.enabled;
        if (controllerWasEnabled)
        {
            characterController.enabled = false;
        }

        playerTransform.SetPositionAndRotation(transform.TransformPoint(exitLocalPosition), transform.rotation);

        if (controllerWasEnabled)
        {
            characterController.enabled = true;
        }

        playerController.ResetVerticalVelocity();
        playerController.enabled = true;
        playerController.SetMovementLocked(false);
    }

    public void PlacePlayerInSeat()
    {
        ResolveReferences();
        if (playerController == null || playerSeat == null || seatReferenceApplied)
        {
            return;
        }

        playerController.enabled = true;
        playerController.SetMovementLocked(true);
        Transform playerTransform = playerController.transform;
        playerTransform.SetParent(playerSeat, true);
        playerTransform.localPosition = seatedLocalPosition;
        playerTransform.localRotation = Quaternion.Euler(seatedLocalEulerAngles);

        if (playerCamera != null)
        {
            playerCamera.localRotation = Quaternion.identity;
        }

        seatReferenceApplied = true;
    }

    private void SetExitUnlocked(bool isUnlocked)
    {
        exitUnlocked = isUnlocked;
        if (!exitUnlocked && driverDoorCollider != null)
        {
            driverDoorCollider.enabled = true;
        }
    }

    private void ResolveReferences()
    {
        if (playerController == null)
        {
            playerController = FindAnyObjectByType<FirstPersonController>();
        }

        if (playerCamera == null && playerController != null)
        {
            Camera camera = playerController.GetComponentInChildren<Camera>();
            if (camera != null)
            {
                playerCamera = camera.transform;
            }
        }
    }
}
