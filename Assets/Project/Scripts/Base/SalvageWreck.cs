using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ResourceReward
{
    public ItemDefinition resourceType;
    public int amount;
}

public class SalvageWreck : MonoBehaviour, IInteractable, ISaveable
{
    [SerializeField] private List<ResourceReward> rewards = new List<ResourceReward>();

    private bool salvaged;

    public bool IsSalvaged => salvaged;

    public string GetPrompt() =>
        salvaged ? "Ya no quedan partes útiles" : "[E] Extraer partes de la nave rota";

    public void Interact(Inventory inventory)
    {
        if (salvaged) return;

        // Todo o nada: si no entra todo en la mochila, no se pierde nada.
        if (!FitsInto(inventory))
        {
            NotificationQueue.Enqueue("No tenés espacio en la mochila para todas las partes");
            return;
        }

        salvaged = true;
        foreach (ResourceReward reward in rewards)
        {
            inventory.Add(reward.resourceType, reward.amount);
            NotificationQueue.Enqueue($"+{reward.amount} {reward.resourceType.displayName}");
        }
    }

    private bool FitsInto(Inventory inventory)
    {
        // Prueba en una copia para considerar que varios premios compiten por los mismos slots.
        var test = new ItemContainer(inventory.Container.SlotCount, inventory.Container.MaxTotalItems);
        foreach (ItemStack slot in inventory.Container.Slots)
            if (!slot.IsEmpty) test.Add(slot.item, slot.amount);

        foreach (ResourceReward reward in rewards)
            if (test.Add(reward.resourceType, reward.amount) < reward.amount) return false;

        return true;
    }

    // ---------- Guardado ----------

    [Serializable]
    private class State
    {
        public bool salvaged;
    }

    public string CaptureState() => JsonUtility.ToJson(new State { salvaged = salvaged });

    public void RestoreState(string json, ItemDatabase database) =>
        salvaged = JsonUtility.FromJson<State>(json).salvaged;
}
