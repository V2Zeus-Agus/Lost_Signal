using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Registro de todos los items del juego. El guardado guarda ids (texto),
/// y al cargar usa esta base para convertirlos de nuevo en ItemDefinition.
/// </summary>
[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Lost Signal/Item Database")]
public class ItemDatabase : ScriptableObject
{
    [SerializeField] private List<ItemDefinition> items = new List<ItemDefinition>();

    private Dictionary<string, ItemDefinition> lookup;

    public IReadOnlyList<ItemDefinition> Items => items;

    private void OnEnable() => lookup = null;

    public ItemDefinition Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (lookup == null) BuildLookup();

        if (lookup.TryGetValue(id, out ItemDefinition item)) return item;

        Debug.LogWarning($"[ItemDatabase] No existe un item con id '{id}'. ¿Falta agregarlo a la base?");
        return null;
    }

    private void BuildLookup()
    {
        lookup = new Dictionary<string, ItemDefinition>();
        foreach (ItemDefinition item in items)
        {
            if (item == null) continue;
            if (lookup.ContainsKey(item.Id))
            {
                Debug.LogError($"[ItemDatabase] Id repetido '{item.Id}' en {item.name}. Cada item necesita un id único.");
                continue;
            }
            lookup.Add(item.Id, item);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Buscar todos los items del proyecto")]
    public void CollectAllItems()
    {
        items.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition"))
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (item != null) items.Add(item);
        }
        lookup = null;
        EditorUtility.SetDirty(this);
    }
#endif
}
