// Patrón Command: cada acción del inventario interactuable es un objeto con Execute().
// La UI no toca el contenedor directamente: arma el comando y lo ejecuta.
// Así las acciones se pueden reutilizar (atajos, mandos, consola) o encolar/deshacer más adelante.

public class MoveSlotCommand : ICommand
{
    private readonly ItemContainer container;
    private readonly int from;
    private readonly int to;

    public MoveSlotCommand(ItemContainer container, int from, int to)
    {
        this.container = container;
        this.from = from;
        this.to = to;
    }

    public void Execute() => container.MoveSlot(from, to);
}

public class SplitStackCommand : ICommand
{
    private readonly ItemContainer container;
    private readonly int slot;

    public SplitStackCommand(ItemContainer container, int slot)
    {
        this.container = container;
        this.slot = slot;
    }

    public void Execute()
    {
        if (!container.SplitSlot(slot))
            NotificationQueue.Enqueue("No se puede dividir: hace falta un slot libre y al menos 2 unidades");
    }
}

public class DiscardFromSlotCommand : ICommand
{
    private readonly ItemContainer container;
    private readonly int slot;
    private readonly int amount;

    public DiscardFromSlotCommand(ItemContainer container, int slot, int amount)
    {
        this.container = container;
        this.slot = slot;
        this.amount = amount;
    }

    public void Execute()
    {
        if (!container.IsValidSlot(slot)) return;
        ItemDefinition item = container.Slots[slot].item;
        int removed = container.RemoveFromSlot(slot, amount);
        if (removed > 0 && item != null)
            NotificationQueue.Enqueue($"Descartaste {removed} {item.displayName}");
    }
}
