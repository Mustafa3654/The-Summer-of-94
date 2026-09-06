using UnityEngine;

/// <summary>
/// Central channel for sounds the Teacher can hear. Anything that makes noise in the world
/// reports it here, and <see cref="TeacherAI"/> is the only listener that acts on it.
///
/// Loudness is a rough radius in metres: how far away the sound is still worth investigating.
/// </summary>
public static class NoiseEvents
{
    /// <summary>Raised with the world position of the sound and how far it carries.</summary>
    public static event System.Action<Vector3, float> NoiseMade;

    public static void Emit(Vector3 position, float loudness)
    {
        if (loudness > 0f)
        {
            NoiseMade?.Invoke(position, loudness);
        }
    }
}
