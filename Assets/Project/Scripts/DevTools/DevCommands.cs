using UnityEngine;

// Comandos de la consola de desarrollador (patrón Command, igual que Fire/Interact).

/// <summary>Da una cantidad de un item a la mochila, respetando (o no) el tope de items.</summary>
public class GiveItemCommand : ICommand
{
    private readonly Inventory inventory;
    private readonly ItemDefinition item;
    private readonly int amount;

    public int Added { get; private set; }

    public GiveItemCommand(Inventory inventory, ItemDefinition item, int amount)
    {
        this.inventory = inventory;
        this.item = item;
        this.amount = amount;
    }

    public void Execute()
    {
        if (inventory == null || item == null || amount <= 0) return;
        Added = inventory.Add(item, amount);
    }
}

/// <summary>Carga en la mochila lo que le falta a la nave (lo que antes hacía F8).</summary>
public class GiveRepairMaterialsCommand : ICommand
{
    private readonly Inventory inventory;
    private readonly ShipRepair shipRepair;

    public GiveRepairMaterialsCommand(Inventory inventory, ShipRepair shipRepair)
    {
        this.inventory = inventory;
        this.shipRepair = shipRepair;
    }

    public void Execute()
    {
        if (inventory == null || shipRepair == null || shipRepair.IsRepaired) return;
        foreach (RepairRequirement requirement in shipRepair.Requirements)
        {
            int missing = requirement.amount
                          - shipRepair.GetDelivered(requirement.resourceType)
                          - inventory.GetAmount(requirement.resourceType);
            if (missing > 0) inventory.Add(requirement.resourceType, missing);
        }
    }
}

/// <summary>Teletransporta al jugador a un punto del mapa (apoyado sobre el suelo).</summary>
public class TeleportCommand : ICommand
{
    private readonly Transform player;
    private readonly Vector3 flatTarget;

    public TeleportCommand(Transform player, Vector3 flatTarget)
    {
        this.player = player;
        this.flatTarget = flatTarget;
    }

    public void Execute()
    {
        if (player == null) return;
        Vector3 target = flatTarget + Vector3.up * 2f;
        if (Physics.Raycast(new Vector3(flatTarget.x, 1000f, flatTarget.z), Vector3.down, out RaycastHit hit, 2000f, ~0, QueryTriggerInteraction.Ignore))
            target = hit.point + Vector3.up * 1.2f;

        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        player.position = target;
        if (controller != null) controller.enabled = true;
    }
}
