using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the storm-night sky: the procedural skybox, a textured moon, a twinkling starfield, and
/// the sky-wide flash that <see cref="LightningEffect"/> drives.
///
/// Every texture and mesh here is generated in code, so the project carries no sky art. The moon
/// gets a real surface texture with maria and craters, and the stars are soft radial points, not
/// bare quads. All generated objects are marked DontSave so nothing untextured is ever serialised
/// into the scene.
/// </summary>
[ExecuteAlways]
public sealed class NightSkyController : MonoBehaviour
{
    [Header("Skybox")]
    [SerializeField] private Material skyboxMaterial;
    [SerializeField] private Color nightSkyTint = new Color(0.0196f, 0.0314f, 0.0667f);     // #050811
    [SerializeField] private Color nightGroundColor = new Color(0.0039f, 0.0078f, 0.0196f); // #010205
    [SerializeField] private float nightExposure = 0.42f;
    [SerializeField] private float atmosphereThickness = 0.62f;

    [Header("Night Ambience")]
    [SerializeField] private Color nightAmbientLight = new Color(0.039f, 0.055f, 0.102f);
    [SerializeField] private Color nightFogColor = new Color(0.039f, 0.055f, 0.102f);

    [Header("Lightning Flash")]
    [SerializeField] private Color flashSkyTint = new Color(0.753f, 0.847f, 1f);  // #C0D8FF
    [SerializeField] private Color flashAmbientLight = new Color(0.753f, 0.847f, 1f);
    [SerializeField] private float flashExposure = 2.6f;
    [SerializeField, Range(0f, 1f)] private float flashFogBlend = 0.85f;

    [Header("Moon")]
    [SerializeField] private Light moonLight;
    [SerializeField] private float moonDistance = 62f;
    [SerializeField] private float moonSize = 9f;
    [SerializeField] private int moonTextureSize = 512;
    [SerializeField] private Color moonColor = new Color(0.82f, 0.87f, 1f, 1f);
    [SerializeField, Range(0f, 1f)] private float moonGlowStrength = 0.42f;
    [SerializeField] private int moonRandomSeed = 611;

    [Header("Starfield")]
    [SerializeField] private bool generateStars = true;
    [SerializeField] private int starCount = 900;
    [SerializeField] private float starDistance = 68f;
    [SerializeField] private Vector2 starSizeRange = new Vector2(0.22f, 0.95f);
    [SerializeField, Range(0f, 1f)] private float starMaximumAlpha = 0.62f;
    [SerializeField] private float horizonFadeStart = 0.05f;
    [SerializeField] private float horizonFadeEnd = 0.55f;
    [SerializeField] private int starRandomSeed = 1994;

    [Header("Twinkle")]
    [SerializeField] private bool twinkle = true;
    [SerializeField, Range(0f, 1f)] private float twinkleDepth = 0.55f;
    [SerializeField] private Vector2 twinkleSpeedRange = new Vector2(0.35f, 1.6f);
    [SerializeField] private float twinkleUpdatesPerSecond = 18f;

    private Material runtimeSkybox;
    private Material previousSkybox;

    private GameObject skyRoot;
    private Transform moonTransform;
    private Mesh starfieldMesh;
    private Material starfieldMaterial;
    private Material moonMaterial;
    private Texture2D moonTexture;
    private Texture2D starTexture;

    private readonly List<Color32> starColors = new List<Color32>();
    private float[] starBaseAlpha;
    private float[] starTwinklePhase;
    private float[] starTwinkleSpeed;
    private Color32[] starColorBuffer;
    private float nextTwinkleTime;
    private float currentFlash;

    /// <summary>Current flash blend, 0 for a black storm night and 1 at the peak of a strike.</summary>
    public float FlashIntensity => currentFlash;

    private void Awake()
    {
        previousSkybox = RenderSettings.skybox;
        BuildRuntimeSkybox();
        BuildSky();
        SetFlashIntensity(0f);
    }

    private void OnDestroy()
    {
        RestoreSkybox();
        TearDownSky();
    }

