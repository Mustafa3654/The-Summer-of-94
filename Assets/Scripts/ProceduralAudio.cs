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

    /// <summary>Looping panicked breathing. Volume is driven by the sanity system.</summary>
    public static AudioClip CreateBreathing(string clipName = "Procedural Breathing", float duration = 4.4f, int seed = 2211)
    {
        return Build(clipName, duration, seed, (time, progress, random) =>
        {
            // Two breaths per loop: a sharper inhale followed by a longer exhale.
            float cycle = Mathf.Repeat(time / (duration * 0.5f), 1f);
            float inhale = Mathf.Pow(Mathf.Clamp01(Mathf.Sin(cycle * Mathf.PI * 2f)), 2f);
            float exhale = Mathf.Pow(Mathf.Clamp01(-Mathf.Sin(cycle * Mathf.PI * 2f)), 1.4f);
            float breathEnvelope = inhale * 0.9f + exhale * 0.65f;

            float air = (float)(random.NextDouble() * 2.0 - 1.0);
            float throat = Mathf.Sin(time * Mathf.PI * 2f * 96f) * 0.16f;
            return (air * 0.42f + throat) * breathEnvelope * 0.5f;
        });
    }

    /// <summary>Looping two-thump heartbeat, one beat per loop so playback speed sets the BPM.</summary>
    public static AudioClip CreateHeartbeat(string clipName = "Procedural Heartbeat", float duration = 1f, int seed = 808)
    {
        return Build(clipName, duration, seed, (time, progress, random) =>
        {
            float lub = Thump(time, 0.02f, 54f, 15f);
            float dub = Thump(time, 0.29f, 44f, 17f) * 0.72f;
            return (lub + dub) * 0.9f;
        });
    }

    /// <summary>Short, distant child laugh built from pitched syllables.</summary>
    public static AudioClip CreateChildLaugh(string clipName = "Procedural Child Laugh", float duration = 1.9f, int seed = 4499)
    {
        return Build(clipName, duration, seed, (time, progress, random) =>
        {
            float fade = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI);
            float syllable = Mathf.Pow(Mathf.Abs(Mathf.Sin(time * Mathf.PI * 4.4f)), 3.5f);
            float pitch = 430f + Mathf.Sin(time * 5.5f) * 70f - progress * 60f;
            float voice = Mathf.Sin(time * Mathf.PI * 2f * pitch) * 0.34f;
            voice += Mathf.Sin(time * Mathf.PI * 2f * pitch * 2f) * 0.12f;
            float breath = (float)(random.NextDouble() * 2.0 - 1.0) * 0.06f;
            return (voice * syllable + breath * syllable) * fade * 0.55f;
        });
    }

    /// <summary>Unintelligible whispering: noise shaped into syllables with a faint pitch.</summary>
    public static AudioClip CreateWhisper(string clipName = "Procedural Whisper", float duration = 2.6f, int seed = 1717)
    {
        return Build(clipName, duration, seed, (time, progress, random) =>
        {
            float fade = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI);
            float syllable = Mathf.Pow(Mathf.Abs(Mathf.Sin(time * Mathf.PI * 2.9f)), 2.2f);
            float breathNoise = (float)(random.NextDouble() * 2.0 - 1.0);
            float formant = Mathf.Sin(time * Mathf.PI * 2f * 210f) * 0.09f;
            return (breathNoise * 0.3f + formant) * syllable * fade * 0.6f;
        });
    }

    /// <summary>Single dry click for the radio spirit detector.</summary>
    public static AudioClip CreateGeigerClick(string clipName = "Procedural Detector Click", float duration = 0.08f, int seed = 3333)
    {
        return Build(clipName, duration, seed, (time, progress, random) =>
        {
            float envelope = Mathf.Exp(-progress * 42f);
            float click = (float)(random.NextDouble() * 2.0 - 1.0) * 0.7f;
            float tone = Mathf.Sin(time * Mathf.PI * 2f * 2400f) * 0.3f;
            return (click + tone) * envelope;
        });
    }

    private static float Thump(float time, float startTime, float frequency, float decay)
    {
        if (time < startTime)
        {
            return 0f;
        }

        float local = time - startTime;
        return Mathf.Sin(local * Mathf.PI * 2f * frequency) * Mathf.Exp(-local * decay);
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
