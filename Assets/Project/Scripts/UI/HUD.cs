using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD principal. Arma en runtime (HUDRoot + HUDTheme):
///  - Izquierda: signos vitales (vida, energía, O2) y debajo las mejoras fijadas (PinnedHUD).
///  - Derecha: objetivos (QuestHUD) y el panel de ambiente (EnvironmentHUD).
///  - Centro: avisos arriba y el prompt de interacción abajo. Abajo a la derecha: el arma.
/// Los widgets viejos de la escena (sliders y textos) se ocultan; las fuentes siguen siendo
/// las mismas (Observer sobre Health, Stamina, PlayerOxygen, Weapon, Interactor).
/// </summary>
public class HUD : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] private Health playerHealth;
    [SerializeField] private Inventory inventory;
    [SerializeField] private ShipRepair shipRepair;
    [SerializeField] private Weapon weapon;
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private Stamina stamina;
    [SerializeField] private PlayerOxygen oxygen;

    [Header("Widgets viejos (se ocultan)")]
    [SerializeField] private Slider healthBar;
    [SerializeField] private Slider staminaBar;
    [SerializeField] private Slider oxygenBar;
    [SerializeField] private TMP_Text oxygenText;
    [SerializeField] private TMP_Text resourcesText;
    [SerializeField] private Slider repairBar;
    [SerializeField] private TMP_Text repairText;
    [SerializeField] private TMP_Text weaponText;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private TMP_Text notificationText;

    [Header("Textos")]
    [Tooltip("Título del objetivo de la nave (en Mars: el despegue).")]
    [SerializeField] private string requirementsTitle = "PARA REPARAR LA NAVE";

    [Header("Notifications")]
    [SerializeField] private float notificationDuration = 2.5f;

    private HUDBar healthView, staminaView, oxygenView;
    private TMP_Text healthValue, staminaValue, oxygenValue, oxygenLabel;
    private TMP_Text weaponLabel;
    private RectTransform promptBox, toastBox;
    private TMP_Text promptLabel, toastLabel;
    private CanvasGroup toastGroup;
    private float notificationTimer;
    private float oxygenCurrent, oxygenMax;
    private int lastOxygenSeconds = -1, lastOxygenState = -1;
    private float lastOxygenMax = -1f;

    private void Awake()
    {
        HideLegacyWidgets();
        BuildVitals();
        BuildCenter();
        BuildWeapon();
    }

    private void OnEnable()
    {
        playerHealth.OnHealthChanged += UpdateHealth;
        weapon.OnStrategyChanged += UpdateWeapon;
        interactor.OnFocusChanged += UpdatePrompt;
        stamina.OnStaminaChanged += UpdateStamina;
        oxygen.OnOxygenChanged += UpdateOxygen;
    }

    private void OnDisable()
    {
        playerHealth.OnHealthChanged -= UpdateHealth;
        weapon.OnStrategyChanged -= UpdateWeapon;
        interactor.OnFocusChanged -= UpdatePrompt;
        stamina.OnStaminaChanged -= UpdateStamina;
        oxygen.OnOxygenChanged -= UpdateOxygen;
    }

    private void Start()
    {
        gameObject.AddComponent<PinnedHUD>().Init(inventory);
        gameObject.AddComponent<QuestHUD>().Init(shipRepair, inventory, ShipQuestTitle(requirementsTitle));

        UpdateHealth(playerHealth.Current, playerHealth.Max);
        UpdateStamina(stamina.Current, stamina.Max);
        UpdateOxygen(oxygen.Current, oxygen.Max);
        UpdateWeapon(weapon.CurrentStrategyName);
        UpdatePrompt(string.Empty);
    }

    private void Update()
    {
        ProcessNotifications();
        // El estado "recargando / en refugio" cambia sin que cambie el valor: se repinta seguido.
        RedrawOxygen();
    }

    // ---------- Construcción ----------

    /// <summary>"PARA DESPEGAR AL PRÓXIMO PLANETA" → "Despegar al próximo planeta".</summary>
    private static string ShipQuestTitle(string title)
    {
        if (string.IsNullOrEmpty(title)) return "Reparar la nave";
        string t = title.Trim();
        if (t.ToUpperInvariant().StartsWith("PARA ")) t = t.Substring(5);
        t = t.ToLowerInvariant();
        return char.ToUpperInvariant(t[0]) + t.Substring(1);
    }

    private void HideLegacyWidgets()
    {
        var legacy = new List<Component> { healthBar, staminaBar, oxygenBar, oxygenText, resourcesText, repairBar, repairText, weaponText, promptText, notificationText };
        Canvas oldCanvas = null;
        foreach (Component c in legacy)
        {
            if (c == null) continue;
            if (oldCanvas == null) oldCanvas = c.GetComponentInParent<Canvas>();
            c.gameObject.SetActive(false);
        }
        if (oldCanvas == null) return;

        // Etiquetas sueltas de los sliders viejos ("VIDA", "STAMINA").
        foreach (TMP_Text text in oldCanvas.GetComponentsInChildren<TMP_Text>(true))
        {
            string value = text.text.Trim().ToUpperInvariant();
            if (value == "VIDA" || value == "STAMINA" || value == "ENERGÍA" || value == "OXÍGENO")
                text.gameObject.SetActive(false);
        }
    }

    private void BuildVitals()
    {
        RectTransform panel = HUDTheme.Panel("Vitals", "Astronauta", HUDTheme.Accent);
        HUDRoot.Instance.Add(HUDRoot.Instance.Left, panel, 0);
        healthView = Stat(panel, "VIDA", HUDTheme.Health, out _, out healthValue);
        staminaView = Stat(panel, "ENERGÍA", HUDTheme.Stamina, out _, out staminaValue);
        oxygenView = Stat(panel, "OXÍGENO", HUDTheme.Oxygen, out oxygenLabel, out oxygenValue);
    }

    private static HUDBar Stat(Transform panel, string label, Color color, out TMP_Text left, out TMP_Text right)
    {
        HUDTheme.SplitRow(panel, 20f, 16f, out left, out right);
        left.text = $"<b>{label}</b>";
        left.characterSpacing = 3f;
        left.color = HUDTheme.Muted;
        var bar = new HUDBar(panel, 12f, color);
        return bar;
    }

    private void BuildCenter()
    {
        Transform center = HUDRoot.Instance.Center;

        // Aviso (arriba, centrado).
        Image toast = HUDTheme.RoundedImage("Toast", center, HUDTheme.PanelColor);
        toastBox = toast.rectTransform;
        toastBox.anchorMin = toastBox.anchorMax = toastBox.pivot = new Vector2(0.5f, 1f);
        toastBox.anchoredPosition = new Vector2(0f, -28f);
        toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
        toastLabel = FittedLabel(toastBox, 24, HUDTheme.Warn);
        toastGroup.alpha = 0f;

        // Prompt de interacción (abajo, centrado).
        Image prompt = HUDTheme.RoundedImage("Prompt", center, HUDTheme.PanelColor);
        promptBox = prompt.rectTransform;
        promptBox.anchorMin = promptBox.anchorMax = promptBox.pivot = new Vector2(0.5f, 0f);
        promptBox.anchoredPosition = new Vector2(0f, 150f);
        promptLabel = FittedLabel(promptBox, 22, HUDTheme.Text);
    }

    /// <summary>Caja que se ajusta al texto (ancho y alto).</summary>
    private static TMP_Text FittedLabel(RectTransform box, float size, Color color)
    {
        HorizontalLayoutGroup layout = box.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(22, 22, 10, 12);
        layout.childControlWidth = layout.childControlHeight = true;
        ContentSizeFitter fitter = box.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        TMP_Text text = RuntimeUI.Text("Text", box, "", size, TextAlignmentOptions.Center);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = color;
        return text;
    }

    private void BuildWeapon()
    {
        RectTransform panel = HUDTheme.Panel("Weapon", "Arma", HUDTheme.Accent);
        HUDRoot.Instance.Add(HUDRoot.Instance.BottomRight, panel, 0);
        weaponLabel = HUDTheme.Label(panel, "", 20, HUDTheme.Text);
        weaponLabel.fontStyle = FontStyles.Bold;
        // En el tutorial no se construye: la ayuda muestra solo las teclas que existen ahí.
        bool canBuild = FindFirstObjectByType<BuildModeController>() != null;
        string hints = canBuild ? "[Clic] disparar   [B] construir   [Tab] mochila" : "[Clic] disparar   [E] usar   [Tab] mochila";
        TMP_Text keys = HUDTheme.Label(panel, hints, 14, HUDTheme.Muted);
        keys.textWrappingMode = TextWrappingModes.Normal;
    }

    // ---------- Avisos ----------

    private void ProcessNotifications()
    {
        if (notificationTimer > 0f)
        {
            notificationTimer -= Time.deltaTime;
            toastGroup.alpha = Mathf.Clamp01(notificationTimer / 0.35f);
            return;
        }

        if (NotificationQueue.TryDequeue(out string message))
        {
            toastLabel.text = message;
            notificationTimer = notificationDuration;
            toastGroup.alpha = 1f;
        }
    }

    // ---------- Observers ----------

    private void UpdateHealth(float current, float max)
    {
        float t = max > 0f ? current / max : 0f;
        healthView.Set(t);
        healthValue.text = $"{Mathf.CeilToInt(current)} / {max:0}";
        healthValue.color = t <= 0.25f ? HUDTheme.Bad : HUDTheme.Text;
    }

    private void UpdateStamina(float current, float max)
    {
        staminaView.Set(max > 0f ? current / max : 0f);
        staminaValue.text = $"{Mathf.CeilToInt(current)} / {max:0}";
    }

    private void UpdateOxygen(float current, float max)
    {
        oxygenCurrent = current;
        oxygenMax = max;
        RedrawOxygen();
    }

    private void RedrawOxygen()
    {
        if (oxygenView == null || oxygenMax <= 0f) return;
        float t = oxygenCurrent / oxygenMax;
        bool safe = oxygen.InSafeZone;
        bool low = !safe && t <= 0.25f;
        Color color = HUDTheme.Oxygen;
        if (low)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
            color = Color.Lerp(HUDTheme.Bad, Color.white, pulse * 0.35f);
        }
        oxygenView.Set(t, color);

        // El texto se arma solo si cambió algo (esto corre cada frame: evita basura para el GC).
        int seconds = Mathf.CeilToInt(oxygenCurrent);
        int state = safe ? 1 : low ? 2 : 0;
        if (seconds == lastOxygenSeconds && state == lastOxygenState && oxygenMax == lastOxygenMax) return;
        lastOxygenSeconds = seconds;
        lastOxygenState = state;
        lastOxygenMax = oxygenMax;
        string status = safe ? $"<color={HUDTheme.Hex(HUDTheme.Teal)}>recargando</color>  " : low ? $"<color={HUDTheme.Hex(HUDTheme.Bad)}>¡bajo!</color>  " : "";
        oxygenValue.text = $"{status}{seconds} / {oxygenMax:0} s";
    }

    private void UpdateWeapon(string strategyName) => weaponLabel.text = strategyName;

    private void UpdatePrompt(string prompt)
    {
        bool show = !string.IsNullOrEmpty(prompt);
        promptBox.gameObject.SetActive(show);
        if (show) promptLabel.text = KeyCaps(prompt);
    }

    /// <summary>Resalta las teclas: "[E] Abrir" → tecla en naranja.</summary>
    private static string KeyCaps(string prompt) =>
        prompt.Replace("[", $"<color={HUDTheme.Hex(HUDTheme.Accent)}><b>[").Replace("]", "]</b></color>");
}
