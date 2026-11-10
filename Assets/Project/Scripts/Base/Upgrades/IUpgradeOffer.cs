using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Una mejora que se vende en la tienda. Cada tipo (tanque, mochila, traje) calcula su
/// nivel, efecto y costo a su manera, pero la tienda y su pantalla las tratan igual (Strategy).
/// </summary>
public interface IUpgradeOffer
{
    string UpgradeId { get; }
    string Title { get; }
    int Level { get; }
    int MaxLevel { get; }
    bool IsMaxed { get; }
    /// <summary>Qué tenés hoy, p. ej. "Tanque: 62 s".</summary>
    string CurrentEffect { get; }
    /// <summary>Qué te da el próximo nivel, p. ej. "+25 s de O2".</summary>
    string NextEffect { get; }
    IReadOnlyList<ItemAmount> NextCost { get; }
}

/// <summary>Un nivel de una mejora con lista de costos (mochila, traje).</summary>
[Serializable]
public class UpgradeLevel
{
    [Tooltip("Qué gana el jugador en este nivel, p. ej. \"+10 items (40)\".")]
    public string description = "+1";
    public List<ItemAmount> cost = new List<ItemAmount>();
}

/// <summary>Mejora del tanque de O2: costo y segundos salen de OxygenTankProgression.</summary>
public class OxygenTankOffer : IUpgradeOffer
{
    private readonly PlayerUpgrades upgrades;
    private readonly PlayerOxygen oxygen;
    private readonly ItemDefinition costItem;
    private readonly int earlyCost;
    private readonly int scaledBaseCost;
    private readonly List<ItemAmount> cost = new List<ItemAmount>();

    public OxygenTankOffer(PlayerUpgrades upgrades, PlayerOxygen oxygen, ItemDefinition costItem, int earlyCost, int scaledBaseCost)
    {
        this.upgrades = upgrades;
        this.oxygen = oxygen;
        this.costItem = costItem;
        this.earlyCost = earlyCost;
        this.scaledBaseCost = scaledBaseCost;
    }

    public string UpgradeId => UpgradeIds.OxygenTank;
    public string Title => "Tanque de O2";
    public int Level => upgrades != null ? upgrades.GetLevel(UpgradeId) : 0;
    public int MaxLevel => OxygenTankProgression.MaxLevel;
    public bool IsMaxed => Level >= MaxLevel;
    public string CurrentEffect => oxygen != null ? $"Tanque: {oxygen.Max:0} s de O2" : "";
    public string NextEffect => oxygen != null ? $"+{oxygen.NextLevelSeconds:0} s de O2" : "";

    public IReadOnlyList<ItemAmount> NextCost
    {
        get
        {
            cost.Clear();
            if (!IsMaxed && costItem != null)
                cost.Add(new ItemAmount { item = costItem, amount = OxygenTankProgression.CostForLevel(Level + 1, earlyCost, scaledBaseCost) });
            return cost;
        }
    }
}

/// <summary>Mejora con una lista fija de niveles (mochila, traje).</summary>
public class LevelListOffer : IUpgradeOffer
{
    private readonly PlayerUpgrades upgrades;
    private readonly List<UpgradeLevel> levels;
    private readonly Func<int, string> currentEffect;
    private static readonly List<ItemAmount> NoCost = new List<ItemAmount>();

    public LevelListOffer(PlayerUpgrades upgrades, string upgradeId, string title, List<UpgradeLevel> levels, Func<int, string> currentEffect)
    {
        this.upgrades = upgrades;
        UpgradeId = upgradeId;
        Title = title;
        this.levels = levels ?? new List<UpgradeLevel>();
        this.currentEffect = currentEffect;
    }

    public string UpgradeId { get; }
    public string Title { get; }
    public int Level => upgrades != null ? upgrades.GetLevel(UpgradeId) : 0;
    public int MaxLevel => levels.Count;
    public bool IsMaxed => Level >= MaxLevel;
    public string CurrentEffect => currentEffect != null ? currentEffect(Level) : "";
    public string NextEffect => IsMaxed ? "" : levels[Level].description;
    public IReadOnlyList<ItemAmount> NextCost => IsMaxed ? NoCost : levels[Level].cost;
}
