using UnityEngine;

/// <summary>
/// Owns the storm-night sky: the procedural skybox, a cold moon aligned with the moonlight, a
/// generated starfield, and the sky-wide flash that <see cref="LightningEffect"/> drives.
///
/// Everything is generated or tinted at runtime, so no sky texture, cubemap, or star sprite has
/// to be imported. The skybox material is instanced on Awake, which keeps the flash from writing
/// into the saved asset while playing.
/// </summary>
[ExecuteAlways]
public sealed class NightSkyController : MonoBehaviour
{
    [Header("Skybox")]
    [SerializeField] private Material skyboxMaterial;
    [SerializeField] private Color nightSkyTint = new Color(0.0196f, 0.0314f, 0.0667f);   // #050811
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
    [SerializeField] private Transform moonTransform;
    [SerializeField] private float moonDistance = 62f;

    [Header("Starfield")]
    [SerializeField] private bool generateStars = true;
    [SerializeField] private int starCount = 700;
    [SerializeField] private float starDistance = 68f;
    [SerializeField] private float starSize = 0.34f;
    [SerializeField, Range(0f, 1f)] private float starMaximumAlpha = 0.5f;
    [SerializeField] private float minimumStarElevation = 0.12f;
    [SerializeField] private int starRandomSeed = 1994;

    private Material runtimeSkybox;
    private Material previousSkybox;
    private GameObject starfieldObject;
    private Mesh starfieldMesh;
    private Material starfieldMaterial;
    private float currentFlash;

    /// <summary>Current flash blend, 0 for a black storm night and 1 at the peak of a strike.</summary>
    public float FlashIntensity => currentFlash;

    private void Awake()
    {
        previousSkybox = RenderSettings.skybox;
        BuildRuntimeSkybox();
        PositionMoon();

        if (generateStars && Application.isPlaying)
        {
            BuildStarfield();
        }

        SetFlashIntensity(0f);
    }

    private void OnValidate()
    {
        // Keeps the moon lined up with the moonlight while dragging values in the Inspector.
        PositionMoon();
    }

