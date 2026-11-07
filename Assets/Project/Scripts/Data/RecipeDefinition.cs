using System.Collections.Generic;
using UnityEngine;

public enum CraftingStation
{
    Hand,
    ShipFabricator,
    Smelter,
    Assembler
}

/// <summary>
/// Receta: qué consume, qué produce, cuánto tarda y dónde se fabrica.
/// La usan tanto el crafteo manual (Fase 2) como las máquinas (Fase 4).
/// </summary>
[CreateAssetMenu(fileName = "NewRecipe", menuName = "Lost Signal/Recipe")]
public class RecipeDefinition : ScriptableObject
{
    public string id;
    public string displayName = "Receta";
    public CraftingStation station = CraftingStation.ShipFabricator;
    [Min(0f)] public float craftSeconds = 2f;
    public List<ItemAmount> inputs = new List<ItemAmount>();
    public List<ItemAmount> outputs = new List<ItemAmount>();

    [Tooltip("Si está desactivado, hay que desbloquear el plano antes de poder fabricarla.")]
    public bool unlockedByDefault = true;

    public string Id => string.IsNullOrEmpty(id) ? name.ToLowerInvariant() : id;

    public bool CanCraftFrom(ItemContainer container) => container.HasAll(inputs);

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(id)) id = name.ToLowerInvariant();
    }
#endif
}
