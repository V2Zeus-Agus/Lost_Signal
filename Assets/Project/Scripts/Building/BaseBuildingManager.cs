using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dueño de todo lo construido en el planeta: lo crea (con BuildPieceFactory), lo guarda
/// (ISaveable, con el planeta) y recalcula qué hábitats están unidos y cuáles tienen aire.
/// </summary>
[RequireComponent(typeof(SaveableEntity))]
public class BaseBuildingManager : MonoBehaviour, ISaveable
{
    public static BaseBuildingManager Instance { get; private set; }

    [SerializeField] private BuildCatalog catalog;

    private readonly List<PlacedPiece> pieces = new List<PlacedPiece>();

    public BuildCatalog Catalog => catalog;
    public IReadOnlyList<PlacedPiece> Pieces => pieces;

    public event Action OnBaseChanged;

    /// <summary>Hay al menos un hábitat con aire (con generador de O2). Desbloquea fábricas y el autoguardado.</summary>
    public bool HasPressurizedBase { get; private set; }

    private bool restoring;

    public bool IsUnlocked(BuildableDefinition definition) =>
        definition != null && (!definition.requiresPressurizedBase || HasPressurizedBase);

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public PlacedPiece Place(BuildableDefinition definition, Vector3 position, Quaternion rotation)
    {
        GameObject go = BuildPieceFactory.Create(definition, catalog, false);
        go.transform.SetParent(transform);
        go.transform.SetPositionAndRotation(position, rotation);
        PlacedPiece piece = go.GetComponent<PlacedPiece>();
        pieces.Add(piece);
        Physics.SyncTransforms();
        Recompute();
        return piece;
    }

    public void Remove(PlacedPiece piece)
    {
        if (piece == null) return;
        pieces.Remove(piece);
        // Que el hábitat deje de contar ya (Destroy recién actúa al final del frame).
        piece.gameObject.SetActive(false);
        Destroy(piece.gameObject);
        Recompute();
    }

    public bool HasPiecesInside(Habitat habitat)
    {
        foreach (PlacedPiece p in pieces)
            if (p != null && p.GetComponent<Habitat>() == null && habitat.Contains(p.transform.position + Vector3.up * 0.2f))
                return true;
        return false;
    }

    /// <summary>
    /// Une los hábitats vecinos (abre las paredes compartidas) y presuriza los grupos
    /// que tienen al menos un generador de O2 adentro.
    /// </summary>
    public void Recompute()
    {
        var habitats = new List<Habitat>();
        foreach (PlacedPiece p in pieces)
        {
            if (p == null || !p.gameObject.activeInHierarchy) continue;
            Habitat h = p.GetComponent<Habitat>();
            if (h != null) habitats.Add(h);
        }

        // Grupos (union-find simple).
        int[] parent = new int[habitats.Count];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;
        int Find(int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }

        var open = new bool[habitats.Count, 4];
        for (int a = 0; a < habitats.Count; a++)
        {
            for (int b = a + 1; b < habitats.Count; b++)
            {
                if (!AreNeighbors(habitats[a], habitats[b], out int sideA, out int sideB)) continue;
                open[a, sideA] = true;
                open[b, sideB] = true;
                parent[Find(a)] = Find(b);
            }
        }

        var pressurizedGroups = new HashSet<int>();
        foreach (PlacedPiece p in pieces)
        {
            if (p == null || !p.gameObject.activeInHierarchy) continue;
            OxygenGenerator generator = p.GetComponent<OxygenGenerator>();
            if (generator == null) continue;
            generator.Running = false;
            for (int i = 0; i < habitats.Count; i++)
            {
                if (!habitats[i].Contains(p.transform.position + Vector3.up * 0.2f)) continue;
                pressurizedGroups.Add(Find(i));
                generator.Running = true;
                break;
            }
        }

        // Puertas construidas: cada una cambia la pared del hábitat que tiene detrás.
        var doorSides = new bool[habitats.Count, 4];
        foreach (PlacedPiece p in pieces)
        {
            if (p == null || !p.gameObject.activeInHierarchy || p.Definition == null || p.Definition.kind != BuildableKind.Door) continue;
            for (int i = 0; i < habitats.Count; i++)
            {
                if (!DoorPlacement.TryGetSide(habitats[i], p.transform.position, p.transform.forward, out int side)) continue;
                doorSides[i, side] = true;
                break;
            }
        }

        for (int i = 0; i < habitats.Count; i++)
        {
            for (int s = 0; s < 4; s++)
            {
                habitats[i].SetDoor(s, doorSides[i, s]);
                habitats[i].SetSideOpen(s, open[i, s]);
            }
            habitats[i].SetPressurized(pressurizedGroups.Contains(Find(i)));
        }

        bool had = HasPressurizedBase;
        HasPressurizedBase = pressurizedGroups.Count > 0;
        if (HasPressurizedBase && !had && !restoring)
            NotificationQueue.Enqueue("¡Base con aire lista! Se desbloquearon las fábricas (B) y el autoguardado");
        else if (!HasPressurizedBase && had && !restoring)
            NotificationQueue.Enqueue("Tu base se quedó sin aire: autoguardado y fábricas nuevas en pausa");

        OnBaseChanged?.Invoke();
    }

