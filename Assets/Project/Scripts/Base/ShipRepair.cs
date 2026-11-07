using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RepairRequirement
{
    public ItemDefinition resourceType;
    public int amount = 5;
}

public class ShipRepair : MonoBehaviour, ISaveable
{
    [SerializeField] private List<RepairRequirement> requirements = new List<RepairRequirement>();

    private readonly Dictionary<ItemDefinition, int> delivered = new Dictionary<ItemDefinition, int>();

    public float Progress { get; private set; }
    public bool IsRepaired => Progress >= 1f;
    public IReadOnlyList<RepairRequirement> Requirements => requirements;

    public event Action<float> OnProgressChanged;
    public event Action OnRepaired;

    public int GetDelivered(ItemDefinition type) =>
        type != null && delivered.TryGetValue(type, out int amount) ? amount : 0;

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

    // ---------- Guardado ----------

    [Serializable]
    private class State
    {
        public List<ItemStackData> delivered = new List<ItemStackData>();
    }

    public string CaptureState()
    {
        var state = new State();
        foreach (KeyValuePair<ItemDefinition, int> pair in delivered)
            if (pair.Key != null && pair.Value > 0)
                state.delivered.Add(new ItemStackData { itemId = pair.Key.Id, amount = pair.Value });
        return JsonUtility.ToJson(state);
    }

    public void RestoreState(string json, ItemDatabase database)
    {
        delivered.Clear();
        State state = JsonUtility.FromJson<State>(json);
        if (database != null)
        {
            foreach (ItemStackData entry in state.delivered)
            {
                ItemDefinition item = database.Get(entry.itemId);
                if (item != null) delivered[item] = entry.amount;
            }
        }
        // Actualiza la barra, pero no dispara la victoria al cargar.
        UpdateProgress();
    }
}
