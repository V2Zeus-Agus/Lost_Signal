/// <summary>Comprar el próximo nivel de una mejora (patrón Command, igual que el resto de acciones).</summary>
public class BuyUpgradeCommand : ICommand
{
    private readonly IUpgradeOffer offer;
    private readonly Inventory inventory;
    private readonly PlayerUpgrades upgrades;

    public bool Succeeded { get; private set; }
    public string Message { get; private set; }

    public BuyUpgradeCommand(IUpgradeOffer offer, Inventory inventory, PlayerUpgrades upgrades)
    {
        this.offer = offer;
        this.inventory = inventory;
        this.upgrades = upgrades;
    }

    public void Execute()
    {
        Succeeded = false;
        if (offer == null || inventory == null || upgrades == null) return;
        if (offer.IsMaxed)
        {
            Message = $"{offer.Title}: ya está al máximo";
            return;
        }

        string next = offer.NextEffect;
        if (!DevCheats.InfiniteMaterials && !inventory.Container.TryConsume(offer.NextCost))
        {
            Message = "Te falta: " + CostText.Missing(inventory, offer.NextCost);
            return;
        }

        upgrades.AddLevel(offer.UpgradeId);
        Succeeded = true;
        Message = $"{offer.Title} nivel {offer.Level}: {next}";
    }
}
