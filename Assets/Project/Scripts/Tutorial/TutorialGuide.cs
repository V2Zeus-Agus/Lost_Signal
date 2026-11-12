using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Guía del tutorial:
///  1) Reconocimiento: unos segundos con marcadores sobre todo lo importante.
///  2) Objetivos en orden, con checklist arriba a la derecha y marcadores con distancia
///     (si el objetivo está fuera de pantalla, el marcador se pega al borde y apunta hacia él).
///  3) Ayudas contextuales: si te queda poco O2 te marca la nave; si te queda poca vida, la estación médica.
/// Todas las referencias se buscan solas en la escena, y la UI se crea en runtime.
/// </summary>
public class TutorialGuide : MonoBehaviour
{
    [Header("Reconocimiento inicial")]
    [SerializeField, Min(0f)] private float introSeconds = 12f;

    [Header("Marcadores")]
    [SerializeField, Min(1)] private int markersPerResource = 3;
    [SerializeField] private float screenMargin = 60f;

    [Header("Ayudas contextuales")]
    [SerializeField, Range(0f, 1f)] private float lowOxygenHint = 0.35f;
    [SerializeField, Range(0f, 1f)] private float lowHealthHint = 0.4f;

    private static readonly Color ShipColor = new Color(0.45f, 0.85f, 1f);
    private static readonly Color StationColor = new Color(0.55f, 1f, 0.6f);
    private static readonly Color WreckColor = new Color(1f, 0.85f, 0.4f);
    private static readonly Color DangerColor = new Color(1f, 0.35f, 0.3f);

    // ---------- Referencias de la escena ----------
    private ShipRepair shipRepair;
    private Inventory inventory;
    private SalvageWreck wreck;
    private UpgradeShop oxygenShop;
    private HealStation healStation;
    private PlayerUpgrades upgrades;
    private PlayerOxygen oxygen;
    private Health playerHealth;
    private Transform player;
    private Camera cam;
    private readonly List<ResourceNode> nodes = new List<ResourceNode>();
    private readonly List<Transform> nests = new List<Transform>();

    // ---------- Objetivos ----------
    private struct Target
    {
        public Transform transform;
        public string label;
        public Color color;
    }

    private class Objective
    {
        public Func<string> label;
        public Func<bool> isDone;
        public Func<List<Target>> targets;
        public bool optional;
        public bool announced;
    }

    private readonly List<Objective> objectives = new List<Objective>();
    private int currentIndex;
    private float playTime;

    // ---------- UI ----------
    private Canvas canvas;
    private RectTransform canvasRect;
    private TMP_Text objectivesText;
    private RectTransform objectivesPanel;
    private string lastObjectivesText;
    private TMP_Text collapseHint;

    private class MarkerView
    {
        public RectTransform root;
        public Image icon;
        public TMP_Text text;
    }

    private readonly List<MarkerView> markerPool = new List<MarkerView>();
    private readonly Dictionary<Transform, float> markerHeights = new Dictionary<Transform, float>();
    private readonly List<Target> frameTargets = new List<Target>();

    // ======================================================================

    private void Start()
    {
        FindSceneReferences();
        BuildObjectives();
        BuildUI();
    }

