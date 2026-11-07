using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[Serializable]
public class ResourceReward
{
    public ItemDefinition resourceType;
    public int amount;
}

/// <summary>
/// Nave caída que se desarma a mano. Cada vez que interactuás te llevás todo lo que entre
/// en la mochila; lo que no entra queda en la nave para el próximo viaje (y se guarda).
/// </summary>
public class SalvageWreck : MonoBehaviour, IInteractable, ISaveable
{
    [SerializeField] private string displayName = "nave rota";
    [SerializeField] private List<ResourceReward> rewards = new List<ResourceReward>();

    // Cuánto queda de cada premio (mismo orden que 'rewards').
    private int[] remaining;

    public bool IsSalvaged
    {
        get
        {
            EnsureRemaining();
            foreach (int left in remaining) if (left > 0) return false;
            return true;
        }
    }

    public IReadOnlyList<ResourceReward> Rewards => rewards;

    public int GetRemaining(int rewardIndex)
    {
        EnsureRemaining();
        return rewardIndex >= 0 && rewardIndex < remaining.Length ? remaining[rewardIndex] : 0;
    }

    private void EnsureRemaining()
    {
        if (remaining != null && remaining.Length == rewards.Count) return;
        remaining = new int[rewards.Count];
        for (int i = 0; i < rewards.Count; i++) remaining[i] = Mathf.Max(rewards[i].amount, 0);
    }

    public string GetPrompt()
    {
        if (IsSalvaged) return $"La {displayName} ya no tiene partes útiles";

        var sb = new StringBuilder($"[E] Extraer partes de la {displayName} · quedan ");
        bool first = true;
        for (int i = 0; i < rewards.Count; i++)
        {
            if (remaining[i] <= 0 || rewards[i].resourceType == null) continue;
            if (!first) sb.Append(", ");
            sb.Append(remaining[i]).Append(' ').Append(rewards[i].resourceType.displayName);
            first = false;
        }
        return sb.ToString();
    }

    public void Interact(Inventory inventory)
    {
        if (IsSalvaged) return;

        int takenTotal = 0;
        for (int i = 0; i < rewards.Count; i++)
        {
            ItemDefinition item = rewards[i].resourceType;
            if (item == null || remaining[i] <= 0) continue;

            int taken = inventory.Add(item, remaining[i]);
            if (taken <= 0) continue;
            remaining[i] -= taken;
            takenTotal += taken;
            NotificationQueue.Enqueue($"+{taken} {item.displayName}");
        }

        if (takenTotal == 0)
            NotificationQueue.Enqueue("Mochila llena: dejá recursos en la nave y volvé");
        else if (!IsSalvaged)
            NotificationQueue.Enqueue("Quedan partes en la nave: volvé con la mochila vacía");
    }

    // ---------- Guardado ----------

    [Serializable]
    private class State
    {
        public bool salvaged;            // formato viejo (todo o nada)
        public List<int> remaining = new List<int>();
    }

    public string CaptureState()
    {
        EnsureRemaining();
        return JsonUtility.ToJson(new State { salvaged = IsSalvaged, remaining = new List<int>(remaining) });
    }

    public void RestoreState(string json, ItemDatabase database)
    {
        State state = JsonUtility.FromJson<State>(json);
        EnsureRemaining();
        for (int i = 0; i < remaining.Length; i++)
        {
            if (state.remaining != null && i < state.remaining.Count) remaining[i] = Mathf.Max(state.remaining[i], 0);
            else if (state.salvaged) remaining[i] = 0; // partida guardada con el formato viejo
        }
    }
}
