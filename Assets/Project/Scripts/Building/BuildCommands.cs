using UnityEngine;

// Patrón Command: construir, desarmar y mover items entre contenedores.

/// <summary>Cobra el costo y construye la pieza.</summary>
public class PlacePieceCommand : ICommand
{
    private readonly BaseBuildingManager manager;
    private readonly BuildableDefinition definition;
    private readonly Inventory inventory;
    private readonly Vector3 position;
    private readonly Quaternion rotation;

    public PlacedPiece Result { get; private set; }

    public PlacePieceCommand(BaseBuildingManager manager, BuildableDefinition definition, Inventory inventory, Vector3 position, Quaternion rotation)
    {
        this.manager = manager;
        this.definition = definition;
        this.inventory = inventory;
        this.position = position;
        this.rotation = rotation;
    }

    public void Execute()
    {
        if (manager == null || definition == null || inventory == null) return;
        if (!inventory.Container.TryConsume(definition.cost))
        {
            NotificationQueue.Enqueue("Te falta: " + CostText.Missing(inventory, definition.cost));
            return;
        }
        Result = manager.Place(definition, position, rotation);
        NotificationQueue.Enqueue($"Construiste: {definition.displayName}");
    }
}

/// <summary>Desarma una pieza y devuelve todo su costo a la mochila (si entra).</summary>
public class RemovePieceCommand : ICommand
{
    private readonly BaseBuildingManager manager;
    private readonly PlacedPiece piece;
    private readonly Inventory inventory;

    public RemovePieceCommand(BaseBuildingManager manager, PlacedPiece piece, Inventory inventory)
    {
        this.manager = manager;
        this.piece = piece;
        this.inventory = inventory;
    }

    public void Execute()
    {
        if (manager == null || piece == null || inventory == null || piece.Definition == null) return;
        if (!piece.CanRemove(out string reason))
        {
            NotificationQueue.Enqueue(reason);
            return;
        }

        // Una fábrica devuelve también lo que tenía producido.
        piece.TryGetComponent(out Factory factory);
        int produced = factory != null && factory.Item != null ? factory.Stored : 0;

        int needed = produced;
        foreach (ItemAmount c in piece.Definition.cost) if (c.item != null) needed += c.amount;
        int free = inventory.Container.HasTotalLimit ? inventory.MaxItems - inventory.TotalItems : int.MaxValue;
        if (needed > free)
        {
            NotificationQueue.Enqueue($"Hacé lugar en la mochila: devuelve {needed} items y tenés {free} libres");
            return;
        }

        foreach (ItemAmount c in piece.Definition.cost)
            if (c.item != null) inventory.Add(c.item, c.amount);
        if (produced > 0)
        {
            inventory.Add(factory.Item, produced);
            NotificationQueue.Enqueue($"+{produced} {factory.Item.displayName} que tenía la fábrica");
        }
        NotificationQueue.Enqueue($"Desarmaste: {piece.Definition.displayName} (+{CostText.Format(piece.Definition.cost)})");
        manager.Remove(piece);
    }
}

/// <summary>Pasa una pila de un contenedor a otro (lo que entre).</summary>
public class TransferStackCommand : ICommand
{
    private readonly ItemContainer from;
    private readonly ItemContainer to;
    private readonly int slot;

    public int Moved { get; private set; }

    public TransferStackCommand(ItemContainer from, ItemContainer to, int slot)
    {
        this.from = from;
        this.to = to;
        this.slot = slot;
    }

    public void Execute()
    {
        if (from == null || to == null || !from.IsValidSlot(slot)) return;
        ItemStack stack = from.Slots[slot];
        if (stack.IsEmpty) return;

        Moved = to.Add(stack.item, stack.amount);
        if (Moved > 0) from.RemoveFromSlot(slot, Moved);
        else NotificationQueue.Enqueue("No entra: no hay lugar");
    }
}
