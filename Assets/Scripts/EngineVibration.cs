using UnityEngine;

/// <summary>
/// Applies small layered positional and rotational vibration while the engine is idling.
/// The offset is re-derived every frame so it composes with the mouse-look transform that
/// FirstPersonController writes absolutely in Update, instead of accumulating drift.
/// </summary>
public sealed class EngineVibration : MonoBehaviour
{
    [SerializeField] private Transform targetCamera;
    [SerializeField] private float positionalAmplitude = 0.008f;
    [SerializeField] private float rotationalAmplitude = 0.28f;
    [SerializeField] private float frequency = 7.5f;
    [SerializeField] private bool vibrating = true;

    private Vector3 basePosition;
    private Quaternion baseRotation = Quaternion.identity;
    private Vector3 lastAppliedPosition;
    private Quaternion lastAppliedRotation = Quaternion.identity;
    private bool hasBaseline;
    private float noiseSeed;

    public bool IsVibrating => vibrating;

    private void Awake()
    {
        if (targetCamera == null)
        {
            Camera childCamera = GetComponentInChildren<Camera>();
            if (childCamera != null) targetCamera = childCamera.transform;
        }

        noiseSeed = Random.Range(0f, 1000f);
    }

    private void OnEnable()
    {
        hasBaseline = false;
    }

    private void LateUpdate()
    {
        if (targetCamera == null) return;

        CaptureBaseline();

        if (!vibrating)
        {
            targetCamera.SetLocalPositionAndRotation(basePosition, baseRotation);
            lastAppliedPosition = basePosition;
            lastAppliedRotation = baseRotation;
            return;
        }

        float time = Time.time * frequency + noiseSeed;
        float x = (Mathf.PerlinNoise(time, 0.17f) - 0.5f) * 2f;
        float y = (Mathf.PerlinNoise(0.43f, time) - 0.5f) * 2f;
        float z = (Mathf.PerlinNoise(time, 0.71f) - 0.5f) * 2f;

        Vector3 position = basePosition + new Vector3(x, y, z) * positionalAmplitude;
        Quaternion rotation = baseRotation * Quaternion.Euler(
            y * rotationalAmplitude,
            x * rotationalAmplitude * 0.8f,
            z * rotationalAmplitude * 0.55f);

        targetCamera.SetLocalPositionAndRotation(position, rotation);
        lastAppliedPosition = position;
        lastAppliedRotation = rotation;
    }

    /// <summary>
    /// Treats the camera's current local transform as the new baseline unless it is still
    /// exactly what this component wrote last frame (meaning nothing else moved the camera).
    /// </summary>
    private void CaptureBaseline()
    {
        if (!hasBaseline)
        {
            targetCamera.GetLocalPositionAndRotation(out basePosition, out baseRotation);
            lastAppliedPosition = basePosition;
            lastAppliedRotation = baseRotation;
            hasBaseline = true;
            return;
        }

        targetCamera.GetLocalPositionAndRotation(out Vector3 currentPosition, out Quaternion currentRotation);

        if ((currentPosition - lastAppliedPosition).sqrMagnitude > 1e-10f)
        {
            basePosition = currentPosition;
        }

        if (Quaternion.Angle(currentRotation, lastAppliedRotation) > 0.0001f)
        {
            baseRotation = currentRotation;
        }
    }

    public void SetVibrating(bool shouldVibrate)
    {
        vibrating = shouldVibrate;
    }

    private void OnDisable()
    {
        if (targetCamera != null && hasBaseline)
        {
            targetCamera.SetLocalPositionAndRotation(basePosition, baseRotation);
        }
    }
}