    private void OnDestroy()
    {
        RestoreSkybox();
        DestroyGenerated(starfieldMesh);
        DestroyGenerated(starfieldMaterial);
    }

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
        ApplyStaticSkyboxSettings(runtimeSkybox);
        RenderSettings.skybox = runtimeSkybox;
    }

    private void ApplyStaticSkyboxSettings(Material material)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_GroundColor")) material.SetColor("_GroundColor", nightGroundColor);
        if (material.HasProperty("_AtmosphereThickness")) material.SetFloat("_AtmosphereThickness", atmosphereThickness);

        // No sun disk: the moon is a real object in the scene instead.
        if (material.HasProperty("_SunDisk")) material.SetFloat("_SunDisk", 0f);
        if (material.HasProperty("_SunSize")) material.SetFloat("_SunSize", 0f);
    }

    /// <summary>
    /// Blends the whole sky between storm night and the icy white-blue of a strike.
    /// Called every frame of a flash by <see cref="LightningEffect"/>.
    /// </summary>
    public void SetFlashIntensity(float intensity)
    {
        currentFlash = Mathf.Clamp01(intensity);

        // Only the runtime instance is ever written to, so edit mode never dirties the asset.
        Material sky = runtimeSkybox;
        if (sky != null)
        {
            if (sky.HasProperty("_SkyTint"))
            {
                sky.SetColor("_SkyTint", Color.Lerp(nightSkyTint, flashSkyTint, currentFlash));
            }

            if (sky.HasProperty("_Exposure"))
            {
                sky.SetFloat("_Exposure", Mathf.Lerp(nightExposure, flashExposure, currentFlash));
            }
        }

        // Ambient and fog move with the sky, so the ground and the air light up together.
        RenderSettings.ambientLight = Color.Lerp(nightAmbientLight, flashAmbientLight, currentFlash);
        RenderSettings.fogColor = Color.Lerp(nightFogColor, flashSkyTint, currentFlash * flashFogBlend);

        if (starfieldMaterial != null)
        {
            // Stars wash out in the glare.
            Color starColor = starfieldMaterial.color;
            starColor.a = Mathf.Lerp(1f, 0.15f, currentFlash);
            starfieldMaterial.color = starColor;
        }
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

        if (moonLight == null)
        {
            return;
        }

        Vector3 towardsMoon = -moonLight.transform.forward;
        moonTransform.position = transform.position + towardsMoon * moonDistance;
        moonTransform.rotation = Quaternion.LookRotation(-towardsMoon);
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
    /// Builds the starfield as a single mesh of camera-facing quads on a dome. One mesh and one
    /// draw call, with per-star brightness carried in vertex colours.
    /// </summary>
    private void BuildStarfield()
    {
        if (starfieldObject != null)
        {
            return;
        }

        Shader unlitShader = Shader.Find("Sprites/Default");
        if (unlitShader == null)
        {
            return;
        }

        starfieldObject = new GameObject("Starfield");
        starfieldObject.transform.SetParent(transform, false);

        int count = Mathf.Max(0, starCount);
        Vector3[] vertices = new Vector3[count * 4];
        Color[] colors = new Color[count * 4];
        int[] triangles = new int[count * 6];

        Random.State previousState = Random.state;
        Random.InitState(starRandomSeed);

        int written = 0;
        int attempts = 0;
        while (written < count && attempts < count * 12)
        {
            attempts++;

            Vector3 direction = Random.onUnitSphere;
            if (direction.y < minimumStarElevation)
            {
                continue;
            }

            Vector3 center = direction * starDistance;

            // Orient each quad to face the dome centre.
            Vector3 forward = -direction;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right.sqrMagnitude < 0.001f)
            {
                right = Vector3.right;
            }

            Vector3 up = Vector3.Cross(forward, right).normalized;

            float size = starSize * Random.Range(0.45f, 1.7f);
            Vector3 halfRight = right * (size * 0.5f);
            Vector3 halfUp = up * (size * 0.5f);

            int vertexIndex = written * 4;
            vertices[vertexIndex + 0] = center - halfRight - halfUp;
            vertices[vertexIndex + 1] = center - halfRight + halfUp;
            vertices[vertexIndex + 2] = center + halfRight + halfUp;
            vertices[vertexIndex + 3] = center + halfRight - halfUp;

            // Dimmer near the horizon, where the storm cloud sits.
            float horizonFade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(minimumStarElevation, 0.75f, direction.y));
            float brightness = Random.Range(0.3f, 1f) * horizonFade;
            Color starColor = new Color(0.82f, 0.88f, 1f, brightness * starMaximumAlpha);

            colors[vertexIndex + 0] = starColor;
            colors[vertexIndex + 1] = starColor;
            colors[vertexIndex + 2] = starColor;
            colors[vertexIndex + 3] = starColor;

            int triangleIndex = written * 6;
            triangles[triangleIndex + 0] = vertexIndex + 0;
            triangles[triangleIndex + 1] = vertexIndex + 1;
            triangles[triangleIndex + 2] = vertexIndex + 2;
            triangles[triangleIndex + 3] = vertexIndex + 0;
            triangles[triangleIndex + 4] = vertexIndex + 2;
            triangles[triangleIndex + 5] = vertexIndex + 3;

            written++;
        }

        Random.state = previousState;

        starfieldMesh = new Mesh
        {
            name = "Procedural Starfield",
            hideFlags = HideFlags.HideAndDontSave
        };

        if (written * 4 > 65000)
        {
            starfieldMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        // Trim to what was actually written, since horizon rejections leave gaps.
        System.Array.Resize(ref vertices, written * 4);
        System.Array.Resize(ref colors, written * 4);
        System.Array.Resize(ref triangles, written * 6);

        starfieldMesh.vertices = vertices;
        starfieldMesh.colors = colors;
        starfieldMesh.triangles = triangles;
        starfieldMesh.RecalculateBounds();

        starfieldMaterial = new Material(unlitShader)
        {
            name = "Procedural Starfield Material",
            hideFlags = HideFlags.HideAndDontSave,
            color = Color.white
        };
        starfieldMaterial.renderQueue = 2900; // Just before regular transparents, behind everything else.

        MeshFilter meshFilter = starfieldObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = starfieldMesh;

        MeshRenderer meshRenderer = starfieldObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = starfieldMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
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