    private void LateUpdate()
    {
        AimMoonAtCamera();
        UpdateTwinkle();
    }

    // ----------------------------------------------------------------------------------------
    // Skybox
    // ----------------------------------------------------------------------------------------

    private void BuildRuntimeSkybox()
    {
        if (skyboxMaterial == null)
        {
            Shader proceduralSky = Shader.Find("Skybox/Procedural");
            if (proceduralSky == null)
            {
                return;
            }

            skyboxMaterial = new Material(proceduralSky) { name = "Procedural Night Sky" };
        }

        if (!Application.isPlaying)
        {
            // In edit mode the saved asset is used directly. Instancing here would leave the
            // scene pointing at a material that disappears on the next domain reload.
            RenderSettings.skybox = skyboxMaterial;
            return;
        }

        // An instance, so flashing never dirties the material asset on disk.
        runtimeSkybox = new Material(skyboxMaterial) { name = "Night Sky (Runtime)" };

        if (runtimeSkybox.HasProperty("_GroundColor")) runtimeSkybox.SetColor("_GroundColor", nightGroundColor);
        if (runtimeSkybox.HasProperty("_AtmosphereThickness")) runtimeSkybox.SetFloat("_AtmosphereThickness", atmosphereThickness);
        if (runtimeSkybox.HasProperty("_SunDisk")) runtimeSkybox.SetFloat("_SunDisk", 0f);
        if (runtimeSkybox.HasProperty("_SunSize")) runtimeSkybox.SetFloat("_SunSize", 0f);

        RenderSettings.skybox = runtimeSkybox;
    }

    /// <summary>
    /// Blends the whole sky between storm night and the icy white-blue of a strike.
    /// Called every frame of a flash by <see cref="LightningEffect"/>.
    /// </summary>
    public void SetFlashIntensity(float intensity)
    {
        currentFlash = Mathf.Clamp01(intensity);

        if (runtimeSkybox != null)
        {
            if (runtimeSkybox.HasProperty("_SkyTint"))
            {
                runtimeSkybox.SetColor("_SkyTint", Color.Lerp(nightSkyTint, flashSkyTint, currentFlash));
            }

            if (runtimeSkybox.HasProperty("_Exposure"))
            {
                runtimeSkybox.SetFloat("_Exposure", Mathf.Lerp(nightExposure, flashExposure, currentFlash));
            }
        }

        // Ambient and fog move with the sky, so the ground and the air light up together.
        RenderSettings.ambientLight = Color.Lerp(nightAmbientLight, flashAmbientLight, currentFlash);
        RenderSettings.fogColor = Color.Lerp(nightFogColor, flashSkyTint, currentFlash * flashFogBlend);

        if (starfieldMaterial != null)
        {
            // Stars wash out in the glare.
            Color starTint = Color.white;
            starTint.a = Mathf.Lerp(1f, 0.12f, currentFlash);
            starfieldMaterial.color = starTint;
        }

        if (moonMaterial != null)
        {
            // The moon goes flat and pale as the sky behind it blows out.
            moonMaterial.color = Color.Lerp(moonColor, flashSkyTint, currentFlash * 0.7f);
        }
    }

    private void RestoreSkybox()
    {
        if (runtimeSkybox == null)
        {
            return;
        }

        if (RenderSettings.skybox == runtimeSkybox)
        {
            RenderSettings.skybox = previousSkybox;
        }

        DestroyGenerated(runtimeSkybox);
        runtimeSkybox = null;
    }

    // ----------------------------------------------------------------------------------------
    // Sky contents
    // ----------------------------------------------------------------------------------------

    private void BuildSky()
    {
        TearDownSky();

        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader == null)
        {
            return;
        }

        skyRoot = new GameObject("Sky Contents")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        skyRoot.transform.SetParent(transform, false);

        BuildMoon(spriteShader);

