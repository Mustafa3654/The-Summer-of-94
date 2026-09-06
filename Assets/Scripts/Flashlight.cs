using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Toggleable flashlight with subtle, irregular intensity changes to suggest a dying battery.
/// </summary>
public sealed class Flashlight : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Light flashlightLight;

    [Header("Toggle")]
    [SerializeField] private bool startsOn = true;
    [SerializeField] private Key toggleKey = Key.F;

    [Header("Flicker")]
    [SerializeField] private bool flickerWhenOn = true;
    [SerializeField, Range(0f, 1f)] private float flickerChance = 0.35f;
    [SerializeField] private Vector2 flickerInterval = new Vector2(0.04f, 0.18f);
    [SerializeField] private Vector2 flickerIntensityMultiplier = new Vector2(0.76f, 1.04f);
    [SerializeField] private float intensitySmoothing = 18f;

    private float baseIntensity;
    private float baseRange;
    private float flickerTimer;
    private float targetIntensity;
    private bool isOn;

    public bool IsOn => isOn;

    /// <summary>The spot light itself, so spirits can test whether the beam is on them.</summary>
    public Light Beam => flashlightLight;

    private void Awake()
    {
        if (flashlightLight == null)
        {
            flashlightLight = GetComponentInChildren<Light>();
        }

        if (flashlightLight != null)
        {
            baseIntensity = flashlightLight.intensity;
            baseRange = flashlightLight.range;
        }
    }

    private void Start()
    {
        SetFlashlightState(startsOn);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            SetFlashlightState(!isOn);
        }

        if (!isOn || flashlightLight == null)
        {
            return;
        }

        if (!flickerWhenOn)
        {
            flashlightLight.intensity = baseIntensity;
            flashlightLight.range = baseRange;
            return;
        }

        flickerTimer -= Time.deltaTime;
        if (flickerTimer <= 0f)
        {
            flickerTimer = Random.Range(
                Mathf.Min(flickerInterval.x, flickerInterval.y),
                Mathf.Max(flickerInterval.x, flickerInterval.y));

            bool shouldFlicker = Random.value <= flickerChance;
            if (!shouldFlicker)
            {
                targetIntensity = baseIntensity;
            }
            else if (Random.value < 0.08f)
            {
                // Rare, nearly imperceptible battery dropout.
                targetIntensity = baseIntensity * Random.Range(0.05f, 0.25f);
            }
            else
            {
                targetIntensity = baseIntensity * Random.Range(
                    Mathf.Min(flickerIntensityMultiplier.x, flickerIntensityMultiplier.y),
                    Mathf.Max(flickerIntensityMultiplier.x, flickerIntensityMultiplier.y));
            }
        }

        flashlightLight.intensity = Mathf.Lerp(
            flashlightLight.intensity,
            targetIntensity,
            1f - Mathf.Exp(-intensitySmoothing * Time.deltaTime));
    }

    private void SetFlashlightState(bool shouldBeOn)
    {
        isOn = shouldBeOn;
        flickerTimer = 0f;
        targetIntensity = baseIntensity;

        if (flashlightLight != null)
        {
            flashlightLight.enabled = shouldBeOn;
            flashlightLight.intensity = shouldBeOn ? baseIntensity : 0f;
        }
    }
}