    public static bool AreNeighbors(Habitat a, Habitat b, out int sideA, out int sideB)
    {
        sideA = sideB = -1;
        Vector3 d = b.transform.position - a.transform.position;
        if (Mathf.Abs(d.y) > 0.3f) return false;
        d.y = 0f;
        if (Mathf.Abs(d.magnitude - Habitat.Size) > 0.3f) return false;

        // Tienen que estar alineados (giros de 90°).
        float angle = Mathf.Abs(Mathf.DeltaAngle(a.transform.eulerAngles.y, b.transform.eulerAngles.y)) % 90f;
        if (angle > 3f && angle < 87f) return false;

        sideA = a.SideFacing(d);
        sideB = b.SideFacing(-d);
        return sideA >= 0 && sideB >= 0;
    }

    // ---------- Guardado ----------

    [Serializable]
    private class PieceRecord
    {
        public string id;
        public float x, y, z, yaw;
        public string extra;
    }

    [Serializable]
    private class State
    {
        public List<PieceRecord> pieces = new List<PieceRecord>();
    }

    public string CaptureState()
    {
        var state = new State();
        foreach (PlacedPiece piece in pieces)
        {
            if (piece == null || piece.Definition == null) continue;
            Vector3 p = piece.transform.position;
            piece.TryGetComponent(out IPieceState extra);
            state.pieces.Add(new PieceRecord
            {
                id = piece.Definition.Id,
                x = p.x, y = p.y, z = p.z,
                yaw = piece.transform.eulerAngles.y,
                extra = extra != null ? extra.CaptureExtra() : null
            });
        }
        return JsonUtility.ToJson(state);
    }

    public void RestoreState(string json, ItemDatabase database)
    {
        foreach (PlacedPiece piece in pieces)
            if (piece != null) Destroy(piece.gameObject);
        pieces.Clear();

        State state = JsonUtility.FromJson<State>(json);
        if (state?.pieces == null || catalog == null) return;
        restoring = true;

        foreach (PieceRecord record in state.pieces)
        {
            BuildableDefinition definition = catalog.Get(record.id);
            if (definition == null) continue;
            GameObject go = BuildPieceFactory.Create(definition, catalog, false);
            go.transform.SetParent(transform);
            go.transform.SetPositionAndRotation(new Vector3(record.x, record.y, record.z), Quaternion.Euler(0f, record.yaw, 0f));
            PlacedPiece piece = go.GetComponent<PlacedPiece>();
            if (go.TryGetComponent(out IPieceState extra)) extra.RestoreExtra(record.extra, database);
            pieces.Add(piece);
        }
        Physics.SyncTransforms();
        Recompute();
        restoring = false;

        // Checkpoint: al cargar (o reiniciar después de morir) aparecés en tu última base con aire.
        MovePlayerToCheckpoint();
    }

    public bool MovePlayerToCheckpoint()
    {
        if (!TryGetCheckpoint(out Vector3 spawn, out Quaternion facing)) return false;
        MovePlayer(spawn, facing);
        return true;
    }

    // ---------- Checkpoint ----------

    /// <summary>
    /// Último hábitat construido que tiene aire (la lista guarda el orden de construcción).
    /// La base de inicio (la nave) no cuenta: solo lo que armó el jugador.
    /// </summary>
    public Habitat LastPressurizedHabitat()
    {
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            if (pieces[i] == null || !pieces[i].TryGetComponent(out Habitat habitat)) continue;
            if (habitat.IsPressurized) return habitat;
        }
        return null;
    }

    public bool TryGetCheckpoint(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        Habitat habitat = LastPressurizedHabitat();
        if (habitat == null) return false;

        // Un lugar libre adentro: primero junto a la puerta, después el resto.
        Vector3[] candidates = { new Vector3(0f, 0f, 1.6f), new Vector3(-1.6f, 0f, 0f), new Vector3(1.6f, 0f, 0f), new Vector3(0f, 0f, -1.6f), Vector3.zero };
        Transform t = habitat.transform;
        foreach (Vector3 local in candidates)
        {
            Vector3 floor = t.TransformPoint(local + Vector3.up * Habitat.FloorTop);
            bool blocked = false;
            foreach (Collider c in Physics.OverlapCapsule(floor + Vector3.up * 0.5f, floor + Vector3.up * 1.6f, 0.35f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c.GetComponentInParent<Inventory>() != null) continue; // el propio jugador
                blocked = true;
                break;
            }
            if (blocked) continue;
            position = floor;
            rotation = Quaternion.Euler(0f, t.eulerAngles.y, 0f); // mirando hacia la puerta
            return true;
        }
        return false;
    }

    private static void MovePlayer(Vector3 floorPoint, Quaternion facing)
    {
        Inventory player = FindFirstObjectByType<Inventory>();
        if (player == null) return;

        CharacterController controller = player.GetComponent<CharacterController>();
        Vector3 position = floorPoint + Vector3.up * 0.05f;
        if (controller != null)
        {
            // Que la base de la cápsula quede apoyada en el piso, esté donde esté el pivote.
            position.y += controller.height * 0.5f - controller.center.y;
            controller.enabled = false;
        }
        player.transform.SetPositionAndRotation(position, facing);
        if (controller != null) controller.enabled = true;
        Physics.SyncTransforms();
        NotificationQueue.Enqueue("Apareciste en tu base");
    }
}