        if (generateStars)
        {
            BuildStarfield(spriteShader);
        }
    }

    private void TearDownSky()
    {
        if (skyRoot != null)
        {
            DestroyGenerated(skyRoot);
            skyRoot = null;
        }

        moonTransform = null;

        DestroyGenerated(starfieldMesh);
        DestroyGenerated(starfieldMaterial);
        DestroyGenerated(moonMaterial);
        DestroyGenerated(moonTexture);
        DestroyGenerated(starTexture);

        starfieldMesh = null;
        starfieldMaterial = null;
        moonMaterial = null;
        moonTexture = null;
        starTexture = null;
        starBaseAlpha = null;
        starTwinklePhase = null;
        starTwinkleSpeed = null;
        starColorBuffer = null;
        starColors.Clear();
    }

    /// <summary>A textured billboard quad rather than a lit sphere, which only ever reads as a disc.</summary>
    private void BuildMoon(Shader spriteShader)
    {
        moonTexture = GenerateMoonTexture(Mathf.Max(64, moonTextureSize), moonRandomSeed, moonGlowStrength);

        moonMaterial = new Material(spriteShader)
        {
            name = "Procedural Moon Material",
            hideFlags = HideFlags.HideAndDontSave,
            mainTexture = moonTexture,
            color = moonColor
        };
        moonMaterial.renderQueue = 2900;

        GameObject moon = new GameObject("Moon")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        moon.transform.SetParent(skyRoot.transform, false);

        MeshFilter meshFilter = moon.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = CreateQuadMesh(moonSize);

        MeshRenderer meshRenderer = moon.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = moonMaterial;
        ConfigureSkyRenderer(meshRenderer);

        moonTransform = moon.transform;
        PositionMoon();
    }

    private Mesh CreateQuadMesh(float size)
    {
        float half = size * 0.5f;

        Mesh mesh = new Mesh
        {
            name = "Procedural Moon Quad",
            hideFlags = HideFlags.HideAndDontSave,
            vertices = new[]
            {
                new Vector3(-half, -half, 0f),
                new Vector3(-half, half, 0f),
                new Vector3(half, half, 0f),
                new Vector3(half, -half, 0f)
            },
            uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f)
            },
            triangles = new[] { 0, 1, 2, 0, 2, 3 }
        };

        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>Places the moon opposite the moonlight, so its glow matches the light direction.</summary>
    public void PositionMoon()
    {
        if (moonTransform == null)
        {
            return;
        }

        if (moonLight == null)
        {
            moonLight = FindMoonLight();
        }

        Vector3 towardsMoon = moonLight != null
            ? -moonLight.transform.forward
            : Quaternion.Euler(48f, -35f, 0f) * Vector3.back;

        moonTransform.position = transform.position + towardsMoon * moonDistance;
    }

    /// <summary>Keeps the moon quad square-on to the camera so it never shows its edge.</summary>
    private void AimMoonAtCamera()
    {
        if (moonTransform == null)
        {
            return;
        }

        Camera camera = Application.isPlaying ? Camera.main : Camera.current;
        Vector3 viewer = camera != null ? camera.transform.position : transform.position;

        Vector3 toViewer = viewer - moonTransform.position;
        if (toViewer.sqrMagnitude > 0.0001f)
        {
            moonTransform.rotation = Quaternion.LookRotation(-toViewer.normalized, Vector3.up);
        }
    }

    private Light FindMoonLight()
    {
        Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Include);
        Light best = null;

        foreach (Light light in lights)
        {
            if (light.type != LightType.Directional)
            {
                continue;
            }

            // The moon is the dim, permanently enabled directional; the lightning one is brighter
            // and normally disabled.
            if (best == null || (light.enabled && light.intensity < best.intensity))
            {
                best = light;
            }
        }

        return best;
    }

    /// <summary>
    /// One mesh of camera-facing quads, each mapped to the soft radial star sprite. Per-star
    /// brightness rides in the vertex colours, which is also what the twinkle animates.
    /// </summary>
    private void BuildStarfield(Shader spriteShader)
    {
        starTexture = GenerateStarSprite(64);

        starfieldMaterial = new Material(spriteShader)
        {
            name = "Procedural Starfield Material",
            hideFlags = HideFlags.HideAndDontSave,
            mainTexture = starTexture,
            color = Color.white
        };
        starfieldMaterial.renderQueue = 2900;

        int count = Mathf.Max(0, starCount);
        List<Vector3> vertices = new List<Vector3>(count * 4);
        List<Vector2> uvs = new List<Vector2>(count * 4);
        List<int> triangles = new List<int>(count * 6);
        starColors.Clear();

        List<float> baseAlpha = new List<float>(count);
        List<float> phases = new List<float>(count);
        List<float> speeds = new List<float>(count);

        Random.State previousState = Random.state;
        Random.InitState(starRandomSeed);

        int attempts = 0;
        while (baseAlpha.Count < count && attempts < count * 12)
        {
            attempts++;

            Vector3 direction = Random.onUnitSphere;
            if (direction.y < horizonFadeStart)
            {
                continue;
            }

            Vector3 center = direction * starDistance;
            Vector3 forward = -direction;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right.sqrMagnitude < 0.001f)
            {
                right = Vector3.right;
            }

            Vector3 up = Vector3.Cross(forward, right).normalized;

            // Mostly faint specks with a handful of brighter ones, rather than a uniform field.
            float sizeRoll = Mathf.Pow(Random.value, 2.4f);
            float size = Mathf.Lerp(
                Mathf.Min(starSizeRange.x, starSizeRange.y),
                Mathf.Max(starSizeRange.x, starSizeRange.y),
                sizeRoll);

            Vector3 halfRight = right * (size * 0.5f);
            Vector3 halfUp = up * (size * 0.5f);

            int vertexIndex = vertices.Count;
            vertices.Add(center - halfRight - halfUp);
            vertices.Add(center - halfRight + halfUp);
            vertices.Add(center + halfRight + halfUp);
            vertices.Add(center + halfRight - halfUp);

            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(1f, 0f));

            triangles.Add(vertexIndex + 0);
            triangles.Add(vertexIndex + 1);
            triangles.Add(vertexIndex + 2);
            triangles.Add(vertexIndex + 0);
            triangles.Add(vertexIndex + 2);
            triangles.Add(vertexIndex + 3);

            // Stars thin out into the storm cloud sitting on the horizon.
            float horizonFade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(horizonFadeStart, horizonFadeEnd, direction.y));
            float brightness = Mathf.Lerp(0.25f, 1f, sizeRoll) * horizonFade * starMaximumAlpha;

            // A faint blue-white spread, so the field is not one uniform colour.
            Color starColor = Color.Lerp(
                new Color(0.75f, 0.82f, 1f),
                new Color(1f, 0.96f, 0.9f),
                Random.value);
            starColor.a = brightness;

            for (int i = 0; i < 4; i++)
            {
                starColors.Add(starColor);
            }

            baseAlpha.Add(brightness);
            phases.Add(Random.Range(0f, Mathf.PI * 2f));
            speeds.Add(Random.Range(
                Mathf.Min(twinkleSpeedRange.x, twinkleSpeedRange.y),
                Mathf.Max(twinkleSpeedRange.x, twinkleSpeedRange.y)));
        }

        Random.state = previousState;

        starBaseAlpha = baseAlpha.ToArray();
        starTwinklePhase = phases.ToArray();
        starTwinkleSpeed = speeds.ToArray();
        starColorBuffer = starColors.ToArray();

        starfieldMesh = new Mesh
        {
            name = "Procedural Starfield",
            hideFlags = HideFlags.HideAndDontSave
        };

        if (vertices.Count > 65000)
        {
            starfieldMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        starfieldMesh.SetVertices(vertices);
        starfieldMesh.SetUVs(0, uvs);
        starfieldMesh.SetColors(starColors);
        starfieldMesh.SetTriangles(triangles, 0);
        starfieldMesh.RecalculateBounds();

        GameObject starfieldObject = new GameObject("Starfield")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        starfieldObject.transform.SetParent(skyRoot.transform, false);

        MeshFilter meshFilter = starfieldObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = starfieldMesh;

        MeshRenderer meshRenderer = starfieldObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = starfieldMaterial;
        ConfigureSkyRenderer(meshRenderer);
    }

    /// <summary>
    /// Breathes the star alphas. Refreshed on an interval rather than every frame: the field is
    /// meant to shimmer slowly, and rewriting 3600 vertex colours per frame buys nothing.
    /// </summary>
    private void UpdateTwinkle()
    {
        if (!twinkle || starfieldMesh == null || starBaseAlpha == null || starBaseAlpha.Length == 0)
        {
            return;
        }

        if (Time.unscaledTime < nextTwinkleTime)
        {
            return;
        }

        nextTwinkleTime = Time.unscaledTime + 1f / Mathf.Max(1f, twinkleUpdatesPerSecond);

        float time = Time.unscaledTime;
        for (int i = 0; i < starBaseAlpha.Length; i++)
        {
            float wave = Mathf.Sin(time * starTwinkleSpeed[i] + starTwinklePhase[i]);
            float alpha = starBaseAlpha[i] * (1f - twinkleDepth * 0.5f * (1f - wave));

            byte encoded = (byte)(Mathf.Clamp01(alpha) * 255f);
            int vertexIndex = i * 4;
            for (int corner = 0; corner < 4; corner++)
            {
                Color32 color = starColorBuffer[vertexIndex + corner];
                color.a = encoded;
                starColorBuffer[vertexIndex + corner] = color;
            }
        }

        starfieldMesh.colors32 = starColorBuffer;
    }

    // ----------------------------------------------------------------------------------------
    // Texture generation
    // ----------------------------------------------------------------------------------------

    /// <summary>
    /// Draws the moon: a lit sphere shading term, dark maria blotches, scattered craters with a
    /// lit rim and shadowed floor, and a soft glow bleeding past the limb.
    /// </summary>
    private static Texture2D GenerateMoonTexture(int size, int seed, float glowStrength)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
        {
            name = "Procedural Moon",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 4,
            hideFlags = HideFlags.HideAndDontSave
        };

        System.Random random = new System.Random(seed);
        Color[] pixels = new Color[size * size];

        // Light arrives from the upper left, which is where the crater shadows fall.
        Vector2 lightDirection = new Vector2(-0.55f, 0.6f).normalized;

        // Craters: a few large, many small.
        int craterCount = 90;
        Vector3[] craters = new Vector3[craterCount]; // x, y, radius in UV space.
        float[] craterDepth = new float[craterCount];
        for (int i = 0; i < craterCount; i++)
        {
            float radius = Mathf.Lerp(0.012f, 0.11f, Mathf.Pow((float)random.NextDouble(), 2.6f));
            craters[i] = new Vector3(
                (float)random.NextDouble(),
                (float)random.NextDouble(),
                radius);
            craterDepth[i] = Mathf.Lerp(0.25f, 1f, (float)random.NextDouble());
        }

        // Maria: large dark seas, built from a handful of soft overlapping blobs.
        int mariaCount = 7;
        Vector3[] maria = new Vector3[mariaCount];
        for (int i = 0; i < mariaCount; i++)
        {
            maria[i] = new Vector3(
                0.25f + (float)random.NextDouble() * 0.5f,
                0.25f + (float)random.NextDouble() * 0.5f,
                Mathf.Lerp(0.1f, 0.28f, (float)random.NextDouble()));
        }

        float noiseOffsetX = (float)random.NextDouble() * 100f;
        float noiseOffsetY = (float)random.NextDouble() * 100f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 uv = new Vector2(x / (float)(size - 1), y / (float)(size - 1));
                Vector2 centered = (uv - new Vector2(0.5f, 0.5f)) * 2f;
                float radius = centered.magnitude;

                // Outside the limb: nothing but the glow halo.
                if (radius >= 1f)
                {
                    float glow = Mathf.Pow(Mathf.Clamp01(1f - Mathf.InverseLerp(1f, 1.9f, radius)), 2.6f);
                    pixels[y * size + x] = new Color(0.62f, 0.72f, 1f, glow * glowStrength * 0.5f);
                    continue;
                }

                // Sphere normal, for the terminator shading that makes it read as a ball.
                float z = Mathf.Sqrt(Mathf.Max(0f, 1f - radius * radius));
                float lambert = Mathf.Clamp01(Vector2.Dot(centered.normalized * radius, lightDirection) * 0.5f + 0.62f);
                lambert = Mathf.Lerp(lambert, 1f, z * 0.35f);

                // Base regolith, mottled with two octaves of noise.
                float grain =
                    Mathf.PerlinNoise(noiseOffsetX + uv.x * 9f, noiseOffsetY + uv.y * 9f) * 0.6f +
                    Mathf.PerlinNoise(noiseOffsetX + uv.x * 27f, noiseOffsetY + uv.y * 27f) * 0.4f;
                float value = Mathf.Lerp(0.72f, 0.93f, grain);

                // Maria darken broad regions.
                for (int i = 0; i < mariaCount; i++)
                {
                    float distance = Vector2.Distance(uv, new Vector2(maria[i].x, maria[i].y));
                    float blend = 1f - Mathf.Clamp01(distance / maria[i].z);
                    value -= Mathf.SmoothStep(0f, 1f, blend) * 0.22f;
                }

                // Craters: shadowed floor, bright rim on the lit side.
                for (int i = 0; i < craterCount; i++)
                {
                    Vector2 craterCenter = new Vector2(craters[i].x, craters[i].y);
                    float craterRadius = craters[i].z;
                    float distance = Vector2.Distance(uv, craterCenter);
                    if (distance > craterRadius)
                    {
                        continue;
                    }

                    float normalized = distance / craterRadius;
                    Vector2 offset = (uv - craterCenter).normalized;
                    float facing = Vector2.Dot(offset, lightDirection);

                    // Rim highlight in the outer fifth, bowl shading inside it.
                    if (normalized > 0.78f)
                    {
                        float rim = Mathf.InverseLerp(0.78f, 1f, normalized);
                        value += facing * 0.16f * craterDepth[i] * Mathf.Sin(rim * Mathf.PI);
                    }
                    else
                    {
                        float bowl = 1f - normalized / 0.78f;
                        value -= facing * 0.12f * craterDepth[i] * bowl;
                        value -= bowl * 0.06f * craterDepth[i];
                    }
                }

                value = Mathf.Clamp01(value * lambert);

                // Soft alpha at the limb, so the disc has no jagged edge.
                float edgeAlpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 0.94f, radius));

                pixels[y * size + x] = new Color(
                    value * 0.97f,
                    value * 0.98f,
                    value,
                    edgeAlpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(true);
        return texture;
    }

    /// <summary>A soft radial point: bright core falling smoothly to fully transparent edges.</summary>
    private static Texture2D GenerateStarSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
        {
            name = "Procedural Star Point",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Trilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(0.5f, 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 uv = new Vector2(x / (float)(size - 1), y / (float)(size - 1));
                float distance = Vector2.Distance(uv, center) * 2f;

                // A tight core over a wide, gentle halo reads as a point of light rather than a dot.
                float core = Mathf.Pow(Mathf.Clamp01(1f - distance / 0.42f), 2.2f);
                float halo = Mathf.Pow(Mathf.Clamp01(1f - distance), 3.4f) * 0.55f;
                float alpha = Mathf.Clamp01(core + halo);

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(true);
        return texture;
    }

    private static void ConfigureSkyRenderer(MeshRenderer meshRenderer)
    {
        if (meshRenderer == null)
        {
            return;
        }

        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    private static void DestroyGenerated(Object generated)
    {
        if (generated == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(generated);
        }
        else
        {
            DestroyImmediate(generated);
        }
    }

    public static NightSkyController Find()
    {
        return FindAnyObjectByType<NightSkyController>();
    }
}
