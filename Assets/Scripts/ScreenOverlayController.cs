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
    private Image jumpscareImage;
    private Text messageText;
    private Text subtitleText;
    private float jumpscareTimeRemaining;

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
        jumpscareImage = CreateLayer("Jumpscare", CreateMascotFaceSprite(), Color.white);
        jumpscareImage.preserveAspect = true;

        messageText = CreateText("Message Title", 96, TextAnchor.MiddleCenter, new Vector2(0f, 40f));
        subtitleText = CreateText("Message Subtitle", 38, TextAnchor.MiddleCenter, new Vector2(0f, -60f));
    }

    private Text CreateText(string layerName, int fontSize, TextAnchor anchor, Vector2 offset)
    {
        GameObject textObject = new GameObject(layerName);
        textObject.transform.SetParent(canvas.transform, false);

        RectTransform rectTransform = textObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(1600f, 260f);
        rectTransform.anchoredPosition = offset;

        Text text = textObject.AddComponent<Text>();

        // The built-in legacy font ships with the engine, so no font asset has to be imported.
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = anchor;
        text.raycastTarget = false;
        text.color = new Color(0.92f, 0.9f, 0.86f, 0f);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private void Update()
    {
        if (jumpscareTimeRemaining <= 0f)
        {
            return;
        }

        jumpscareTimeRemaining -= Time.unscaledDeltaTime;
        if (jumpscareTimeRemaining <= 0f)
        {
            ApplyAlpha(jumpscareImage, Color.white, 0f);
        }
    }

    /// <summary>Slams the mascot face over the whole screen for a fraction of a second.</summary>
    public void ShowJumpscare(float duration)
    {
        jumpscareTimeRemaining = Mathf.Max(0.05f, duration);
        ApplyAlpha(jumpscareImage, Color.white, 1f);
    }

    /// <summary>Shows the end-of-run message. Pass null to clear it.</summary>
    public void ShowMessage(string title, string subtitle, Color color)
    {
        if (messageText != null)
        {
            messageText.text = title ?? string.Empty;
            messageText.color = new Color(color.r, color.g, color.b, string.IsNullOrEmpty(title) ? 0f : 1f);
        }

        if (subtitleText != null)
        {
            subtitleText.text = subtitle ?? string.Empty;
            subtitleText.color = new Color(color.r, color.g, color.b, string.IsNullOrEmpty(subtitle) ? 0f : 0.8f);
        }
    }

    public void ClearMessage()
    {
        ShowMessage(null, null, Color.white);
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

    /// <summary>
    /// Draws the distorted mascot face used for the kill sting: a pale mask on black with
    /// blown-out eyes and a jagged grin. Generated so no texture has to be imported.
    /// </summary>
    private static Sprite CreateMascotFaceSprite()
    {
        const int size = 512;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Procedural Mascot Face",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[size * size];
        System.Random random = new System.Random(1994);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 uv = new Vector2(x / (float)(size - 1), y / (float)(size - 1));
                Vector2 centered = (uv - new Vector2(0.5f, 0.5f)) * 2f;

                // Head: a slightly squashed ellipse of dirty felt.
                float head = new Vector2(centered.x / 0.78f, centered.y / 0.92f).magnitude;
                float value = head < 1f ? Mathf.Lerp(0.42f, 0.16f, head) : 0f;

                // Two hollow, over-bright eyes.
                float leftEye = new Vector2((centered.x + 0.32f) / 0.2f, (centered.y - 0.24f) / 0.26f).magnitude;
                float rightEye = new Vector2((centered.x - 0.32f) / 0.2f, (centered.y - 0.24f) / 0.26f).magnitude;
                float eye = Mathf.Min(leftEye, rightEye);
                if (eye < 1f)
                {
                    value = Mathf.Lerp(1f, 0.05f, Mathf.SmoothStep(0f, 1f, eye));
                }

                // A torn, zig-zag mouth.
                float mouthLine = -0.34f + Mathf.Sin(centered.x * 22f) * 0.05f;
                bool inMouth = Mathf.Abs(centered.y - mouthLine) < 0.11f && Mathf.Abs(centered.x) < 0.46f && head < 1f;
                if (inMouth)
                {
                    value = Mathf.Abs(Mathf.Sin(centered.x * 44f)) > 0.45f ? 0.95f : 0.02f;
                }

                float grain = (float)(random.NextDouble() - 0.5) * 0.08f;
                value = Mathf.Clamp01(value + grain);

                float alpha = head < 1.02f ? 1f : 0f;
                pixels[y * size + x] = new Color(value, value * 0.93f, value * 0.82f, alpha);
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
