using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RepairRequirement
{
    public ResourceType resourceType;
    public int amount = 5;
}

public class ShipRepair : MonoBehaviour
{
    [SerializeField] private List<RepairRequirement> requirements = new List<RepairRequirement>();

    private readonly Dictionary<ResourceType, int> delivered = new Dictionary<ResourceType, int>();

    public float Progress { get; private set; }
    public bool IsRepaired => Progress >= 1f;
    public IReadOnlyList<RepairRequirement> Requirements => requirements;

    public event Action<float> OnProgressChanged;
    public event Action OnRepaired;

    public int GetDelivered(ResourceType type) =>
        delivered.TryGetValue(type, out int amount) ? amount : 0;

    public void Deposit(Inventory inventory)
    {
        if (IsRepaired) return;

        int totalTaken = 0;
        foreach (RepairRequirement requirement in requirements)
        {
            int missing = requirement.amount - GetDelivered(requirement.resourceType);
            if (missing <= 0) continue;

            int taken = inventory.Remove(requirement.resourceType, missing);
            delivered[requirement.resourceType] = GetDelivered(requirement.resourceType) + taken;
            totalTaken += taken;
        }

        if (totalTaken == 0)
        {
            NotificationQueue.Enqueue("No tenés recursos que la nave necesite");
            return;
        }

        UpdateProgress();
        NotificationQueue.Enqueue($"Nave reparada al {Mathf.RoundToInt(Progress * 100f)}%");

        if (IsRepaired) OnRepaired?.Invoke();
    }

    private void UpdateProgress()
    {
        int needed = 0;
        int done = 0;
        foreach (RepairRequirement requirement in requirements)
        {
            needed += requirement.amount;
            done += Mathf.Min(GetDelivered(requirement.resourceType), requirement.amount);
        }

        Progress = needed > 0 ? (float)done / needed : 1f;
        OnProgressChanged?.Invoke(Progress);
    }
}
