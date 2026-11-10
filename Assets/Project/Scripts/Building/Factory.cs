using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fábrica: extrae su recurso sin límite (las máquinas son infinitas, la mano no).
/// Produce con el TickManager (solo en tiempo jugado), guarda hasta su capacidad y
/// si hay un cofre a menos de 10 m le manda lo producido. Con E se recolecta.
/// </summary>
public class Factory : MonoBehaviour, IInteractable, IPieceState, ITickable
{
    private const float ChestRange = 10f;

    private BuildableDefinition definition;
    private Light statusLight;
    private Transform spinner;
    private float progress;

    public int Stored { get; private set; }
    public ItemDefinition Item => definition != null ? definition.producedItem : null;
    public int Capacity => definition != null ? definition.storageCapacity : 0;
    public bool IsFull => Stored >= Capacity;

    public void Init(BuildableDefinition buildable, Light light, Transform spinningPart)
    {
        definition = buildable;
        statusLight = light;
        spinner = spinningPart;
        RefreshLight();
    }

    private void OnEnable() => TickManager.Register(this);
    private void OnDisable() => TickManager.Unregister(this);

    public void Tick(float deltaTime)
    {
        if (Item == null) return;
        progress += deltaTime;
        if (progress < definition.secondsPerItem) return;
        progress -= definition.secondsPerItem;

        // Primero al cofre más cercano; si no hay o está lleno, a su depósito interno.
        if (TryPushToChest()) return;
        if (IsFull)
        {
            progress = definition.secondsPerItem; // queda lista, esperando lugar
            RefreshLight();
            return;
        }
        Stored++;
        RefreshLight();
    }

    private bool TryPushToChest()
    {
        if (BaseBuildingManager.Instance == null) return false;
        StorageContainer best = null;
        float bestDistance = ChestRange;
        foreach (PlacedPiece piece in BaseBuildingManager.Instance.Pieces)
        {
            if (piece == null || !piece.TryGetComponent(out StorageContainer storage)) continue;
            float d = Vector3.Distance(piece.transform.position, transform.position);
            if (d < bestDistance && storage.Container.GetSpaceFor(Item) > 0)
            {
                best = storage;
                bestDistance = d;
            }
        }
        return best != null && best.Container.Add(Item, 1) > 0;
    }

    private void Update()
    {
        if (spinner != null && !IsFull && GameManager.Instance != null && GameManager.Instance.IsPlaying)
            spinner.Rotate(0f, 120f * Time.deltaTime, 0f, Space.Self);
    }

    private void RefreshLight()
    {
        if (statusLight != null) statusLight.color = IsFull ? new Color(1f, 0.75f, 0.2f) : new Color(0.3f, 1f, 0.45f);
    }

    public string GetPrompt()
    {
        if (Item == null) return "";
        string rate = $"1 {Item.displayName} cada {definition.secondsPerItem:0} s";
        return Stored > 0
            ? $"[E] Recolectar {Stored} {Item.displayName} · {Stored}/{Capacity} · {rate}"
            : $"Fábrica de {Item.displayName} trabajando · {rate}";
    }

    public void Interact(Inventory inventory)
    {
        if (Item == null || Stored <= 0) return;
        int added = inventory.Add(Item, Stored);
        if (added <= 0)
        {
            NotificationQueue.Enqueue("Mochila llena");
            return;
        }
        Stored -= added;
        NotificationQueue.Enqueue($"+{added} {Item.displayName}");
        RefreshLight();
    }

    // ---------- Guardado ----------

    [Serializable]
    private class State
    {
        public int stored;
        public float progress;
    }

    public string CaptureExtra() => JsonUtility.ToJson(new State { stored = Stored, progress = progress });

    public void RestoreExtra(string json, ItemDatabase database)
    {
        if (string.IsNullOrEmpty(json)) return;
        State state = JsonUtility.FromJson<State>(json);
        Stored = Mathf.Clamp(state.stored, 0, Capacity);
        progress = state.progress;
        RefreshLight();
    }

    // ---------- Material del color del recurso ----------

    private static readonly Dictionary<ItemDefinition, Material> bandMaterials = new Dictionary<ItemDefinition, Material>();

    public static Material BandMaterial(ItemDefinition item)
    {
        if (item == null) return null;
        if (!bandMaterials.TryGetValue(item, out Material m) || m == null)
            bandMaterials[item] = m = RuntimeMaterials.Lit(item.uiColor, 2f);
        return m;
    }
}
