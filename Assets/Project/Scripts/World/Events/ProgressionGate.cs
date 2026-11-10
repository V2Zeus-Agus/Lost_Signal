using UnityEngine;

/// <summary>
/// ¿El jugador ya está avanzado? Lo usan los eventos más peligrosos (meteoritos).
/// Avanzado = suma de niveles de mejoras (tanque + mochila + traje) >= 6, o la nave al 50%.
/// </summary>
public static class ProgressionGate
{
    public const int AdvancedUpgradeLevels = 6;
    public const float AdvancedShipProgress = 0.5f;

    public static int TotalUpgradeLevels(PlayerUpgrades upgrades)
    {
        if (upgrades == null) return 0;
        return upgrades.GetLevel(UpgradeIds.OxygenTank) + upgrades.GetLevel(UpgradeIds.Backpack) + upgrades.GetLevel(UpgradeIds.Suit);
    }

    public static bool IsAdvanced(PlayerUpgrades upgrades, ShipRepair ship) =>
        TotalUpgradeLevels(upgrades) >= AdvancedUpgradeLevels || (ship != null && ship.Progress >= AdvancedShipProgress);
}
