using System.Collections.Generic;
using UnityEngine;

public enum BuildableKind
{
    Habitat,
    OxygenGenerator,
    Storage,
    Lamp,
    Factory
}

/// <summary>Algo que el jugador puede construir (data-driven, como los items).</summary>
[CreateAssetMenu(fileName = "NewBuildable", menuName = "Lost Signal/Buildable")]
public class BuildableDefinition : ScriptableObject
{
    [Tooltip("Id estable para el guardado.")]
    public string id;
    public string displayName = "Pieza";
    [TextArea(2, 4)] public string description;
    public BuildableKind kind;
    public List<ItemAmount> cost = new List<ItemAmount>();
    [Tooltip("Solo se puede poner adentro de un hábitat (p. ej. el generador de O2).")]
    public bool mustBeInsideHabitat;
    [Tooltip("Solo aparece habilitado cuando el jugador tiene una base con aire (hábitat + generador de O2).")]
    public bool requiresPressurizedBase;

    [Header("Fábrica (solo si kind = Factory)")]
    public ItemDefinition producedItem;
    [Tooltip("Segundos de juego por cada unidad producida.")]
    [Min(1f)] public float secondsPerItem = 30f;
    [Tooltip("Cuánto guarda adentro antes de frenar.")]
    [Min(1)] public int storageCapacity = 30;

    public string Id => string.IsNullOrEmpty(id) ? name.ToLowerInvariant() : id;
}
