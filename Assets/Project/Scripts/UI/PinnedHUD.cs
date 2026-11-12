using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Mejoras y construcciones fijadas (izquierda, debajo de los signos vitales): nombre,
/// próximo nivel y una ficha por material con tengo/necesito. Las mejoras que llegan al
/// máximo se sueltan solas. Observer de PinnedUpgrades, la mochila y las mejoras.
/// </summary>
public class PinnedHUD : MonoBehaviour
{
    private const float ChipIcon = 22f;
    private const float ChipFont = 16f;

    private RectTransform panel;
    private RectTransform content;
    private Inventory inventory;
    private PlayerUpgrades upgrades;
    private UpgradeShop shop;
    private bool dirty = true;
    private readonly List<string> toUnpin = new List<string>();

    public void Init(Inventory playerInventory)
    {
        inventory = playerInventory;
        upgrades = inventory != null ? inventory.GetComponent<PlayerUpgrades>() : null;
        if (upgrades == null) upgrades = FindFirstObjectByType<PlayerUpgrades>();
        shop = FindFirstObjectByType<UpgradeShop>();

        panel = HUDTheme.Panel("Pinned", "Fijados", HUDTheme.Teal);
        content = HUDTheme.Group(panel, 10f);
        HUDRoot.Instance.Add(HUDRoot.Instance.Left, panel, 10);

        PinnedUpgrades.OnChanged += MarkDirty;
        DevCheats.OnChanged += MarkDirty;
        if (inventory != null) inventory.OnResourceChanged += HandleInventory;
        if (upgrades != null) upgrades.OnUpgradeChanged += HandleUpgrade;
        if (BaseBuildingManager.Instance != null) BaseBuildingManager.Instance.OnBaseChanged += MarkDirty;
    }

    private void OnDestroy()
    {
        PinnedUpgrades.OnChanged -= MarkDirty;
        DevCheats.OnChanged -= MarkDirty;
        if (inventory != null) inventory.OnResourceChanged -= HandleInventory;
        if (upgrades != null) upgrades.OnUpgradeChanged -= HandleUpgrade;
        if (BaseBuildingManager.Instance != null) BaseBuildingManager.Instance.OnBaseChanged -= MarkDirty;
    }

    private void MarkDirty() => dirty = true;
    private void HandleInventory(ItemDefinition item, int amount) => dirty = true;
    private void HandleUpgrade(string id, int level) => dirty = true;

    private void LateUpdate()
    {
        if (panel == null || !dirty) return;
        dirty = false;
        Redraw();
    }

    private void Redraw()
    {
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            GameObject old = content.GetChild(i).gameObject;
            old.SetActive(false);
            Destroy(old);
        }
        toUnpin.Clear();
        // Activo mientras se arman las fichas: TMP no inicializa su fuente en objetos inactivos.
        panel.gameObject.SetActive(true);

        int shown = 0;
        foreach (string key in PinnedUpgrades.All)
        {
            if (TryResolve(key, out string title, out string level, out IReadOnlyList<ItemAmount> cost, out string status, out bool finished))
            {
                if (finished)
                {
                    toUnpin.Add(key);
                    NotificationQueue.Enqueue($"Mejora completa: {title}");
                    continue;
                }
                AddEntry(title, level, cost, status);
                shown++;
            }
        }
        foreach (string key in toUnpin) PinnedUpgrades.Unpin(key);

        panel.gameObject.SetActive(shown > 0);
    }

    private void AddEntry(string title, string level, IReadOnlyList<ItemAmount> cost, string status)
    {
        RectTransform card = HUDTheme.Group(content, 4f);
        HUDTheme.SplitRow(card, 24f, 18f, out TMP_Text left, out TMP_Text right);
        left.text = $"<b>{title}</b>  <color={HUDTheme.Hex(HUDTheme.Muted)}><size=15>{level}</size></color>";
        right.text = status;
        right.fontSize = 15f;

        RectTransform chips = HUDTheme.Row(card, ChipIcon);
        float width = HUDRoot.LeftWidth - 32f;
        float height = CostChips.Fill(chips, cost, inventory, ChipIcon, ChipFont, width);
        chips.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = Mathf.Max(height, ChipIcon);
    }

    private bool TryResolve(string key, out string title, out string level, out IReadOnlyList<ItemAmount> cost, out string status, out bool finished)
    {
        title = level = status = "";
        cost = null;
        finished = false;

        if (key.StartsWith("upgrade:"))
        {
            string id = key.Substring("upgrade:".Length);
            if (shop == null) return false;
            foreach (IUpgradeOffer offer in shop.Offers)
            {
                if (offer.UpgradeId != id) continue;
                title = offer.Title;
                if (offer.IsMaxed)
                {
                    finished = true;
                    return true;
                }
                level = $"Nv {offer.Level} → {offer.Level + 1}";
                cost = offer.NextCost;
                status = Status(cost, true);
                return true;
            }
            return false;
        }

        if (key.StartsWith("build:"))
        {
            BaseBuildingManager manager = BaseBuildingManager.Instance;
            if (manager == null || manager.Catalog == null) return false;
            BuildableDefinition definition = manager.Catalog.Get(key.Substring("build:".Length));
            if (definition == null) return false;
            title = definition.displayName;
            level = ""; // el nombre ya es largo: sin etiqueta para no pisar el estado
            cost = definition.cost;
            status = manager.IsUnlocked(definition)
                ? Status(cost, false)
                : $"<color={HUDTheme.Hex(HUDTheme.Warn)}>Bloqueada</color>";
            return true;
        }
        return false;
    }

    private string Status(IReadOnlyList<ItemAmount> cost, bool upgrade)
    {
        if (CostText.CanAfford(inventory, cost))
            return $"<color={HUDTheme.Hex(HUDTheme.Good)}>{(upgrade ? "¡Lista!" : "¡Podés!")}</color>";
        int missing = 0;
        foreach (ItemAmount c in cost)
            if (c.item != null) missing += Mathf.Max(0, c.amount - (inventory != null ? inventory.GetAmount(c.item) : 0));
        return $"<color={HUDTheme.Hex(HUDTheme.Muted)}>faltan {missing}</color>";
    }
}
