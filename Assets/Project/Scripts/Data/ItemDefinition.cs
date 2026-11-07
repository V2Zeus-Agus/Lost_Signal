using UnityEngine;

// NOTA: este archivo tiene que llamarse ItemDefinition.cs.
// Renombralo desde la ventana Project de Unity (F2) para que conserve su .meta
// y los assets Hierro/Cristal/Combustible sigan apuntando a este script.

public enum ItemCategory
{
    RawResource,
    Refined,
    Component,
    Fuel,
    Consumable,
    Tool
}

[CreateAssetMenu(fileName = "NewItem", menuName = "Lost Signal/Item")]
public class ItemDefinition : ScriptableObject
{
    [Tooltip("Id único y estable que usa el guardado. No lo cambies una vez que haya partidas guardadas.")]
    public string id;
    public string displayName = "Item";
    public Color uiColor = Color.white;
    public Sprite icon;
    public ItemCategory category = ItemCategory.RawResource;
    [Min(1)] public int maxStack = 50;

    public string Id => string.IsNullOrEmpty(id) ? name.ToLowerInvariant() : id;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(id)) id = name.ToLowerInvariant();
    }
#endif
}
