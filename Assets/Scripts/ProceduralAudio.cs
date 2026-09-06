using UnityEngine;

/// <summary>
/// Shared generators for the placeholder AudioClips used across the sandbox, so scenes stay
/// playable before any real audio is imported. Every generated clip is mono and short.
/// </summary>
public static class ProceduralAudio
{
    private const int SampleRate = 22050;

    /// <summary>Slow, dry hinge creak that rises in pitch as the door swings.</summary>
    public static AudioClip CreateDoorCreak(string clipName = "Procedural Door Creak", float duration = 1.6f, int seed = 3011)
    {
        return Build(clipName, duration, seed, (time, progress, random) =>
        {
            float envelope = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI);
            float pitch = 128f + progress * 190f;
            float grind = Mathf.Sign(Mathf.Sin(time * Mathf.PI * 2f * pitch)) * 0.09f;
            float body = Mathf.Sin(time * Mathf.PI * 2f * pitch * 0.5f) * 0.13f;
            float stutter = Mathf.Pow(Mathf.Abs(Mathf.Sin(time * Mathf.PI * 11f)), 6f);
            float grit = (float)(random.NextDouble() * 2.0 - 1.0) * 0.05f;
            return (grind * stutter + body + grit) * envelope * 0.55f;
        });
    }

    /// <summary>Heavy wooden slam with a short low tail.</summary>
    public static AudioClip CreateDoorSlam(string clipName = "Procedural Door Slam", float duration = 1.1f, int seed = 7788)
    {
        return Build(clipName, duration, seed, (time, progress, random) =>
        {
            float impact = Mathf.Exp(-progress * 26f);
            float tail = Mathf.Exp(-progress * 5.5f);
            float thud = Mathf.Sin(time * Mathf.PI * 2f * 68f) * 0.55f;
            thud += Mathf.Sin(time * Mathf.PI * 2f * 41f) * 0.35f;
            float crack = (float)(random.NextDouble() * 2.0 - 1.0) * impact * 0.6f;
            return Mathf.Clamp(thud * tail + crack, -1f, 1f) * 0.85f;
        });
    }

    /// <summary>Rattling handle: several dry metallic knocks in quick succession.</summary>
    public static AudioClip CreateHandleJiggle(string clipName = "Procedural Locked Handle", float duration = 0.75f, int seed = 5150)
    {
        return Build(clipName, duration, seed, (time, progress, random) =>
        {
            float knockPhase = Mathf.Repeat(time * 7.5f, 1f);
            float knock = Mathf.Exp(-knockPhase * 34f);
            float metal = Mathf.Sin(time * Mathf.PI * 2f * 1450f) * 0.4f;
            metal += Mathf.Sin(time * Mathf.PI * 2f * 880f) * 0.3f;
            float clatter = (float)(random.NextDouble() * 2.0 - 1.0) * 0.35f;
            float envelope = 1f - Mathf.Clamp01(progress) * 0.35f;
            return (metal + clatter) * knock * envelope * 0.5f;
        });
    }

    /// <summary>Small bright two-tone chime for picking an item up.</summary>
    public static AudioClip CreatePickupChime(string clipName = "Procedural Pickup Chime", float duration = 0.9f, int seed = 1994)
    {
        return Build(clipName, duration, seed, (time, progress, random) =>
        {
            float envelope = Mathf.Exp(-progress * 6.5f);
            float first = Mathf.Sin(time * Mathf.PI * 2f * 784f) * 0.5f;
            float second = time > 0.09f ? Mathf.Sin((time - 0.09f) * Mathf.PI * 2f * 1046f) * 0.42f : 0f;
            float shimmer = Mathf.Sin(time * Mathf.PI * 2f * 2093f) * 0.08f;
            return (first + second + shimmer) * envelope * 0.42f;
        });
    }

    /// <summary>Dull click for a lock releasing.</summary>
    public static AudioClip CreateUnlockClick(string clipName = "Procedural Unlock Click", float duration = 0.45f, int seed = 6402)
    {
        return Build(clipName, duration, seed, (time, progress, random) =>
        {
            float envelope = Mathf.Exp(-progress * 17f);
            float click = Mathf.Sin(time * Mathf.PI * 2f * 640f) * 0.45f;
            float mechanism = (float)(random.NextDouble() * 2.0 - 1.0) * 0.28f;
            return (click + mechanism) * envelope * 0.6f;
        });
    }

    private delegate float SampleGenerator(float time, float progress, System.Random random);

    private static AudioClip Build(string clipName, float duration, int seed, SampleGenerator generator)
    {
        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(duration * SampleRate));
        float[] samples = new float[sampleCount];
        System.Random random = new System.Random(seed);

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)SampleRate;
            samples[i] = Mathf.Clamp(generator(time, time / duration, random), -1f, 1f);
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
