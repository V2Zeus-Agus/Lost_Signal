using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Frío extremo: fuera de la cúpula, por debajo de cierta temperatura, el visor se congela de a poco.
/// Después de un rato expuesto, el frío daña la vida MUY lento. Al volver al calor se descongela.
/// Lee la temperatura de BiomeMap; en escenas sin biomas no hace nada.
/// </summary>
public class ColdExposure : MonoBehaviour
{
    [Header("Cuándo hace frío")]
    [Tooltip("A partir de esta temperatura (°C) el frío empieza a afectar.")]
    [SerializeField] private float freezingTemperature = -60f;

    [Header("Visor")]
    [Tooltip("Segundos de exposición hasta que el visor queda congelado del todo.")]
    [SerializeField, Min(1f)] private float secondsToFullFrost = 20f;
    [SerializeField, Range(0f, 1f)] private float maxFrostOpacity = 0.8f;
    [Tooltip("Qué tan rápido se descongela comparado con lo que tarda en congelarse.")]
    [SerializeField, Min(0.1f)] private float thawSpeed = 2f;

    [Header("Daño (muy lento)")]
    [SerializeField, Min(0f)] private float damageAfterSeconds = 20f;
    [SerializeField, Min(0f)] private float damagePerTick = 1f;
    [SerializeField, Min(0.1f)] private float secondsPerTick = 4f;

    [Header("Referencias")]
    [SerializeField] private Health health;
    [SerializeField] private PlayerOxygen oxygen;
    [SerializeField] private PlayerUpgrades upgrades;

    public float Exposure { get; private set; }
    public bool IsFreezing { get; private set; }

    /// <summary>Temperatura desde la que congela, según el nivel del traje (cada nivel aguanta 20 °C más).</summary>
    public float CurrentLimit
    {
        get
        {
            int suit = upgrades != null ? upgrades.GetLevel(UpgradeIds.Suit) : 0;
            return freezingTemperature - SuitProgression.ColdLimitPerLevel * Mathf.Clamp(suit, 0, SuitProgression.MaxLevel);
        }
    }

    private Image frost;
    private Canvas canvas;
    private float tickTimer;
    private bool warnedFrost, warnedDamage;

    private void Awake()
    {
        if (health == null) health = GetComponent<Health>();
        if (oxygen == null) oxygen = GetComponent<PlayerOxygen>();
        if (upgrades == null) upgrades = GetComponent<PlayerUpgrades>();
        BuildOverlay();
    }

    private void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsPlaying || health == null || health.IsDead)
        {
            canvas.enabled = GameManager.Instance != null && GameManager.Instance.IsPlaying;
            return;
        }
        canvas.enabled = true;

        BiomeMap biomes = BiomeMap.Instance;
        bool inShelter = oxygen != null && oxygen.InSafeZone;
        IsFreezing = biomes != null && !inShelter && biomes.CurrentTemperature <= CurrentLimit;

        if (IsFreezing) Exposure += Time.deltaTime;
        else Exposure = Mathf.Max(0f, Exposure - Time.deltaTime * thawSpeed);

        float frostAmount = Mathf.Clamp01(Exposure / secondsToFullFrost);
        Color c = frost.color;
        c.a = frostAmount * maxFrostOpacity;
        frost.color = c;

        if (IsFreezing && !warnedFrost && Exposure > 3f)
        {
            warnedFrost = true;
            NotificationQueue.Enqueue("El visor se está congelando: buscá calor o mejorá el traje");
        }

        if (IsFreezing && Exposure >= damageAfterSeconds)
        {
            if (!warnedDamage)
            {
                warnedDamage = true;
                NotificationQueue.Enqueue("¡El frío te está dañando!");
            }

            tickTimer += Time.deltaTime;
            if (tickTimer >= secondsPerTick)
            {
                tickTimer = 0f;
                if (damagePerTick >= health.Current)
                    GameManager.Instance.SetDefeatCause("TE CONGELASTE",
                        "Estuviste demasiado tiempo en el frío. Volvé a la base para descongelarte.");
                health.TakeDamage(damagePerTick);
            }
        }
        else
        {
            tickTimer = 0f;
        }

        if (Exposure <= 0f)
        {
            warnedFrost = false;
            warnedDamage = false;
        }
    }

    // ---------- Escarcha del visor (se genera en runtime, sin texturas importadas) ----------

    private void BuildOverlay()
    {
        var canvasGo = new GameObject("ColdVisorCanvas", typeof(RectTransform), typeof(Canvas));
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -2; // debajo de la guía y de los menús

        var frostGo = new GameObject("Frost", typeof(RectTransform), typeof(Image));
        frostGo.transform.SetParent(canvasGo.transform, false);
        RectTransform rt = (RectTransform)frostGo.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        frost = frostGo.GetComponent<Image>();
        frost.raycastTarget = false;
        frost.sprite = CreateFrostSprite(256);
        frost.color = new Color(0.85f, 0.93f, 1f, 0f);
    }

    private static Sprite CreateFrostSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                // Más escarcha en los bordes del visor, el centro queda casi libre.
                float edge = Mathf.Max(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f)) * 2f;
                float vignette = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, edge)); // centro libre
                float crystals = Mathf.PerlinNoise(u * 18f, v * 18f) * 0.6f + Mathf.PerlinNoise(u * 55f + 7f, v * 55f + 3f) * 0.4f;
                float a = Mathf.Clamp01(vignette * (0.55f + crystals * 0.8f));
                byte shade = (byte)(215 + crystals * 40f);
                pixels[y * size + x] = new Color32(shade, shade, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