    private void FindSceneReferences()
    {
        shipRepair = FindFirstObjectByType<ShipRepair>();
        inventory = FindFirstObjectByType<Inventory>();
        wreck = FindFirstObjectByType<SalvageWreck>();
        oxygenShop = FindFirstObjectByType<UpgradeShop>();
        healStation = FindFirstObjectByType<HealStation>();
        oxygen = FindFirstObjectByType<PlayerOxygen>();

        if (inventory != null)
        {
            player = inventory.transform;
            upgrades = inventory.GetComponent<PlayerUpgrades>();
            playerHealth = inventory.GetComponent<Health>();
        }

        cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();

        nodes.AddRange(FindObjectsByType<ResourceNode>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        foreach (CreatureNest nest in FindObjectsByType<CreatureNest>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            nests.Add(nest.transform);
    }

    // ---------- Definición del tutorial ----------

    private void BuildObjectives()
    {
        if (wreck != null)
        {
            objectives.Add(new Objective
            {
                label = () => "Extraé las partes de la nave rota",
                // Si ya juntaste todo por otro lado, la nave rota deja de ser necesaria.
                isDone = () => wreck.IsSalvaged || AllResourcesCollected(),
                targets = () => Single(wreck.transform, "NAVE ROTA · partes útiles", WreckColor)
            });
        }

        if (shipRepair != null)
        {
            foreach (RepairRequirement requirement in shipRepair.Requirements)
            {
                RepairRequirement req = requirement; // captura para el lambda
                if (req.resourceType == null) continue;

                objectives.Add(new Objective
                {
                    label = () => $"Juntá {Mathf.Min(Collected(req.resourceType), req.amount)}/{req.amount} de " +
                                  $"<color=#{ColorUtility.ToHtmlStringRGB(req.resourceType.uiColor)}>{req.resourceType.displayName}</color>",
                    isDone = () => Collected(req.resourceType) >= req.amount,
                    targets = () => NearestNodes(req.resourceType)
                });
            }
        }

        if (oxygenShop != null && upgrades != null)
        {
            objectives.Add(new Objective
            {
                optional = true,
                label = () => "Opcional: mejorá el tanque de O2 en la tienda de mejoras",
                // Se da por cumplido si mejorás el tanque o si ya fuiste a depositar a la nave.
                isDone = () => upgrades.GetLevel(UpgradeIds.OxygenTank) > 0 || (shipRepair != null && shipRepair.Progress > 0f),
                targets = () => Single(oxygenShop.transform, "TIENDA DE MEJORAS", StationColor)
            });
        }

        if (shipRepair != null)
        {
            objectives.Add(new Objective
            {
                label = () => "Volvé a la nave y depositá los recursos con [E]",
                isDone = () => shipRepair.IsRepaired,
                targets = () => Single(shipRepair.transform, "NAVE · depositá con [E]", ShipColor)
            });
        }
    }

    private int Collected(ItemDefinition item)
    {
        int amount = inventory != null ? inventory.GetAmount(item) : 0;
        if (shipRepair != null) amount += shipRepair.GetDelivered(item);
        return amount;
    }

    private bool AllResourcesCollected()
    {
        if (shipRepair == null) return false;
        foreach (RepairRequirement req in shipRepair.Requirements)
            if (req.resourceType != null && Collected(req.resourceType) < req.amount) return false;
        return true;
    }

    private bool InIntro => playTime < introSeconds;

    // ---------- Loop ----------

    private void LateUpdate()
    {
        bool playing = GameManager.Instance != null && GameManager.Instance.IsPlaying;
        if (canvas != null) canvas.enabled = playing;
        if (!playing || cam == null || player == null) return;

        playTime += Time.deltaTime;

        AdvanceObjectives();
        RefreshObjectivesPanel();

        frameTargets.Clear();
        if (InIntro) AddIntroTargets();
        else if (currentIndex < objectives.Count) frameTargets.AddRange(objectives[currentIndex].targets());
        AddContextualHints();

        DrawMarkers();
    }

    private void AdvanceObjectives()
    {
        if (InIntro) return;

        while (currentIndex < objectives.Count)
        {
            Objective current = objectives[currentIndex];
            if (!current.announced)
            {
                current.announced = true;
                NotificationQueue.Enqueue("Nuevo objetivo: " + StripTags(current.label()));
            }

            if (!current.isDone()) break;

            NotificationQueue.Enqueue("Objetivo cumplido");
            currentIndex++;
        }
    }

    // ---------- Targets ----------

    private void AddIntroTargets()
    {
        if (shipRepair != null) frameTargets.Add(new Target { transform = shipRepair.transform, label = "NAVE · recargá O2 en la cúpula", color = ShipColor });
        if (oxygenShop != null) frameTargets.Add(new Target { transform = oxygenShop.transform, label = "TIENDA DE MEJORAS", color = StationColor });
        if (healStation != null) frameTargets.Add(new Target { transform = healStation.transform, label = "ESTACIÓN MÉDICA", color = StationColor });
        if (wreck != null) frameTargets.Add(new Target { transform = wreck.transform, label = "NAVE ROTA · partes útiles", color = WreckColor });

        if (shipRepair != null)
            foreach (RepairRequirement req in shipRepair.Requirements)
                if (req.resourceType != null) frameTargets.AddRange(NearestNodes(req.resourceType, 1));

        foreach (Transform nest in nests)
            if (nest != null) frameTargets.Add(new Target { transform = nest, label = "NIDO · peligro", color = DangerColor });
    }

    private void AddContextualHints()
    {
        if (oxygen != null && !oxygen.InSafeZone && oxygen.Max > 0f && oxygen.Current / oxygen.Max <= lowOxygenHint && shipRepair != null)
            AddUnique(new Target { transform = shipRepair.transform, label = "¡POCO O2! · volvé a la cúpula", color = DangerColor });

        if (playerHealth != null && healStation != null && healStation.UsesLeft > 0
            && playerHealth.Max > 0f && playerHealth.Current / playerHealth.Max <= lowHealthHint)
            AddUnique(new Target { transform = healStation.transform, label = "POCA VIDA · estación médica", color = DangerColor });
    }

    private void AddUnique(Target target)
    {
        // Si el objetivo actual ya marca ese objeto, la ayuda lo reemplaza.
        frameTargets.RemoveAll(t => t.transform == target.transform);
        frameTargets.Add(target);
    }

    private static List<Target> Single(Transform transform, string label, Color color) =>
        new List<Target> { new Target { transform = transform, label = label, color = color } };

    private List<Target> NearestNodes(ItemDefinition item, int count = -1)
    {
        if (count < 0) count = markersPerResource;
        var result = new List<Target>();
        var candidates = new List<ResourceNode>();
        foreach (ResourceNode node in nodes)
            if (node != null && node.gameObject.activeInHierarchy && node.Item == item) candidates.Add(node);

        Vector3 origin = player.position;
        candidates.Sort((a, b) => (a.transform.position - origin).sqrMagnitude.CompareTo((b.transform.position - origin).sqrMagnitude));

        for (int i = 0; i < candidates.Count && i < count; i++)
            result.Add(new Target { transform = candidates[i].transform, label = item.displayName.ToUpperInvariant(), color = item.uiColor });
        return result;
    }

    // ---------- UI ----------

    private void BuildUI()
    {
        var canvasGo = new GameObject("TutorialGuideCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -1; // debajo de los menús (pausa, victoria)
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect = (RectTransform)canvasGo.transform;

        // Panel de objetivos: arriba de todo en la columna derecha del HUD, con el mismo estilo.
        objectivesPanel = HUDTheme.Panel("TutorialObjectives", "Tutorial", HUDTheme.Accent);
        HUDRoot.Instance.Add(HUDRoot.Instance.Right, objectivesPanel, -10);
        objectivesText = HUDTheme.Label(objectivesPanel, "", 18, HUDTheme.Text);
        // Se oculta junto con los objetivos ([J]); el título queda como recordatorio.
        TMP_Text hint = RuntimeUI.Text("ToggleHint", objectivesPanel.GetChild(0), "[J] ocultar", 14, TextAlignmentOptions.TopRight);
        hint.color = HUDTheme.Muted;
        RuntimeUI.Stretch(hint.rectTransform);
        collapseHint = hint;
        objectivesText.lineSpacing = 4f;
    }

    private void RefreshObjectivesPanel()
    {
        var sb = new StringBuilder();

        if (InIntro)
        {
            int secondsLeft = Mathf.CeilToInt(introSeconds - playTime);
            sb.AppendLine($"<color=#7fd8ff><b>■ Reconocé la zona</b> ({secondsLeft} s)</color>");
            sb.AppendLine("<size=15><color=#9aa6b5>Los marcadores muestran la nave, las estaciones, los recursos y los nidos. Tu O2 solo se recarga en la cúpula.</color></size>");
        }
        else
        {
            for (int i = 0; i < objectives.Count; i++)
            {
                Objective o = objectives[i];
                string text = o.label();
                if (i < currentIndex || (i > currentIndex && o.isDone()))
                    sb.Append("<color=#78eb84>■ <s>").Append(StripTags(text)).AppendLine("</s></color>");
                else if (i == currentIndex)
                    sb.Append("<color=#ff8f33>■</color> <color=#ffffff><b>").Append(text).AppendLine("</b></color>");
                else
                    sb.Append("<color=#5d6672>■</color> <color=#9aa6b5>").Append(StripTags(text)).AppendLine("</color>");
            }
        }

        bool collapsed = QuestHUD.Collapsed;
        objectivesText.gameObject.SetActive(!collapsed);
        if (collapseHint != null) collapseHint.text = collapsed ? "<color=#ff8f33>[J]</color> mostrar" : "[J] ocultar";
        string result = sb.ToString();
        if (result != lastObjectivesText)
        {
            lastObjectivesText = result;
            objectivesText.text = result.TrimEnd(); // el panel se ajusta solo (LayoutGroup)
        }
    }

    private void DrawMarkers()
    {
        while (markerPool.Count < frameTargets.Count) markerPool.Add(CreateMarker());

        for (int i = 0; i < markerPool.Count; i++)
        {
            bool active = i < frameTargets.Count && frameTargets[i].transform != null;
            markerPool[i].root.gameObject.SetActive(active);
            if (active) PlaceMarker(markerPool[i], frameTargets[i]);
        }
    }

    private void PlaceMarker(MarkerView view, Target target)
    {
        Vector3 world = target.transform.position + Vector3.up * MarkerHeight(target.transform);
        Vector3 screen = cam.WorldToScreenPoint(world);

        bool behind = screen.z < 0f;
        if (behind)
        {
            // Detrás de la cámara: se refleja para que la flecha apunte del lado correcto.
            screen.x = Screen.width - screen.x;
            screen.y = Screen.height - screen.y;
        }

        float minX = screenMargin, maxX = Screen.width - screenMargin;
        float minY = screenMargin, maxY = Screen.height - screenMargin;
        bool offscreen = behind || screen.x < minX || screen.x > maxX || screen.y < minY || screen.y > maxY;

        string arrow = "";
        if (offscreen)
        {
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 dir = new Vector2(screen.x, screen.y) - center;
            if (behind && dir.sqrMagnitude < 1f) dir = Vector2.down;
            // Empuja el punto hasta el borde del rectángulo seguro.
            float scale = Mathf.Min(
                Mathf.Abs(dir.x) > 0.01f ? (maxX - center.x) / Mathf.Abs(dir.x) : float.MaxValue,
                Mathf.Abs(dir.y) > 0.01f ? (maxY - center.y) / Mathf.Abs(dir.y) : float.MaxValue);
            Vector2 edge = center + dir * scale;
            screen.x = edge.x;
            screen.y = edge.y;
            arrow = Mathf.Abs(dir.x) > Mathf.Abs(dir.y) ? (dir.x > 0 ? " >>" : "<< ") : (dir.y > 0 ? "^ " : "v ");
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);
        view.root.anchoredPosition = local;

        float distance = Vector3.Distance(player.position, target.transform.position);
        string hex = ColorUtility.ToHtmlStringRGB(target.color);
        string label = arrow == " >>" ? $"{target.label} {distance:0} m{arrow}"
                     : arrow.Length > 0 ? $"{arrow}{target.label} {distance:0} m"
                     : $"{target.label}\n<size=18>{distance:0} m</size>";
        view.text.text = $"<color=#{hex}>{label}</color>";
        view.icon.color = target.color;
        view.icon.enabled = !offscreen;

        // La etiqueta nunca se sale de la pantalla: se corre hacia adentro si hace falta.
        float halfW = canvasRect.rect.width * 0.5f, halfH = canvasRect.rect.height * 0.5f;
        float textHalf = Mathf.Min(view.text.preferredWidth, 420f) * 0.5f;
        float labelX = Mathf.Clamp(local.x, -halfW + textHalf + 12f, halfW - textHalf - 12f);
        float labelY = Mathf.Clamp(local.y + 34f, -halfH + 40f, halfH - 40f);
        view.text.rectTransform.anchoredPosition = new Vector2(labelX - local.x, labelY - local.y);
    }

    private float MarkerHeight(Transform target)
    {
        if (markerHeights.TryGetValue(target, out float height)) return height;

        height = 2f;
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            height = bounds.max.y - target.position.y + 0.8f;
        }
        markerHeights[target] = height;
        return height;
    }

    private MarkerView CreateMarker()
    {
        var root = new GameObject("Marker", typeof(RectTransform));
        root.transform.SetParent(canvasRect, false);
        RectTransform rootRt = (RectTransform)root.transform;
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.sizeDelta = Vector2.zero;

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(root.transform, false);
        RectTransform iconRt = (RectTransform)iconGo.transform;
        iconRt.sizeDelta = new Vector2(18f, 18f);
        iconRt.localRotation = Quaternion.Euler(0f, 0f, 45f); // rombo

        TMP_Text text = CreateText(root.transform, 22, TextAlignmentOptions.Center);
        RectTransform textRt = text.rectTransform;
        textRt.sizeDelta = new Vector2(420f, 60f);
        textRt.anchoredPosition = new Vector2(0f, 34f);
        text.fontStyle = FontStyles.Bold;
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color32(0, 0, 0, 220);

        return new MarkerView { root = rootRt, icon = iconGo.GetComponent<Image>(), text = text };
    }

    private static TMP_Text CreateText(Transform parent, float size, TextAlignmentOptions alignment)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.alignment = alignment;
        text.richText = true;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private static string StripTags(string text) =>
        System.Text.RegularExpressions.Regex.Replace(text, "<.*?>", string.Empty);
}
