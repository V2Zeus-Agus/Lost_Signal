using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Contenedor de items con slots y pilas máximas. Es una clase C# común (no MonoBehaviour)
/// para reutilizarla en la mochila, cofres, buffers de máquinas y la bodega de la nave.
/// </summary>
public class ItemContainer
{
    private readonly List<ItemStack> slots = new List<ItemStack>();
    private int maxTotalItems;

    /// <summary>Se dispara cuando cambia la cantidad total de un item (item, nuevo total).</summary>
    public event Action<ItemDefinition, int> OnItemChanged;

    /// <param name="maxTotalItems">Tope de items sumando todo el contenedor. 0 = sin tope.</param>
    public ItemContainer(int slotCount, int maxTotalItems = 0)
    {
        this.maxTotalItems = Mathf.Max(0, maxTotalItems);
        Resize(slotCount);
    }

    public int SlotCount => slots.Count;
    public int MaxTotalItems => maxTotalItems;

    /// <summary>Cambia el tope total (por ejemplo, al mejorar la mochila). No saca items si ya hay más.</summary>
    public void SetMaxTotalItems(int value) => maxTotalItems = Mathf.Max(0, value);
    public bool HasTotalLimit => maxTotalItems > 0;

    /// <summary>Cantidad total de items, sumando todos los slots.</summary>
    public int TotalCount
    {
        get
        {
            int total = 0;
            foreach (ItemStack slot in slots)
                if (!slot.IsEmpty) total += slot.amount;
            return total;
        }
    }

    private int RemainingTotal => HasTotalLimit ? Mathf.Max(maxTotalItems - TotalCount, 0) : int.MaxValue;
    public IReadOnlyList<ItemStack> Slots => slots;

    /// <summary>Agrega slots vacíos (por ejemplo, al mejorar la mochila). No achica.</summary>
    public void Resize(int slotCount)
    {
        while (slots.Count < slotCount) slots.Add(new ItemStack());
    }

    public int GetAmount(ItemDefinition item)
    {
        if (item == null) return 0;

        int total = 0;
        foreach (ItemStack slot in slots)
            if (slot.item == item) total += slot.amount;
        return total;
    }

    /// <summary>Cuántas unidades de este item entran todavía.</summary>
    public int GetSpaceFor(ItemDefinition item)
    {
        if (item == null) return 0;

        int space = 0;
        foreach (ItemStack slot in slots)
        {
            if (slot.IsEmpty) space += item.maxStack;
            else if (slot.item == item) space += Mathf.Max(item.maxStack - slot.amount, 0);
        }
        return Mathf.Min(space, RemainingTotal);
    }

    /// <summary>Agrega todo lo que entre. Devuelve cuánto se agregó realmente.</summary>
    public int Add(ItemDefinition item, int amount)
    {
        if (item == null || amount <= 0) return 0;

        amount = Mathf.Min(amount, RemainingTotal);
        if (amount <= 0) return 0;
        int remaining = amount;

        // Primero completa pilas existentes del mismo item.
        foreach (ItemStack slot in slots)
        {
            if (remaining <= 0) break;
            if (slot.IsEmpty || slot.item != item || slot.amount >= item.maxStack) continue;

            int put = Mathf.Min(remaining, item.maxStack - slot.amount);
            slot.amount += put;
            remaining -= put;
        }

        // Después usa slots vacíos.
        foreach (ItemStack slot in slots)
        {
            if (remaining <= 0) break;
            if (!slot.IsEmpty) continue;

            int put = Mathf.Min(remaining, item.maxStack);
            slot.item = item;
            slot.amount = put;
            remaining -= put;
        }

        int added = amount - remaining;
        if (added > 0) Notify(item);
        return added;
    }

    /// <summary>Saca hasta 'amount'. Devuelve cuánto se sacó realmente.</summary>
    public int Remove(ItemDefinition item, int amount)
    {
        if (item == null || amount <= 0) return 0;

        int remaining = amount;
        for (int i = slots.Count - 1; i >= 0 && remaining > 0; i--)
        {
            ItemStack slot = slots[i];
            if (slot.IsEmpty || slot.item != item) continue;

            int take = Mathf.Min(remaining, slot.amount);
            slot.amount -= take;
            remaining -= take;
            if (slot.amount <= 0) slot.Clear();
        }

        int removed = amount - remaining;
        if (removed > 0) Notify(item);
        return removed;
    }

    public bool Has(ItemDefinition item, int amount) => GetAmount(item) >= amount;

    public bool HasAll(IReadOnlyList<ItemAmount> costs)
    {
        if (costs == null) return true;
        foreach (ItemAmount cost in costs)
            if (cost.item != null && GetAmount(cost.item) < cost.amount) return false;
        return true;
    }

    /// <summary>Consume todos los costos o ninguno.</summary>
    public bool TryConsume(IReadOnlyList<ItemAmount> costs)
    {
        if (!HasAll(costs)) return false;
        foreach (ItemAmount cost in costs)
            if (cost.item != null) Remove(cost.item, cost.amount);
        return true;
    }

    public void Clear()
    {
        var touched = new HashSet<ItemDefinition>();
        foreach (ItemStack slot in slots)
        {
            if (!slot.IsEmpty) touched.Add(slot.item);
            slot.Clear();
        }
        foreach (ItemDefinition item in touched) Notify(item);
    }

    // ---------- Guardado ----------

    public ContainerData ToData()
    {
        var data = new ContainerData();
        for (int i = 0; i < slots.Count; i++)
        {
            ItemStack slot = slots[i];
            if (slot.IsEmpty) continue;
            data.stacks.Add(new ItemStackData { slot = i, itemId = slot.item.Id, amount = slot.amount });
        }
        return data;
    }

    public void LoadData(ContainerData data, ItemDatabase database)
    {
        var touched = new HashSet<ItemDefinition>();
        foreach (ItemStack slot in slots)
        {
            if (!slot.IsEmpty) touched.Add(slot.item);
            slot.Clear();
        }

        if (data != null && database != null)
        {
            foreach (ItemStackData stack in data.stacks)
            {
                ItemDefinition item = database.Get(stack.itemId);
                if (item == null || stack.amount <= 0) continue;

                if (stack.slot >= 0 && stack.slot < slots.Count && slots[stack.slot].IsEmpty)
                {
                    slots[stack.slot].item = item;
                    slots[stack.slot].amount = Mathf.Min(stack.amount, item.maxStack);
                }
                else
                {
                    Add(item, stack.amount); // el slot ya no existe: lo acomoda donde entre
                }
                touched.Add(item);
            }
        }

        foreach (ItemDefinition item in touched) Notify(item);
    }

    private void Notify(ItemDefinition item) => OnItemChanged?.Invoke(item, GetAmount(item));
}
