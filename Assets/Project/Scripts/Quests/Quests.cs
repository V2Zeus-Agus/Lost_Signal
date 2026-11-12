using System.Collections.Generic;
using UnityEngine;

/// <summary>Una línea dentro de un objetivo: un material (con ícono) o una tarea con casilla.</summary>
public struct QuestLine
{
    public ItemDefinition item;
    public bool isTask;
    public bool done;
    public string label;
    public string value;
}

/// <summary>
/// Un objetivo que se muestra arriba a la derecha. Cada tipo calcula su estado a su manera
/// y el panel los dibuja a todos igual (Strategy).
/// </summary>
public interface IQuest
{
    bool IsVisible { get; }
    bool IsComplete { get; }
    string Title { get; }
    string Hint { get; }
    /// <summary>0..1 para la barra; negativo = sin barra.</summary>
    float Progress { get; }
    void FillLines(List<QuestLine> lines);
}

/// <summary>Objetivo principal: llevar los materiales a la nave (reparar o despegar).</summary>
public class RepairShipQuest : IQuest
{
    private readonly ShipRepair ship;
    private readonly Inventory inventory;

    public RepairShipQuest(ShipRepair ship, Inventory inventory, string title)
    {
        this.ship = ship;
        this.inventory = inventory;
        Title = title;
    }

    public bool IsVisible => ship != null;
    public bool IsComplete => ship != null && ship.IsRepaired;
    public string Title { get; }
    public string Hint => IsComplete
        ? $"<color={HUDTheme.Hex(HUDTheme.Good)}>¡Nave lista! Subí para despegar.</color>"
        : "Llevá los materiales a la nave y apretá [E].";
    public float Progress => ship != null ? ship.Progress : 0f;

    public void FillLines(List<QuestLine> lines)
    {
        foreach (RepairRequirement requirement in ship.Requirements)
        {
            ItemDefinition item = requirement.resourceType;
            if (item == null) continue;
            int delivered = ship.GetDelivered(item);
            bool done = delivered >= requirement.amount;
            int carried = inventory != null ? inventory.GetAmount(item) : 0;

            string value = done
                ? $"<color={HUDTheme.Hex(HUDTheme.Good)}>{delivered}/{requirement.amount}</color>"
                : $"{delivered}/{requirement.amount}";
            if (!done && carried > 0) value = $"<color={HUDTheme.Hex(HUDTheme.Teal)}>+{carried}</color>  " + value;
            lines.Add(new QuestLine { item = item, label = item.displayName, value = value, done = done });
        }
    }
}

/// <summary>Construir la primera base con aire (hábitat + generador de O2 adentro).</summary>
public class BuildBaseQuest : IQuest
{
    public bool IsVisible => BaseBuildingManager.Instance != null && !IsComplete;
    public bool IsComplete => BaseBuildingManager.Instance != null && BaseBuildingManager.Instance.HasPressurizedBase;
    public string Title => "Construí tu base";
    public string Hint => "[B] Construir. Con aire se activa el autoguardado.";
    public float Progress => -1f;

    public void FillLines(List<QuestLine> lines)
    {
        bool door = false;
        foreach (Habitat h in Habitat.All) if (h != null && h.HasAnyDoor) door = true;
        lines.Add(new QuestLine { isTask = true, done = Habitat.All.Count > 0, label = "Hábitat" });
        lines.Add(new QuestLine { isTask = true, done = door, label = "Puerta (para poder entrar)" });
        lines.Add(new QuestLine { isTask = true, done = QuestUtil.HasPiece(BuildableKind.OxygenGenerator), label = "Generador de O2 adentro" });
    }
}

/// <summary>Automatizar: construir la primera fábrica (se habilita con base con aire).</summary>
public class BuildFactoryQuest : IQuest
{
    public bool IsVisible => BaseBuildingManager.Instance != null && BaseBuildingManager.Instance.HasPressurizedBase && !IsComplete;
    public bool IsComplete => QuestUtil.HasPiece(BuildableKind.Factory);
    public string Title => "Automatizá la extracción";
    public string Hint => "[B] Fábrica de Hierro o de Combustible.";
    public float Progress => -1f;

    public void FillLines(List<QuestLine> lines)
    {
        lines.Add(new QuestLine { isTask = true, done = false, label = "Construí una fábrica" });
    }
}

/// <summary>Mejorar el equipo en la tienda: una línea por mejora con su nivel.</summary>
public class UpgradeGearQuest : IQuest
{
    private readonly UpgradeShop shop;
    private readonly Inventory inventory;

    public UpgradeGearQuest(UpgradeShop shop, Inventory inventory)
    {
        this.shop = shop;
        this.inventory = inventory;
    }

    public bool IsVisible => shop != null && !IsComplete;

    public bool IsComplete
    {
        get
        {
            if (shop == null) return true;
            foreach (IUpgradeOffer offer in shop.Offers) if (!offer.IsMaxed) return false;
            return true;
        }
    }

    public string Title => "Mejorá tu equipo";
    public string Hint => "En la tienda podés fijar mejoras a la izquierda.";
    public float Progress => -1f;

    public void FillLines(List<QuestLine> lines)
    {
        foreach (IUpgradeOffer offer in shop.Offers)
        {
            string value;
            if (offer.IsMaxed) value = $"<color={HUDTheme.Hex(HUDTheme.Good)}>Máx.</color>";
            else if (CostText.CanAfford(inventory, offer.NextCost)) value = $"<color={HUDTheme.Hex(HUDTheme.Good)}>¡Lista!</color>  Nv {offer.Level}/{offer.MaxLevel}";
            else value = $"Nv {offer.Level}/{offer.MaxLevel}";
            lines.Add(new QuestLine { isTask = true, done = offer.IsMaxed, label = offer.Title, value = value });
        }
    }
}

public static class QuestUtil
{
    public static bool HasPiece(BuildableKind kind)
    {
        BaseBuildingManager manager = BaseBuildingManager.Instance;
        if (manager == null) return false;
        foreach (PlacedPiece piece in manager.Pieces)
            if (piece != null && piece.Definition != null && piece.Definition.kind == kind) return true;
        return false;
    }
}
