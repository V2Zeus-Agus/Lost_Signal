using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Menú de pausa armado en runtime con el estilo del HUD: tarjeta central con las acciones en
/// orden (Reanudar, Guardar, Opciones | Reiniciar, Menú principal) y al lado la lista de
/// controles leída de las teclas reales del Input System. Lo muestra GameScreens.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    private const float CardWidth = 460f;
    private const float ControlsWidth = 400f;

    private Canvas canvas;
    private TMP_Text subtitle;
    private TMP_Text controlsText;
    private Button saveButton;

    public void Build(UnityAction onResume, UnityAction onOptions, UnityAction onRestart, UnityAction onMainMenu)
    {
        RuntimeUI.EnsureEventSystem();
        canvas = RuntimeUI.CreateCanvas("PauseMenuCanvas", transform, 40);
        Image dim = RuntimeUI.Image("Dim", canvas.transform, new Color(0.01f, 0.015f, 0.03f, 0.72f));
        RuntimeUI.Stretch(dim.rectTransform);

        bool tutorial = FindFirstObjectByType<TutorialGuide>() != null;
        bool canSave = SaveManager.Instance != null;

        // Contenedor horizontal: acciones + controles, centrado.
        RectTransform row = RuntimeUI.Rect("Row", canvas.transform);
        row.anchorMin = row.anchorMax = row.pivot = new Vector2(0.5f, 0.5f);
        HorizontalLayoutGroup h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 20f;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        ContentSizeFitter fit = row.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // --- Tarjeta de acciones ---
        RectTransform card = Card(row, CardWidth, HUDTheme.Accent);
        TMP_Text title = HUDTheme.Label(card, "PAUSA", 40, HUDTheme.Text);
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 12f;
        subtitle = HUDTheme.Label(card, "", 16, HUDTheme.Muted);
        Spacer(card, 8f);

        MenuButton(card, "Reanudar", onResume, HUDTheme.Accent, true);
        if (canSave) saveButton = MenuButton(card, "Guardar partida", SaveNow, RuntimeUI.ButtonColor, false);
        MenuButton(card, "Opciones", onOptions, RuntimeUI.ButtonColor, false);
        Spacer(card, 6f);
        Divider(card);
        Spacer(card, 6f);
        MenuButton(card, tutorial ? "Reiniciar tutorial" : canSave ? "Volver al último guardado" : "Reiniciar", onRestart,
            new Color(0.2f, 0.22f, 0.28f, 1f), false);
        MenuButton(card, canSave ? "Guardar y salir al menú" : "Salir al menú principal", onMainMenu,
            new Color(0.42f, 0.16f, 0.15f, 1f), false);

        // --- Tarjeta de controles ---
        RectTransform controls = Card(row, ControlsWidth, HUDTheme.Teal);
        TMP_Text controlsTitle = HUDTheme.Label(controls, "CONTROLES", 15, HUDTheme.Muted);
        controlsTitle.fontStyle = FontStyles.Bold;
        controlsTitle.characterSpacing = 6f;
        controlsText = HUDTheme.Label(controls, "", 17, HUDTheme.Text);
        controlsText.lineSpacing = 10f;
        controlsText.text = ControlsText(tutorial);

        canvas.gameObject.SetActive(false);
    }

    public void SetVisible(bool visible)
    {
        if (canvas == null) return;
        if (visible) RefreshSubtitle();
        canvas.gameObject.SetActive(visible);
        if (!visible) RuntimeUI.ClearSelection();
    }

    private void RefreshSubtitle()
    {
        PlanetDefinition planet = PlanetContext.Current;
        string where = FindFirstObjectByType<TutorialGuide>() != null ? "Tutorial"
                     : planet != null ? planet.displayName : "Lost Signal";
        string time = "";
        if (SaveManager.Instance != null)
        {
            int minutes = Mathf.FloorToInt(SaveManager.Instance.TotalPlayTime / 60f);
            time = $"  ·  {minutes / 60}h {minutes % 60:00}m jugados";
        }
        subtitle.text = where + time;
    }

    private void SaveNow()
    {
        if (SaveManager.Instance == null) return;
        SaveManager.Instance.SaveGame();
        NotificationQueue.Enqueue("Partida guardada");
        TMP_Text label = saveButton.GetComponentInChildren<TMP_Text>();
        label.text = "Guardado";
        StopAllCoroutines();
        StartCoroutine(ResetSaveLabel());
    }

    // En pausa timeScale = 0: hay que esperar en tiempo real.
    private System.Collections.IEnumerator ResetSaveLabel()
    {
        yield return new WaitForSecondsRealtime(1.5f);
        if (saveButton != null) saveButton.GetComponentInChildren<TMP_Text>().text = "Guardar partida";
    }

    private string ControlsText(bool tutorial)
    {
        string accent = HUDTheme.Hex(HUDTheme.Accent);
        var sb = new System.Text.StringBuilder();
        PlayerInputHandler input = FindFirstObjectByType<PlayerInputHandler>();
        if (input != null)
            foreach ((string action, string keys) in input.ControlHints())
                sb.Append("<color=").Append(accent).Append("><b>").Append(keys).Append("</b></color>  ").AppendLine(action);
        sb.Append("<color=").Append(accent).Append("><b>Tab / I</b></color>  ").AppendLine("Mochila");
        sb.Append("<color=").Append(accent).Append("><b>J</b></color>  ").AppendLine("Mostrar / ocultar objetivos");
        if (!tutorial && FindFirstObjectByType<BuildModeController>() != null)
        {
            sb.Append("<color=").Append(accent).Append("><b>B</b></color>  ").AppendLine("Construir");
            sb.Append("<color=").Append(accent).Append("><b>R</b></color>  ").AppendLine("Girar pieza");
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        sb.Append("<color=").Append(accent).Append("><b>F8</b></color>  ").Append("Consola de pruebas");
#endif
        return sb.ToString().TrimEnd();
    }

    // ---------- Helpers ----------

    private static RectTransform Card(Transform parent, float width, Color accent)
    {
        Image bg = HUDTheme.RoundedImage("Card", parent, new Color(0.05f, 0.065f, 0.1f, 0.96f));
        RectTransform card = bg.rectTransform;
        VerticalLayoutGroup v = card.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(32, 32, 28, 30);
        v.spacing = 10f;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        LayoutElement le = card.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = width;

        // Franja de color arriba de la tarjeta.
        Image strip = HUDTheme.RoundedImage("Accent", card, accent);
        strip.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        strip.rectTransform.anchorMin = new Vector2(0f, 1f);
        strip.rectTransform.anchorMax = new Vector2(1f, 1f);
        strip.rectTransform.pivot = new Vector2(0.5f, 1f);
        strip.rectTransform.offsetMin = new Vector2(32f, -6f);
        strip.rectTransform.offsetMax = new Vector2(-32f, 0f);
        return card;
    }

    private static Button MenuButton(Transform parent, string label, UnityAction onClick, Color color, bool primary)
    {
        Button button = RuntimeUI.Button(label, parent, label, onClick, primary ? 24f : 21f);
        Image image = button.GetComponent<Image>();
        image.sprite = HUDTheme.Rounded;
        image.type = Image.Type.Sliced;
        image.color = color;
        TMP_Text text = button.GetComponentInChildren<TMP_Text>();
        text.fontStyle = primary ? FontStyles.Bold : FontStyles.Normal;
        text.color = Color.white;
        LayoutElement le = button.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = primary ? 60f : 52f;
        return button;
    }

    private static void Spacer(Transform parent, float height) => HUDTheme.Row(parent, height);

    private static void Divider(Transform parent)
    {
        RectTransform row = HUDTheme.Row(parent, 1f);
        Image line = RuntimeUI.Image("Line", row, new Color(1f, 1f, 1f, 0.1f), false);
        RuntimeUI.Stretch(line.rectTransform);
    }
}
