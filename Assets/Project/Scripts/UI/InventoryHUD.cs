using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel de mochila (abajo a la izquierda): slots usados y cantidad de cada item.
/// Se arma en runtime, no hace falta tocar el Canvas del HUD.
/// </summary>
public class InventoryHUD : MonoBehaviour
{
    [SerializeField] private Inventory inventory;

    private Canvas canvas;
    private RectTransform panel;
    private TMP_Text text;
    private bool dirty = true;

    private void Awake()
    {
        if (inventory == null) inventory = FindFirstObjectByType<Inventory>();
        BuildUI();
    }

    private void OnEnable()
    {
        if (inventory != null)
        {
            inventory.OnResourceChanged += HandleChanged;
            inventory.OnCapacityChanged += HandleCapacityChanged;
        }
        dirty = true;
    }

    private void OnDisable()
    {
        if (inventory == null) return;
        inventory.OnResourceChanged -= HandleChanged;
        inventory.OnCapacityChanged -= HandleCapacityChanged;
    }

    private void HandleChanged(ItemDefinition item, int total) => dirty = true;
    private void HandleCapacityChanged() => dirty = true;

    private void LateUpdate()
    {
        bool playing = GameManager.Instance != null && GameManager.Instance.IsPlaying;
        canvas.enabled = playing;
        if (!playing || !dirty || inventory == null) return;

        dirty = false;
        Refresh();
    }

    private void Refresh()
    {
        ItemContainer container = inventory.Container;
        var totals = new List<KeyValuePair<ItemDefinition, int>>();
        int usedSlots = 0;

        foreach (ItemStack slot in container.Slots)
        {
            if (slot.IsEmpty) continue;
            usedSlots++;
            int index = totals.FindIndex(p => p.Key == slot.item);
            if (index < 0) totals.Add(new KeyValuePair<ItemDefinition, int>(slot.item, slot.amount));
            else totals[index] = new KeyValuePair<ItemDefinition, int>(slot.item, totals[index].Value + slot.amount);
        }

        var sb = new StringBuilder();
        int totalItems = container.TotalCount;
        bool full = container.HasTotalLimit ? totalItems >= container.MaxTotalItems : usedSlots >= container.SlotCount;
        string capacityColor = full ? "#ff6655" : "#bbbbbb";
        string capacity = container.HasTotalLimit
            ? $"{totalItems}/{container.MaxTotalItems} items"
            : $"{usedSlots}/{container.SlotCount} slots";
        sb.Append("<b>MOCHILA</b>  <size=20><color=").Append(capacityColor).Append('>')
          .Append(capacity).AppendLine("</color></size>");

        if (totals.Count == 0)
            sb.Append("<size=20><color=#999999>Vacía</color></size>");

        foreach (KeyValuePair<ItemDefinition, int> pair in totals)
        {
            sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(pair.Key.uiColor)).Append('>')
              .Append(pair.Key.displayName).Append("</color>: ").Append(pair.Value).AppendLine();
        }

        text.text = sb.ToString().TrimEnd();
        panel.sizeDelta = new Vector2(300f, text.preferredHeight + 24f);
    }

    private void BuildUI()
    {
        var canvasGo = new GameObject("InventoryHUDCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -1;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var panelGo = new GameObject("Backpack", typeof(RectTransform), typeof(Image));
        panelGo.transform.SetParent(canvasGo.transform, false);
        panel = (RectTransform)panelGo.transform;
        panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.zero;
        panel.anchoredPosition = new Vector2(24f, 24f);
        panel.sizeDelta = new Vector2(300f, 80f);
        panelGo.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.06f, 0.55f);

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(panelGo.transform, false);
        text = textGo.GetComponent<TextMeshProUGUI>();
        text.fontSize = 24;
        text.richText = true;
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.TopLeft;
        RectTransform rt = text.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(16f, 12f);
        rt.offsetMax = new Vector2(-16f, -12f);
    }
}
