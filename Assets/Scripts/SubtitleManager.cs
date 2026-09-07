using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Closed captions for every sound in the game, drawn bottom-centre over a semi-transparent box.
///
/// The whole UI is generated at runtime from the engine's built-in font, so nothing has to be
/// imported. Call it from anywhere through the static <see cref="Caption"/> helper, which is a
/// no-op when no manager exists rather than throwing.
///
/// Two behaviours keep the captions readable rather than a churning wall of text:
///   Dedupe   - repeating the caption already on screen just extends its timer, so recurring
///              sounds such as thunder or footsteps do not flicker.
///   Priority - a louder event holds the line against ambient chatter. A caption is only
///              replaced by one of equal or higher priority, or once it has expired.
/// </summary>
public sealed class SubtitleManager : MonoBehaviour
{
    /// <summary>Rough importance bands used across the game.</summary>
    public static class Priority
    {
        public const int Ambient = 0;      // Rain, thunder, idle engine.
        public const int Interaction = 10; // Doors, pickups, the radio.
        public const int Threat = 20;      // Spirits, footsteps, the heartbeat.
        public const int Critical = 30;    // Slams, the generator, the kill.
    }

    private static SubtitleManager instance;

    [Header("Layout")]
    [SerializeField] private int sortingOrder = 90;
    [SerializeField] private float boxWidth = 1180f;
    [SerializeField] private float bottomMargin = 90f;
    [SerializeField] private int fontSize = 34;
    [SerializeField] private int padding = 18;

    [Header("Style")]
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.62f);
    [SerializeField] private Color textColor = new Color(0.94f, 0.93f, 0.89f, 1f);
    [SerializeField] private float fadeOutSeconds = 0.35f;

    private Canvas canvas;
    private Image backgroundImage;
    private Text captionText;
    private CanvasGroup canvasGroup;

    private string currentText;
    private float timeRemaining;
    private int currentPriority;

    /// <summary>The active manager, or null if the scene has none.</summary>
    public static SubtitleManager Instance => instance;

    public string CurrentText => currentText;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
        BuildUserInterface();
        Clear();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void BuildUserInterface()
    {
        GameObject canvasObject = new GameObject("Subtitle Canvas");
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGroup = canvasObject.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0f;

        // Background box, hugging the caption vertically while keeping a fixed width.
        GameObject boxObject = new GameObject("Caption Box");
        boxObject.transform.SetParent(canvas.transform, false);

        RectTransform boxRect = boxObject.AddComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0f);
        boxRect.anchorMax = new Vector2(0.5f, 0f);
        boxRect.pivot = new Vector2(0.5f, 0f);
        boxRect.anchoredPosition = new Vector2(0f, bottomMargin);
        boxRect.sizeDelta = new Vector2(boxWidth, 0f);

        backgroundImage = boxObject.AddComponent<Image>();
        backgroundImage.color = backgroundColor;
        backgroundImage.raycastTarget = false;
        backgroundImage.sprite = CreateRoundedBoxSprite();
        backgroundImage.type = Image.Type.Sliced;

        VerticalLayoutGroup layout = boxObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(padding, padding, padding, padding);
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        ContentSizeFitter fitter = boxObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject textObject = new GameObject("Caption Text");
        textObject.transform.SetParent(boxObject.transform, false);

        captionText = textObject.AddComponent<Text>();

        // Built into the engine, so no font asset has to be imported.
        captionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        captionText.fontSize = fontSize;
        captionText.alignment = TextAnchor.MiddleCenter;
        captionText.color = textColor;
        captionText.raycastTarget = false;
        captionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        captionText.verticalOverflow = VerticalWrapMode.Overflow;
        captionText.text = string.Empty;
    }

    private void Update()
    {
        if (timeRemaining <= 0f)
        {
            return;
        }

        timeRemaining -= Time.unscaledDeltaTime;

        if (timeRemaining <= 0f)
        {
            Clear();
            return;
        }

        canvasGroup.alpha = fadeOutSeconds > 0f
            ? Mathf.Clamp01(timeRemaining / fadeOutSeconds)
            : 1f;
    }

    /// <summary>Displays a caption, replacing whatever is on screen, and clears it after the duration.</summary>
    public void ShowSubtitle(string text, float duration = 3.0f)
    {
        ShowSubtitle(text, duration, Priority.Interaction);
    }

    /// <summary>
    /// Displays a caption unless something more important is already showing. Repeating the
    /// current caption simply extends it.
    /// </summary>
    public void ShowSubtitle(string text, float duration, int priority)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        bool sameAsCurrent = timeRemaining > 0f && string.Equals(text, currentText, System.StringComparison.Ordinal);

        if (sameAsCurrent)
        {
            timeRemaining = Mathf.Max(timeRemaining, duration);
            currentPriority = Mathf.Max(currentPriority, priority);
            return;
        }

        if (timeRemaining > 0f && priority < currentPriority)
        {
            return;
        }

        currentText = text;
        currentPriority = priority;
        timeRemaining = Mathf.Max(0.1f, duration);

        if (captionText != null)
        {
            captionText.text = text;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
    }

    public void Clear()
    {
        currentText = string.Empty;
        currentPriority = int.MinValue;
        timeRemaining = 0f;

        if (captionText != null)
        {
            captionText.text = string.Empty;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }

    /// <summary>
    /// Static entry point for every caption in the game. Safe to call when no SubtitleManager
    /// exists, which keeps callers free of null checks.
    /// </summary>
    public static void Caption(string text, float duration = 3.0f, int priority = Priority.Interaction)
    {
        if (instance != null)
        {
            instance.ShowSubtitle(text, duration, priority);
        }
    }

    /// <summary>A soft-cornered box, generated so no UI sprite has to be imported.</summary>
    private static Sprite CreateRoundedBoxSprite()
    {
        const int size = 32;
        const int corner = 8;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Procedural Caption Box",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distanceX = Mathf.Max(0f, Mathf.Max(corner - x, x - (size - 1 - corner)));
                float distanceY = Mathf.Max(0f, Mathf.Max(corner - y, y - (size - 1 - corner)));
                float cornerDistance = new Vector2(distanceX, distanceY).magnitude;
                float alpha = Mathf.Clamp01(1f - Mathf.InverseLerp(corner - 1.5f, corner + 0.5f, cornerDistance));
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(corner, corner, corner, corner));
    }
}
