#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Configura la Fase 0 en la escena abierta con un solo click:
/// base de items, planeta Marte, sistemas (Tick, Planeta, Guardado), mejoras del jugador
/// e ids de guardado para todos los objetos que guardan estado.
/// </summary>
public static class Phase0Setup
{
    private const string SoFolder = "Assets/Project/Scriptable_objects";
    private const string DatabasePath = SoFolder + "/ItemDatabase.asset";
    private const string PlanetsFolder = SoFolder + "/Planets";
    private const string MarsPath = PlanetsFolder + "/Mars.asset";
    private const string SystemsRootName = "CoreSystems"; // separado de "Systems", que el Setup Scene recrea

    [MenuItem("Tools/Lost Signal/Fase 0/1 - Configurar escena actual")]
    public static void SetupCurrentScene()
    {
        if (!ConfigureOpenScene())
        {
            EditorUtility.DisplayDialog("Fase 0", "No encontré un Inventory en la escena. Abrí la escena de juego primero.", "OK");
            return;
        }

        EditorUtility.DisplayDialog("Fase 0",
            "Listo:\n• ItemDatabase y planeta Marte creados\n• CoreSystems (TickManager, PlanetContext, SaveManager)\n• PlayerUpgrades en el jugador\n• IDs de guardado asignados\n\nGuardá la escena con Ctrl+S.",
            "OK");
    }

    /// <summary>Configura la escena abierta sin diálogos. Devuelve false si no es una escena de juego.</summary>
    public static bool ConfigureOpenScene()
    {
        Inventory inventory = Object.FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (inventory == null) return false;

        ItemDatabase database = CreateOrUpdateDatabase();
        PlanetDefinition mars = CreateOrLoadMars(database);

        // Mejoras persistentes en el jugador
        GameObject player = inventory.gameObject;
        PlayerUpgrades upgrades = player.GetComponent<PlayerUpgrades>();
        if (upgrades == null) upgrades = Undo.AddComponent<PlayerUpgrades>(player);

        PlayerOxygen oxygen = player.GetComponent<PlayerOxygen>();
        if (oxygen != null) SetReference(oxygen, "upgrades", upgrades);

        OxygenShop shop = Object.FindFirstObjectByType<OxygenShop>(FindObjectsInactive.Include);
        if (shop != null) SetReference(shop, "playerUpgrades", upgrades);

        // Sistemas
        GameObject root = GameObject.Find(SystemsRootName);
        if (root == null)
        {
            root = new GameObject(SystemsRootName);
            Undo.RegisterCreatedObjectUndo(root, "Crear CoreSystems");
        }

        GetOrAdd<TickManager>(root);
        PlanetContext planetContext = GetOrAdd<PlanetContext>(root);
        SetReference(planetContext, "planet", mars);

        SaveManager saveManager = GetOrAdd<SaveManager>(root);
        SetReference(saveManager, "itemDatabase", database);
        SetReference(saveManager, "playerInventory", inventory);
        SetReference(saveManager, "playerUpgrades", upgrades);

        AssignSaveIds();

        EditorSceneManager.MarkSceneDirty(player.scene);
        Selection.activeGameObject = root;

        Debug.Log($"[Fase 0] Escena '{player.scene.name}' configurada.");
        return true;
    }

    [MenuItem("Tools/Lost Signal/Fase 0/2 - Reasignar IDs de guardado")]
    public static void AssignSaveIds()
    {
        int added = 0;
        foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (behaviour is ISaveable && behaviour.GetComponent<SaveableEntity>() == null)
            {
                Undo.AddComponent<SaveableEntity>(behaviour.gameObject);
                added++;
            }
        }

        // Ids vacíos o duplicados (pasa al duplicar objetos con Ctrl+D) reciben uno nuevo.
        int fixedIds = 0;
        var seen = new HashSet<string>();
        foreach (SaveableEntity entity in Object.FindObjectsByType<SaveableEntity>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!string.IsNullOrEmpty(entity.Id) && seen.Add(entity.Id)) continue;

            var so = new SerializedObject(entity);
            string newId = Guid.NewGuid().ToString("N");
            so.FindProperty("id").stringValue = newId;
            so.ApplyModifiedProperties();
            seen.Add(newId);
            fixedIds++;
        }

        if (added > 0 || fixedIds > 0)
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log($"[Fase 0] SaveableEntity agregados: {added} · ids nuevos/corregidos: {fixedIds}");
    }

    [MenuItem("Tools/Lost Signal/Guardado/Borrar partida guardada")]
    public static void DeleteSave()
    {
        if (!EditorUtility.DisplayDialog("Borrar partida", "¿Borrar el guardado actual (perfil y planetas)?", "Borrar", "Cancelar"))
            return;
        SaveSystem.DeleteAll();
        Debug.Log("[Guardado] Partida borrada.");
    }

    [MenuItem("Tools/Lost Signal/Guardado/Abrir carpeta de guardado")]
    public static void OpenSaveFolder()
    {
        System.IO.Directory.CreateDirectory(SaveSystem.RootFolder);
        EditorUtility.RevealInFinder(SaveSystem.RootFolder);
    }

    // ---------- Assets ----------

    private static ItemDatabase CreateOrUpdateDatabase()
    {
        // Ids para los items que todavía no tienen.
        foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition"))
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (item == null || !string.IsNullOrEmpty(item.id)) continue;
            item.id = item.name.ToLowerInvariant();
            EditorUtility.SetDirty(item);
        }

        ItemDatabase database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(DatabasePath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<ItemDatabase>();
            AssetDatabase.CreateAsset(database, DatabasePath);
        }
        database.CollectAllItems();

        if (database.Items.Count == 0)
            Debug.LogWarning("[Fase 0] No se encontraron ItemDefinition. ¿Renombraste ResourceType.cs a ItemDefinition.cs?");

        AssetDatabase.SaveAssets();
        return database;
    }

    private static PlanetDefinition CreateOrLoadMars(ItemDatabase database)
    {
        if (!AssetDatabase.IsValidFolder(PlanetsFolder))
            AssetDatabase.CreateFolder(SoFolder, "Planets");

        PlanetDefinition mars = AssetDatabase.LoadAssetAtPath<PlanetDefinition>(MarsPath);
        if (mars != null) return mars;

        mars = ScriptableObject.CreateInstance<PlanetDefinition>();
        mars.id = "mars";
        mars.displayName = "Marte";
        mars.description = "Donde se estrelló la nave. Hostil, pero el más amable de todos.";
        mars.sceneName = EditorSceneManager.GetActiveScene().name;
        mars.oxygenDrainMultiplier = 1f;
        mars.pressure = 1f;
        mars.temperatureCelsius = -60f;
        mars.gravityMultiplier = 1f;
        mars.launchFuelCost = 0; // planeta inicial
        mars.availableResources = new List<ItemDefinition>(database.Items);

        AssetDatabase.CreateAsset(mars, MarsPath);
        AssetDatabase.SaveAssets();
        return mars;
    }

    // ---------- Helpers ----------

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(go);
    }

    private static void SetReference(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null)
        {
            Debug.LogError($"[Fase 0] El campo '{field}' no existe en {target.GetType().Name}");
            return;
        }
        property.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }
}
#endif
