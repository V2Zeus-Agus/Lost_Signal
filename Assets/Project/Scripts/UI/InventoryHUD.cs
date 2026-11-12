using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Panel de mochila (abajo a la izquierda): barra de capacidad y una fila con ícono por item.
/// Vive en la columna inferior izquierda del HUDRoot.
/// </summary>
public class InventoryHUD : MonoBehaviour
{
    [SerializeField] private Inventory inventory;

    private TMP_Text capacity;
    private HUDBar capacityBar;
    private TMP_Text empty;
    private HUDLineList lines;
    private bool dirty = true;
    private readonly List<KeyValuePair<ItemDefinition, int>> totals = new List<KeyValuePair<ItemDefinition, int>>();

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
        if (!dirty || inventory == null) return;
        dirty = false;
        Refresh();
    }

    private void Refresh()
    {
        ItemContainer container = inventory.Container;
        totals.Clear();
        int usedSlots = 0;

        foreach (ItemStack slot in container.Slots)
        {
            if (slot.IsEmpty) continue;
            usedSlots++;
            int index = totals.FindIndex(p => p.Key == slot.item);
            if (index < 0) totals.Add(new KeyValuePair<ItemDefinition, int>(slot.item, slot.amount));
            else totals[index] = new KeyValuePair<ItemDefinition, int>(slot.item, totals[index].Value + slot.amount);
        }

        int used = container.HasTotalLimit ? container.TotalCount : usedSlots;
        int max = container.HasTotalLimit ? container.MaxTotalItems : container.SlotCount;
        float t = max > 0 ? (float)used / max : 0f;
        bool full = used >= max;
        capacity.text = $"{used} / {max} {(container.HasTotalLimit ? "items" : "slots")}";
        capacity.color = full ? HUDTheme.Bad : HUDTheme.Muted;
        capacityBar.Set(t, full ? HUDTheme.Bad : t >= 0.8f ? HUDTheme.Warn : HUDTheme.Teal);

        empty.gameObject.SetActive(totals.Count == 0);
        for (int i = 0; i < totals.Count; i++)
        {
            HUDLine line = lines.Get(i);
            line.SetItem(totals[i].Key);
            line.Left.text = totals[i].Key.displayName;
            line.Right.text = $"<b>{totals[i].Value}</b>";
        }
        lines.Trim(totals.Count);
    }

    private void BuildUI()
    {
        RectTransform panel = HUDTheme.Panel("Backpack", "Mochila", HUDTheme.Teal);
        HUDRoot.Instance.Add(HUDRoot.Instance.BottomLeft, panel, 0);

        HUDTheme.SplitRow(panel, 18f, 15f, out TMP_Text hint, out capacity);
        hint.text = "[Tab] abrir";
        hint.color = HUDTheme.Muted;
        capacityBar = new HUDBar(panel, 6f, HUDTheme.Teal);
        empty = HUDTheme.Label(panel, "Vacía", 16, HUDTheme.Muted);
        lines = new HUDLineList(panel, 22f, 17f);
    }
}
