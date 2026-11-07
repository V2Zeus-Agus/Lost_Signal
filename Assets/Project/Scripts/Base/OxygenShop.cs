using UnityEngine;

/// <summary>
/// Vende niveles de la mejora del tanque de O2 (hasta nivel 10, ver OxygenTankProgression).
/// El nivel vive en PlayerUpgrades (perfil), así que la mejora se conserva al cambiar de planeta.
/// </summary>
public class OxygenShop : MonoBehaviour, IInteractable
{
    [SerializeField] private PlayerOxygen playerOxygen;
    [SerializeField] private PlayerUpgrades playerUpgrades;
    [SerializeField] private ItemDefinition costType;
    [Tooltip("Costo de los niveles 1-4.")]
    [SerializeField] private int costPerUpgrade = 3;
    [Tooltip("Costo base desde el nivel 5; se multiplica x1.3 por cada nivel.")]
    [SerializeField] private int scaledBaseCost = OxygenTankProgression.DefaultScaledBaseCost;

    private int Level => playerUpgrades != null ? playerUpgrades.GetLevel(UpgradeIds.OxygenTank) : 0;
    private bool IsMaxed => Level >= OxygenTankProgression.MaxLevel;
    private int NextCost => OxygenTankProgression.CostForLevel(Level + 1, costPerUpgrade, scaledBaseCost);

    private void Awake()
    {
        if (playerUpgrades == null && playerOxygen != null)
            playerUpgrades = playerOxygen.GetComponent<PlayerUpgrades>();
    }

    public string GetPrompt() =>
        IsMaxed
            ? $"Tanque de O2 al máximo (nivel {OxygenTankProgression.MaxLevel})"
            : $"[E] Mejorar tanque de O2 a nivel {Level + 1} (+{playerOxygen.NextLevelSeconds:0} s) · cuesta {NextCost} {costType.displayName}";

    public void Interact(Inventory inventory)
    {
        if (IsMaxed) return;

        if (playerUpgrades == null)
        {
            Debug.LogError("[OxygenShop] Falta PlayerUpgrades en el jugador. Corré Lost Signal > Fase 0 > Configurar escena actual.", this);
            return;
        }

        int cost = NextCost;
        if (inventory.GetAmount(costType) < cost)
        {
            NotificationQueue.Enqueue($"Necesitás {cost} {costType.displayName}");
            return;
        }

        inventory.Remove(costType, cost);
        playerUpgrades.AddLevel(UpgradeIds.OxygenTank);
        NotificationQueue.Enqueue($"Tanque de O2 nivel {Level}: {playerOxygen.Max:0} s");
    }
}
