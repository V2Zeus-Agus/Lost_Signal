using UnityEngine;

public class CrashedShip : MonoBehaviour, IInteractable
{
    [SerializeField] private ShipRepair shipRepair;

    public string GetPrompt() =>
        shipRepair.IsRepaired
            ? "Nave reparada"
            : $"[E] Depositar recursos ({Mathf.RoundToInt(shipRepair.Progress * 100f)}%)";

    public void Interact(Inventory inventory) => shipRepair.Deposit(inventory);
}
