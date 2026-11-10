using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// La única tienda de mejoras: tanque de O2, mochila y traje. Al interactuar abre una pantalla
/// con todas las mejoras. Cada mejora es un IUpgradeOffer; la tienda solo las arma y las vende.
/// </summary>
public class UpgradeShop : MonoBehaviour, IInteractable
{
    [SerializeField] private string shopName = "Tienda de mejoras";
    [SerializeField] private PlayerUpgrades playerUpgrades;
    [SerializeField] private PlayerOxygen playerOxygen;

    [Header("Tanque de O2")]
    [SerializeField] private bool offerOxygen = true;
    [SerializeField] private ItemDefinition oxygenCostItem;
    [Tooltip("Costo de los niveles 1-4.")]
    [SerializeField] private int oxygenEarlyCost = 3;
    [Tooltip("Costo base desde el nivel 5; se multiplica x1.3 por nivel.")]
    [SerializeField] private int oxygenScaledBaseCost = OxygenTankProgression.DefaultScaledBaseCost;

    [Header("Mochila")]
    [SerializeField] private bool offerBackpack = true;
    [SerializeField] private List<UpgradeLevel> backpackLevels = new List<UpgradeLevel>();

    [Header("Traje")]
    [SerializeField] private bool offerSuit = true;
    [SerializeField] private List<UpgradeLevel> suitLevels = new List<UpgradeLevel>();

    private readonly List<IUpgradeOffer> offers = new List<IUpgradeOffer>();

    public string ShopName => shopName;
    public IReadOnlyList<IUpgradeOffer> Offers => offers;
    public PlayerUpgrades Upgrades => playerUpgrades;

    private void Awake()
    {
        if (playerUpgrades == null) playerUpgrades = FindFirstObjectByType<PlayerUpgrades>();
        if (playerOxygen == null) playerOxygen = FindFirstObjectByType<PlayerOxygen>();
        Inventory inventory = playerUpgrades != null ? playerUpgrades.GetComponent<Inventory>() : null;

        if (offerOxygen)
            offers.Add(new OxygenTankOffer(playerUpgrades, playerOxygen, oxygenCostItem, oxygenEarlyCost, oxygenScaledBaseCost));
        if (offerBackpack && backpackLevels.Count > 0)
            offers.Add(new LevelListOffer(playerUpgrades, UpgradeIds.Backpack, "Mochila", backpackLevels,
                level => inventory != null ? $"Mochila: {inventory.MaxItems} items" : $"Nivel {level}"));
        if (offerSuit && suitLevels.Count > 0)
            offers.Add(new LevelListOffer(playerUpgrades, UpgradeIds.Suit, "Traje", suitLevels,
                level => "Traje: " + SuitProgression.Describe(level)));
    }

    public string GetPrompt() => $"[E] Abrir {shopName.ToLowerInvariant()}";

    public void Interact(Inventory inventory) => UpgradeShopPanel.Show(this, inventory);

    public BuyUpgradeCommand Buy(IUpgradeOffer offer, Inventory inventory)
    {
        var command = new BuyUpgradeCommand(offer, inventory, playerUpgrades);
        command.Execute();
        if (!string.IsNullOrEmpty(command.Message)) NotificationQueue.Enqueue(command.Message);
        return command;
    }
}
