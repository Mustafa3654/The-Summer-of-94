using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Lightweight first-person controller for the Summer of '94 sandbox.
/// Uses Unity's Input System package for keyboard and mouse input.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public sealed class FirstPersonController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4.5f;
    [SerializeField] private float sprintMultiplier = 1.75f;
    [SerializeField] private float movementSmoothing = 0.08f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedPull = -2f;

    [Header("Noise")]
    [SerializeField] private bool emitFootstepNoise = true;
    [SerializeField] private float walkNoiseRadius = 4.5f;
    [SerializeField] private float sprintNoiseRadius = 17f;
    [SerializeField] private float noiseInterval = 0.45f;

    [Header("Look")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float mouseSensitivity = 0.10f;
    [SerializeField] private float maxLookAngle = 89f;
    [SerializeField] private bool lockCursorOnStart = true;

    private CharacterController characterController;
    private Vector3 planarVelocity;
    private Vector3 planarVelocitySmoothing;
    private float verticalVelocity;
    private float pitch;
    private bool cursorLocked;
    private bool movementLocked;
    private float moveSpeedMultiplier = 1f;
    private bool sprinting;
    private float nextNoiseTime;

    public bool MovementLocked => movementLocked;

    /// <summary>True while the player is actually running, not merely holding shift.</summary>
    public bool IsSprinting => sprinting;

    /// <summary>Current planar speed in metres per second.</summary>
    public float CurrentSpeed => planarVelocity.magnitude;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (cameraTransform == null)
        {
            Camera childCamera = GetComponentInChildren<Camera>();
            if (childCamera != null)
            {
                cameraTransform = childCamera.transform;
            }
        }
    }

    private void Start()
    {
        if (lockCursorOnStart)
        {
            SetCursorLock(true);
        }
    }

    private void Update()
    {
        HandleCursorInput();
        HandleLook();
        HandleMovement();
    }

    private void HandleCursorInput()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SetCursorLock(false);
        }
        else if (!cursorLocked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            SetCursorLock(true);
        }
    }

    private void HandleLook()
    {
        if (!cursorLocked || Mouse.current == null)
        {
            return;
        }

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        float yaw = mouseDelta.x * mouseSensitivity;
        float lookDelta = mouseDelta.y * mouseSensitivity;

        transform.Rotate(Vector3.up, yaw, Space.Self);

        pitch = Mathf.Clamp(pitch - lookDelta, -maxLookAngle, maxLookAngle);
        if (cameraTransform != null)
        {
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    private void HandleMovement()
    {
        if (movementLocked)
        {
            planarVelocity = Vector3.zero;
            planarVelocitySmoothing = Vector3.zero;
            verticalVelocity = 0f;
            return;
        }

        Vector2 input = ReadMovementInput();
        Vector3 desiredDirection = (transform.right * input.x + transform.forward * input.y);
        desiredDirection = Vector3.ClampMagnitude(desiredDirection, 1f);

        bool wantsToSprint = Keyboard.current != null
            && Keyboard.current.leftShiftKey.isPressed
            && input.sqrMagnitude > 0.04f;

        // Sprinting is disallowed while the eyes are shut, which is what the speed penalty means.
        sprinting = wantsToSprint && moveSpeedMultiplier > 0.9f;

        float speed = moveSpeed * moveSpeedMultiplier * (sprinting ? sprintMultiplier : 1f);
        Vector3 desiredVelocity = desiredDirection * speed;

        planarVelocity = Vector3.SmoothDamp(
            planarVelocity,
            desiredVelocity,
            ref planarVelocitySmoothing,
            movementSmoothing);

        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedPull;
        }

        verticalVelocity += gravity * Time.deltaTime;
        Vector3 motion = planarVelocity + Vector3.up * verticalVelocity;
        characterController.Move(motion * Time.deltaTime);

        EmitFootstepNoise();
    }

    /// <summary>
    /// Reports footsteps to <see cref="NoiseEvents"/>. Sprinting carries far enough to pull the
    /// Teacher across the camp; walking barely registers.
    /// </summary>
    private void EmitFootstepNoise()
    {
        if (!emitFootstepNoise || Time.time < nextNoiseTime)
        {
            return;
        }

        float speed = planarVelocity.magnitude;
        if (speed < 0.4f)
        {
            return;
        }

        nextNoiseTime = Time.time + noiseInterval;

        float radius = sprinting
            ? sprintNoiseRadius
            : walkNoiseRadius * Mathf.Clamp01(speed / Mathf.Max(0.01f, moveSpeed));

        NoiseEvents.Emit(transform.position, radius);
    }

    public void ResetVerticalVelocity()
    {
        verticalVelocity = 0f;
    }

    /// <summary>
    /// Scales walking speed. Used by <see cref="EyeCloseMechanic"/> to slow the player to a
    /// shuffle while their eyes are shut.
    /// </summary>
    public void SetMoveSpeedMultiplier(float multiplier)
    {
        moveSpeedMultiplier = Mathf.Clamp(multiplier, 0f, 4f);
    }

    public void SetMovementLocked(bool locked)
    {
        movementLocked = locked;
        if (locked)
        {
            planarVelocity = Vector3.zero;
            planarVelocitySmoothing = Vector3.zero;
            verticalVelocity = 0f;
        }
    }

    private static Vector2 ReadMovementInput()
    {
        if (Keyboard.current == null)
        {
            return Vector2.zero;
        }

        Vector2 input = Vector2.zero;
        if (Keyboard.current.aKey.isPressed) input.x -= 1f;
        if (Keyboard.current.dKey.isPressed) input.x += 1f;
        if (Keyboard.current.sKey.isPressed) input.y -= 1f;
        if (Keyboard.current.wKey.isPressed) input.y += 1f;
        return Vector2.ClampMagnitude(input, 1f);
    }

    private void SetCursorLock(bool shouldLock)
    {
        cursorLocked = shouldLock;
        Cursor.lockState = shouldLock ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !shouldLock;
    }
}
