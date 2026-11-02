using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ResourceReward
{
    public ResourceType resourceType;
    public int amount;
}

public class SalvageWreck : MonoBehaviour, IInteractable
{
    [SerializeField] private List<ResourceReward> rewards = new List<ResourceReward>();

    private bool salvaged;

    public string GetPrompt() =>
        salvaged ? "Ya no quedan partes útiles" : "[E] Extraer partes de la nave rota";

    public void Interact(Inventory inventory)
    {
        if (salvaged) return;
        salvaged = true;

        foreach (ResourceReward reward in rewards)
        {
            inventory.Add(reward.resourceType, reward.amount);
            NotificationQueue.Enqueue($"+{reward.amount} {reward.resourceType.displayName}");
        }
    }
}
