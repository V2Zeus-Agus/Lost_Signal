using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Al morir: los recursos escasos (poco común en adelante) de la mochila quedan en un cofre
/// donde caíste y los comunes se pierden. Después se guarda la partida, así reiniciar no
/// devuelve la mochila de antes. Los cofres se guardan con el planeta (ISaveable) hasta vaciarlos.
/// Patrón Observer: escucha GameManager.OnPlayerDefeated.
/// </summary>
[RequireComponent(typeof(SaveableEntity))]
public class DeathChestManager : MonoBehaviour, ISaveable
{
    [SerializeField] private Inventory playerInventory;

    private readonly List<DeathChest> chests = new List<DeathChest>();

    public IReadOnlyList<DeathChest> Chests => chests;

    private void Awake()
    {
        if (playerInventory == null) playerInventory = FindFirstObjectByType<Inventory>();
    }

    private void Start()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnPlayerDefeated += HandlePlayerDefeated;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnPlayerDefeated -= HandlePlayerDefeated;
    }

    private void HandlePlayerDefeated()
    {
        if (playerInventory == null) return;

        ItemContainer backpack = playerInventory.Container;
        DeathChest chest = null;
        int saved = 0, lost = 0;

        foreach (ItemStack stack in backpack.Slots)
        {
            if (stack.IsEmpty) continue;
            if (stack.item.IsScarce)
            {
                if (chest == null) chest = Spawn(GroundBelow(playerInventory.transform.position));
                saved += chest.Container.Add(stack.item, stack.amount);
            }
            else lost += stack.amount;
        }
        backpack.Clear();

        if (GameManager.Instance != null)
        {
            string extra = saved > 0
                ? $"\n\nTus recursos escasos ({saved}) quedaron en un cofre donde caíste: seguí la luz naranja."
                : string.Empty;
            if (lost > 0) extra += $"\nPerdiste {lost} recursos comunes.";
            GameManager.Instance.SetDefeatCause(GameManager.Instance.DefeatTitle, GameManager.Instance.DefeatMessage + extra);
        }

        // La muerte queda guardada: al reiniciar aparecés en la nave con la mochila vacía y el cofre en el mapa.
        if (SaveManager.Instance != null) SaveManager.Instance.SaveGame();
    }

    public DeathChest Spawn(Vector3 position)
    {
        var go = new GameObject("DeathChest");
        go.transform.SetParent(transform);
        go.transform.position = position;
        DeathChest chest = go.AddComponent<DeathChest>();
        chest.Init(this);
        chest.BuildVisual();
        chests.Add(chest);
        return chest;
    }

    public void Remove(DeathChest chest)
    {
        chests.Remove(chest);
        if (chest != null) Destroy(chest.gameObject);
    }

    private static Vector3 GroundBelow(Vector3 position)
    {
        if (Physics.Raycast(position + Vector3.up, Vector3.down, out RaycastHit hit, 50f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point;
        return position;
    }

    // ---------- Guardado ----------

    [Serializable]
    private class ChestRecord
    {
        public float x, y, z;
        public ContainerData items = new ContainerData();
    }

    [Serializable]
    private class State
    {
        public List<ChestRecord> chests = new List<ChestRecord>();
    }

    public string CaptureState()
    {
        var state = new State();
        foreach (DeathChest chest in chests)
        {
            if (chest == null || chest.Container.TotalCount == 0) continue;
            Vector3 p = chest.transform.position;
            state.chests.Add(new ChestRecord { x = p.x, y = p.y, z = p.z, items = chest.Container.ToData() });
        }
        return JsonUtility.ToJson(state);
    }

    public void RestoreState(string json, ItemDatabase database)
    {
        foreach (DeathChest chest in chests)
            if (chest != null) Destroy(chest.gameObject);
        chests.Clear();

        State state = JsonUtility.FromJson<State>(json);
        if (state?.chests == null) return;
        foreach (ChestRecord record in state.chests)
        {
            DeathChest chest = Spawn(new Vector3(record.x, record.y, record.z));
            chest.Container.LoadData(record.items, database);
        }

        if (chests.Count > 0)
            NotificationQueue.Enqueue(chests.Count == 1
                ? "Tenés un cofre con recursos donde caíste (luz naranja)"
                : $"Tenés {chests.Count} cofres con recursos donde caíste (luz naranja)");
    }
}
