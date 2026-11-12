using UnityEngine;

/// <summary>
/// Terminal de mejoras construible: abre la misma tienda de la nave (mismas mejoras, mismos
/// costos y el mismo progreso), así el jugador progresa desde su base sin volver a la nave.
/// </summary>
public class UpgradeTerminal : MonoBehaviour, IInteractable
{
    private UpgradeShop shop;

    private UpgradeShop Shop
    {
        get
        {
            // La tienda de la nave tiene los datos (niveles y costos): la terminal solo la abre.
            if (shop == null) shop = FindFirstObjectByType<UpgradeShop>();
            return shop;
        }
    }

    public string GetPrompt() => Shop != null ? "[E] Terminal de mejoras" : "Terminal sin conexión con la nave";

    public void Interact(Inventory inventory)
    {
        if (Shop != null) UpgradeShopPanel.Show(Shop, inventory);
        else NotificationQueue.Enqueue("No hay tienda de mejoras en este planeta");
    }
}
