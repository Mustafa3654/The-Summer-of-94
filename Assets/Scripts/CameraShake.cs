using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The single writer for additive camera shake. Other systems (engine idle, low sanity,
/// heartbeat) register a named source and this component composes them all in LateUpdate.
///
/// The baseline is re-read every frame rather than stored once, because
/// <see cref="FirstPersonController"/> writes the camera local rotation absolutely in Update.
/// Anything this component wrote last frame is recognised and discarded, so offsets never
/// accumulate into drift.
/// </summary>
public sealed class CameraShake : MonoBehaviour
{
    private struct ShakeSource
    {
        public float PositionalAmplitude;
        public float RotationalAmplitude;
        public float Frequency;
        public float Seed;
    }

    [SerializeField] private Transform targetCamera;

    private readonly Dictionary<string, ShakeSource> sources = new Dictionary<string, ShakeSource>();
    private Vector3 basePosition;
    private Quaternion baseRotation = Quaternion.identity;
    private Vector3 lastAppliedPosition;
    private Quaternion lastAppliedRotation = Quaternion.identity;
    private bool hasBaseline;

    private void Awake()
    {
        if (targetCamera == null)
        {
            Camera childCamera = GetComponentInChildren<Camera>();
            targetCamera = childCamera != null ? childCamera.transform : transform;
        }
    }

    private void OnEnable()
    {
        hasBaseline = false;
    }

    /// <summary>Adds or updates a persistent shake source. Amplitudes of zero are idle.</summary>
    public void SetSource(string sourceId, float positionalAmplitude, float rotationalAmplitude, float frequency)
    {
        if (string.IsNullOrEmpty(sourceId))
        {
            return;
        }

        if (!sources.TryGetValue(sourceId, out ShakeSource source))
        {
            source.Seed = Random.Range(0f, 1000f);
        }

        source.PositionalAmplitude = positionalAmplitude;
        source.RotationalAmplitude = rotationalAmplitude;
        source.Frequency = Mathf.Max(0.01f, frequency);
        sources[sourceId] = source;
    }

    public void ClearSource(string sourceId)
    {
        if (!string.IsNullOrEmpty(sourceId))
        {
            sources.Remove(sourceId);
        }
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
        {
            return;
        }

        CaptureBaseline();

        Vector3 positionOffset = Vector3.zero;
        Vector3 rotationOffset = Vector3.zero;

        foreach (KeyValuePair<string, ShakeSource> entry in sources)
        {
            ShakeSource source = entry.Value;
            if (source.PositionalAmplitude <= 0f && source.RotationalAmplitude <= 0f)
            {
                continue;
            }

            float time = Time.time * source.Frequency + source.Seed;
            float x = (Mathf.PerlinNoise(time, 0.17f) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(0.43f, time) - 0.5f) * 2f;
            float z = (Mathf.PerlinNoise(time, 0.71f) - 0.5f) * 2f;

            positionOffset += new Vector3(x, y, z) * source.PositionalAmplitude;
            rotationOffset += new Vector3(
                y * source.RotationalAmplitude,
                x * source.RotationalAmplitude * 0.8f,
                z * source.RotationalAmplitude * 0.55f);
        }

        Vector3 finalPosition = basePosition + positionOffset;
        Quaternion finalRotation = baseRotation * Quaternion.Euler(rotationOffset);

        targetCamera.SetLocalPositionAndRotation(finalPosition, finalRotation);
        lastAppliedPosition = finalPosition;
        lastAppliedRotation = finalRotation;
    }

    /// <summary>
    /// Treats the camera local transform as a new baseline unless it is exactly what this
    /// component wrote last frame, which means no other system moved it.
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

    private void OnDisable()
    {
        if (targetCamera != null && hasBaseline)
        {
            targetCamera.SetLocalPositionAndRotation(basePosition, baseRotation);
        }
    }

    /// <summary>Finds the shaker on the player, adding one if the scene predates this component.</summary>
    public static CameraShake FindOrCreate(Transform playerRoot)
    {
        if (playerRoot == null)
        {
            return null;
        }

        CameraShake shaker = playerRoot.GetComponentInChildren<CameraShake>();
        if (shaker == null)
        {
            shaker = playerRoot.gameObject.AddComponent<CameraShake>();
        }

        return shaker;
    }
}
