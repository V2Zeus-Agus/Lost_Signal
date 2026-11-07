using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Estación genérica que vende niveles de una mejora persistente (PlayerUpgrades).
/// Cada nivel tiene su propia lista de costos, configurable desde el Inspector.
/// Hoy se usa para la mochila; sirve igual para el traje u otras mejoras futuras.
/// </summary>
public class UpgradeStation : MonoBehaviour, IInteractable
{
    [Serializable]
    public class LevelCost
    {
        [Tooltip("Qué se muestra en el cartel, por ejemplo: \"+10 items (40)\".")]
        public string description = "+1";
        public List<ItemAmount> cost = new List<ItemAmount>();
    }

    [SerializeField] private string upgradeId = UpgradeIds.Backpack;
    [SerializeField] private string displayName = "Mejorar mochila";
    [Tooltip("Un elemento por nivel: el primero es el nivel 1.")]
    [SerializeField] private List<LevelCost> levels = new List<LevelCost>();
    [SerializeField] private PlayerUpgrades playerUpgrades;

    private int Level => playerUpgrades != null ? playerUpgrades.GetLevel(upgradeId) : 0;
    private bool IsMaxed => Level >= levels.Count;
    private LevelCost Next => IsMaxed ? null : levels[Level];

    private void Awake()
    {
        if (playerUpgrades == null) playerUpgrades = FindFirstObjectByType<PlayerUpgrades>();
    }

    public string GetPrompt()
    {
        if (IsMaxed) return $"{displayName}: nivel máximo ({levels.Count})";
        return $"[E] {displayName} a nivel {Level + 1} ({Next.description}) · cuesta {FormatCost(Next.cost)}";
    }

    public void Interact(Inventory inventory)
    {
        if (IsMaxed) return;

        if (playerUpgrades == null)
        {
            Debug.LogError("[UpgradeStation] Falta PlayerUpgrades en el jugador.", this);
            return;
        }

        LevelCost next = Next;
        if (!inventory.Container.TryConsume(next.cost))
        {
            NotificationQueue.Enqueue($"Te falta: {FormatMissing(inventory, next.cost)}");
            return;
        }

        playerUpgrades.AddLevel(upgradeId);
        NotificationQueue.Enqueue($"{displayName}: nivel {Level} ({next.description})");
    }

    private static string FormatCost(List<ItemAmount> cost)
    {
        var sb = new StringBuilder();
        foreach (ItemAmount c in cost)
        {
            if (c.item == null) continue;
            if (sb.Length > 0) sb.Append(" + ");
            sb.Append(c.amount).Append(' ').Append(c.item.displayName);
        }
        return sb.ToString();
    }

    private static string FormatMissing(Inventory inventory, List<ItemAmount> cost)
    {
        var sb = new StringBuilder();
        foreach (ItemAmount c in cost)
        {
            if (c.item == null) continue;
            int missing = c.amount - inventory.GetAmount(c.item);
            if (missing <= 0) continue;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(missing).Append(' ').Append(c.item.displayName);
        }
        return sb.ToString();
    }
}
