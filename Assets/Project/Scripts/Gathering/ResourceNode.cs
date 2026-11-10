using System;
using UnityEngine;

/// <summary>
/// Nodo de extracción MANUAL: es finito y no vuelve.
/// Las máquinas (Fase 4) van a usar yacimientos aparte, que son infinitos.
/// </summary>
public class ResourceNode : MonoBehaviour, IInteractable, ISaveable
{
    [SerializeField] private ItemDefinition resourceType;
    [SerializeField] private int amountPerHarvest = 1;
    [SerializeField] private int harvestsLeft = 1;

    public ItemDefinition Item => resourceType;
    public int HarvestsLeft => harvestsLeft;

    // Cuántas cosechas tenía al cargar la escena (antes de aplicar el guardado). Para el "% extraído".
    private int initialHarvests = -1;
    public int InitialHarvests => initialHarvests >= 0 ? initialHarvests : harvestsLeft;

    private void Awake()
    {
        if (initialHarvests < 0) initialHarvests = harvestsLeft;
    }

    public string GetPrompt() => $"[E] Recolectar {resourceType.displayName}";

    public void Interact(Inventory inventory)
    {
        if (inventory.GetSpaceFor(resourceType) < amountPerHarvest)
        {
            NotificationQueue.Enqueue(inventory.MaxItems > 0
                ? $"Mochila llena ({inventory.TotalItems}/{inventory.MaxItems} items)"
                : "Mochila llena");
            return;
        }

        inventory.Add(resourceType, amountPerHarvest);
        NotificationQueue.Enqueue($"+{amountPerHarvest} {resourceType.displayName}");

        harvestsLeft--;
        if (harvestsLeft <= 0)
            gameObject.SetActive(false);
    }

    // ---------- Guardado ----------

    [Serializable]
    private class State
    {
        public int harvestsLeft;
    }

    public string CaptureState() => JsonUtility.ToJson(new State { harvestsLeft = harvestsLeft });

    public void RestoreState(string json, ItemDatabase database)
    {
        State state = JsonUtility.FromJson<State>(json);
        if (initialHarvests < 0) initialHarvests = harvestsLeft;
        harvestsLeft = state.harvestsLeft;
        gameObject.SetActive(harvestsLeft > 0);
    }
}
