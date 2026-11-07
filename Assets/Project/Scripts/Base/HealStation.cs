using System;
using UnityEngine;

public class HealStation : MonoBehaviour, IInteractable, ISaveable
{
    [SerializeField] private Health playerHealth;
    [SerializeField] private int maxUses = 2;

    private int uses;

    public int UsesLeft => maxUses - uses;

    public string GetPrompt() =>
        UsesLeft > 0 ? $"[E] Curarte ({UsesLeft} restantes)" : "Estación médica agotada";

    public void Interact(Inventory inventory)
    {
        if (UsesLeft <= 0) return;

        if (playerHealth.Current >= playerHealth.Max)
        {
            NotificationQueue.Enqueue("Ya tenés la vida completa");
            return;
        }

        uses++;
        playerHealth.RestoreFull();
        NotificationQueue.Enqueue($"Vida restaurada · quedan {UsesLeft} curaciones");
    }

    // ---------- Guardado ----------

    [Serializable]
    private class State
    {
        public int uses;
    }

    public string CaptureState() => JsonUtility.ToJson(new State { uses = uses });

    public void RestoreState(string json, ItemDatabase database) =>
        uses = JsonUtility.FromJson<State>(json).uses;
}
