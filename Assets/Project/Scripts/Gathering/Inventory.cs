using System;
using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    private readonly Dictionary<ResourceType, int> amounts = new Dictionary<ResourceType, int>();

    public event Action<ResourceType, int> OnResourceChanged;

    public int GetAmount(ResourceType type) =>
        amounts.TryGetValue(type, out int amount) ? amount : 0;

    public void Add(ResourceType type, int amount)
    {
        amounts[type] = GetAmount(type) + amount;
        OnResourceChanged?.Invoke(type, amounts[type]);
    }

    public int Remove(ResourceType type, int amount)
    {
        int removed = Mathf.Min(amount, GetAmount(type));
        if (removed <= 0) return 0;

        amounts[type] -= removed;
        OnResourceChanged?.Invoke(type, amounts[type]);
        return removed;
    }
}
