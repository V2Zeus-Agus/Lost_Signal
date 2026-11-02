using UnityEngine;

public class OxygenShop : MonoBehaviour, IInteractable
{
    [SerializeField] private PlayerOxygen playerOxygen;
    [SerializeField] private ResourceType costType;
    [SerializeField] private int costPerUpgrade = 3;
    [SerializeField] private float secondsPerUpgrade = 8f;
    [SerializeField] private int maxUpgrades = 5;

    private int upgradesBought;

    private bool IsMaxed => upgradesBought >= maxUpgrades;

    public string GetPrompt() =>
        IsMaxed
            ? "Tanque de O2 al máximo"
            : $"[E] Mejorar tanque de O2 (+{secondsPerUpgrade:0} s) · cuesta {costPerUpgrade} {costType.displayName}";

    public void Interact(Inventory inventory)
    {
        if (IsMaxed) return;

        if (inventory.GetAmount(costType) < costPerUpgrade)
        {
            NotificationQueue.Enqueue($"Necesitás {costPerUpgrade} {costType.displayName}");
            return;
        }

        inventory.Remove(costType, costPerUpgrade);
        upgradesBought++;
        playerOxygen.UpgradeCapacity(secondsPerUpgrade);
        NotificationQueue.Enqueue($"Tanque de O2: {playerOxygen.Max:0} s");
    }
}
