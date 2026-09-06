using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the full-screen horror overlays at runtime from generated textures, so the project
/// needs no imported UI sprites and no post-processing volume profile.
///
/// Layers, back to front:
///   Haze     - flat darkening that muddies the view as sanity falls.
///   Vignette - radial gradient that closes in from the screen edges.
///   Eyelids  - solid black used by <see cref="EyeCloseMechanic"/>.
/// </summary>
public sealed class ScreenOverlayController : MonoBehaviour
{
    [SerializeField] private int sortingOrder = 100;
    [SerializeField] private Color vignetteColor = new Color(0f, 0f, 0f, 1f);
    [SerializeField] private Color hazeColor = new Color(0.02f, 0.03f, 0.05f, 1f);

    private Canvas canvas;
    private Image hazeImage;
    private Image vignetteImage;
    private Image eyelidImage;

    private float vignetteIntensity;
    private float hazeIntensity;
    private float blackoutIntensity;

    public float VignetteIntensity => vignetteIntensity;
    public float BlackoutIntensity => blackoutIntensity;

    private void Awake()
    {
        BuildOverlay();
        SetVignette(0f);
        SetHaze(0f);
        SetBlackout(0f);
    }

    private void BuildOverlay()
    {
        GameObject canvasObject = new GameObject("Horror Overlay Canvas");
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // No GraphicRaycaster: these overlays must never swallow input.
        hazeImage = CreateLayer("Sanity Haze", CreateSolidSprite(), hazeColor);
        vignetteImage = CreateLayer("Sanity Vignette", CreateVignetteSprite(), vignetteColor);
        eyelidImage = CreateLayer("Eyelids", CreateSolidSprite(), Color.black);
    }

    private Image CreateLayer(string layerName, Sprite sprite, Color color)
    {
        GameObject layerObject = new GameObject(layerName);
        layerObject.transform.SetParent(canvas.transform, false);

        RectTransform rectTransform = layerObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Image image = layerObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.raycastTarget = false;
        image.color = new Color(color.r, color.g, color.b, 0f);
        return image;
    }

    public void SetVignette(float intensity)
    {
        vignetteIntensity = Mathf.Clamp01(intensity);
        ApplyAlpha(vignetteImage, vignetteColor, vignetteIntensity);
    }

    public void SetHaze(float intensity)
    {
        hazeIntensity = Mathf.Clamp01(intensity);
        ApplyAlpha(hazeImage, hazeColor, hazeIntensity * 0.55f);
    }

    public void SetBlackout(float intensity)
    {
        blackoutIntensity = Mathf.Clamp01(intensity);
        ApplyAlpha(eyelidImage, Color.black, blackoutIntensity);
    }

    private static void ApplyAlpha(Image image, Color baseColor, float alpha)
    {
        if (image != null)
        {
            image.color = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Clamp01(alpha));
        }
    }

    private static Sprite CreateSolidSprite()
    {
        Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, false)
        {
            name = "Procedural Solid Overlay",
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>Radial gradient: transparent in the middle, opaque at the corners.</summary>
    private static Sprite CreateVignetteSprite()
    {
        const int size = 256;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Procedural Vignette",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(0.5f, 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 uv = new Vector2(x / (float)(size - 1), y / (float)(size - 1));
                float distance = Vector2.Distance(uv, center) / 0.7071f;
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, distance));
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    public static ScreenOverlayController Find()
    {
        return FindAnyObjectByType<ScreenOverlayController>();
    }
}
