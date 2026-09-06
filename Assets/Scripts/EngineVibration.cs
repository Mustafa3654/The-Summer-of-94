using UnityEngine;

/// <summary>
/// Idle engine rumble felt through the camera while the player sits in the car.
/// The motion itself is composed by <see cref="CameraShake"/>, which is the only component
/// allowed to write the camera transform, so this never fights the sanity wobble.
/// </summary>
public sealed class EngineVibration : MonoBehaviour
{
    private const string ShakeSourceId = "EngineIdle";

    [SerializeField] private Transform targetCamera;
    [SerializeField] private CameraShake cameraShake;
    [SerializeField] private float positionalAmplitude = 0.008f;
    [SerializeField] private float rotationalAmplitude = 0.28f;
    [SerializeField] private float frequency = 7.5f;
    [SerializeField] private bool vibrating = true;

    public bool IsVibrating => vibrating;

    private void Awake()
    {
        if (targetCamera == null)
        {
            Camera childCamera = GetComponentInChildren<Camera>();
            if (childCamera != null) targetCamera = childCamera.transform;
        }

        if (cameraShake == null)
        {
            cameraShake = CameraShake.FindOrCreate(transform);
        }
    }

    private void OnEnable()
    {
        ApplyToShaker();
    }

    public void SetVibrating(bool shouldVibrate)
    {
        vibrating = shouldVibrate;
        ApplyToShaker();
    }

    private void ApplyToShaker()
    {
        if (cameraShake == null)
        {
            return;
        }

        if (vibrating)
        {
            cameraShake.SetSource(ShakeSourceId, positionalAmplitude, rotationalAmplitude, frequency);
        }
        else
        {
            cameraShake.ClearSource(ShakeSourceId);
        }
    }

    private void OnDisable()
    {
        if (cameraShake != null)
        {
            cameraShake.ClearSource(ShakeSourceId);
        }
    }
}
