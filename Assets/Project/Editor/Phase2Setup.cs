#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Fase 2a: fábricas (Hierro y Combustible), generador de O2 70% más barato.
/// Las fábricas se desbloquean con una base con aire. Re-ejecutable.
/// </summary>
public static class Phase2Setup
{
    private const string Root = "Assets/Project";
    private const string ItemFolder = Root + "/Scriptable_objects/ResourceTypes";
    private const string BuildFolder = Root + "/Scriptable_objects/Buildables";

    [MenuItem("Tools/Lost Signal/Fase 2a: fábricas de Hierro y Combustible")]
    public static void Apply()
    {
        BuildCatalog catalog = AssetDatabase.LoadAssetAtPath<BuildCatalog>(BuildFolder + "/BuildCatalog.asset");
        if (catalog == null)
        {
            EditorUtility.DisplayDialog("Fase 2a", "Falta el catálogo: corré primero \"Fase 1\".", "OK");
            return;
        }
        var report = new StringBuilder();

        // Generador de O2: 70% más barato (10 Fe, 6 Cr, 4 Si, 2 S → 3, 2, 1, 1).
        BuildableDefinition generator = catalog.Get("oxygen_generator");
        if (generator != null)
        {
            generator.cost = Cost(("Iron", 3), ("Crystal", 2), ("Silicon", 1), ("Sulfur", 1));
            generator.description = "Va adentro de un hábitat: le da aire a todos los módulos unidos. Con una base con aire se desbloquean las fábricas y el autoguardado.";
            EditorUtility.SetDirty(generator);
            report.AppendLine("• Generador de O2: 3 Hierro + 2 Cristal + 1 Silicio + 1 Azufre (70% menos)");
        }

        BuildableDefinition ironFactory = Factory("IronFactory", "iron_factory", "Fábrica de Hierro", "Iron", 30f, 30,
            "Extrae Hierro sin límite: 1 cada 30 s de juego. Guarda 30; si hay un cofre a menos de 10 m, lo manda ahí.",
            ("Iron", 25), ("Silicon", 8), ("Crystal", 4));
        BuildableDefinition fuelFactory = Factory("FuelFactory", "fuel_factory", "Fábrica de Combustible", "Fuel", 45f, 30,
            "Extrae Combustible sin límite: 1 cada 45 s de juego. Guarda 30; si hay un cofre a menos de 10 m, lo manda ahí.",
            ("Iron", 20), ("Silicon", 6), ("Crystal", 6), ("Sulfur", 2));

        foreach (BuildableDefinition factory in new[] { ironFactory, fuelFactory })
            if (!catalog.buildables.Contains(factory)) catalog.buildables.Add(factory);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        report.AppendLine("• Fábrica de Hierro: 25 Hierro + 8 Silicio + 4 Cristal · 1 cada 30 s");
        report.AppendLine("• Fábrica de Combustible: 20 Hierro + 6 Silicio + 6 Cristal + 2 Azufre · 1 cada 45 s");
        report.AppendLine("• Se desbloquean en el menú B con una base con aire (hábitat + generador de O2)");

        // Las fábricas producen con el TickManager: que exista en Mars.
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Mars.unity");
            if (Object.FindFirstObjectByType<TickManager>(FindObjectsInactive.Include) == null)
            {
                new GameObject("TickManager").AddComponent<TickManager>();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                report.Append("• TickManager agregado a Mars");
            }
            else report.Append("• TickManager ya estaba en Mars");
        }
        EditorUtility.DisplayDialog("Fase 2a", report.ToString(), "OK");
    }

    private static BuildableDefinition Factory(string file, string id, string name, string item, float seconds, int capacity,
        string description, params (string item, int amount)[] cost)
    {
        string path = $"{BuildFolder}/{file}.asset";
        BuildableDefinition definition = AssetDatabase.LoadAssetAtPath<BuildableDefinition>(path);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<BuildableDefinition>();
            AssetDatabase.CreateAsset(definition, path);
        }
        definition.id = id;
        definition.displayName = name;
        definition.kind = BuildableKind.Factory;
        definition.requiresPressurizedBase = true;
        definition.mustBeInsideHabitat = false;
        definition.producedItem = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemFolder}/{item}.asset");
        definition.secondsPerItem = seconds;
        definition.storageCapacity = capacity;
        definition.description = description;
        definition.cost = Cost(cost);
        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static List<ItemAmount> Cost(params (string item, int amount)[] cost)
    {
        var list = new List<ItemAmount>();
        foreach (var (item, amount) in cost)
        {
            ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemFolder}/{item}.asset");
            if (definition != null) list.Add(new ItemAmount { item = definition, amount = amount });
        }
        return list;
    }
}
#endif
