#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fase 3a: modelos KayKit (Space Base Bits) para las piezas construibles.
/// El HUD nuevo no necesita setup: se arma solo en runtime. Re-ejecutable.
/// </summary>
public static class Phase3Setup
{
    private const string BuildFolder = "Assets/Project/Scriptable_objects/Buildables";
    private const string KayKitFolder = "Assets/Unity_assets";

    [MenuItem("Tools/Lost Signal/Fase 3a: modelos KayKit para fábricas y salas")]
    public static void Apply()
    {
        BuildCatalog catalog = AssetDatabase.LoadAssetAtPath<BuildCatalog>(BuildFolder + "/BuildCatalog.asset");
        if (catalog == null)
        {
            EditorUtility.DisplayDialog("Fase 3a", "Falta el catálogo: corré primero \"Fase 1\".", "OK");
            return;
        }

        var report = new StringBuilder();
        foreach (BuildableDefinition definition in catalog.buildables)
        {
            if (definition == null) continue;
            string modelName = ModelFor(definition);
            if (modelName == null) continue;

            GameObject model = FindModel(modelName);
            if (model == null)
            {
                report.AppendLine($"• {definition.displayName}: no encontré \"{modelName}\" (queda con primitivas)");
                continue;
            }
            definition.model = model;
            definition.modelScale = 1f;
            definition.modelYaw = definition.kind == BuildableKind.Factory ? 0f : definition.modelYaw;
            EditorUtility.SetDirty(definition);
            report.AppendLine($"• {definition.displayName} → {modelName}");
        }

        GameObject roof = FindModel("roofmodule_solarpanels");
        catalog.habitatRoofDecor = roof;
        EditorUtility.SetDirty(catalog);
        report.AppendLine(roof != null ? "• Hábitat: paneles solares en el techo (roofmodule_solarpanels)" : "• Hábitat: no encontré roofmodule_solarpanels");

        AssetDatabase.SaveAssets();
        report.AppendLine();
        report.AppendLine("El HUD nuevo (vitales, fijados, objetivos, entorno, mochila) se arma solo al dar Play.");
        report.AppendLine("Las piezas ya construidas se rearman con el modelo al cargar la partida.");
        Debug.Log("[Fase 3a]\n" + report);
        EditorUtility.DisplayDialog("Fase 3a: modelos KayKit", report.ToString(), "OK");
    }

    private const string ItemFolder = "Assets/Project/Scriptable_objects/ResourceTypes";

    [MenuItem("Tools/Lost Signal/Fase 3b: terminal de mejoras construible")]
    public static void AddUpgradeTerminal()
    {
        BuildCatalog catalog = AssetDatabase.LoadAssetAtPath<BuildCatalog>(BuildFolder + "/BuildCatalog.asset");
        if (catalog == null)
        {
            EditorUtility.DisplayDialog("Fase 3b", "Falta el catálogo: corré primero \"Fase 1\".", "OK");
            return;
        }

        string path = BuildFolder + "/UpgradeTerminal.asset";
        BuildableDefinition terminal = AssetDatabase.LoadAssetAtPath<BuildableDefinition>(path);
        if (terminal == null)
        {
            terminal = ScriptableObject.CreateInstance<BuildableDefinition>();
            AssetDatabase.CreateAsset(terminal, path);
        }
        terminal.id = "upgrade_terminal";
        terminal.displayName = "Terminal de mejoras";
        terminal.kind = BuildableKind.UpgradeShop;
        terminal.mustBeInsideHabitat = true;
        terminal.requiresPressurizedBase = true;
        terminal.description = "La tienda de la nave dentro de tu base: O2, mochila y traje sin volver a la nave. Va adentro de un hábitat con aire.";
        terminal.cost = new List<ItemAmount>();
        foreach (var (item, amount) in new[] { ("Iron", 12), ("Silicon", 6), ("Crystal", 4), ("Sulfur", 2) })
        {
            ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemFolder}/{item}.asset");
            if (definition != null) terminal.cost.Add(new ItemAmount { item = definition, amount = amount });
        }
        EditorUtility.SetDirty(terminal);

        // En el menú B va justo después del generador de O2.
        catalog.buildables.Remove(terminal);
        int index = catalog.buildables.FindIndex(b => b != null && b.kind == BuildableKind.OxygenGenerator);
        catalog.buildables.Insert(index >= 0 ? index + 1 : catalog.buildables.Count, terminal);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Fase 3b: terminal de mejoras",
            "• Terminal de mejoras en el menú B (después del generador)\n" +
            "• 12 Hierro + 6 Silicio + 4 Cristal + 2 Azufre\n" +
            "• Va adentro de un hábitat y se desbloquea con una base con aire\n" +
            "• Abre la misma tienda de la nave (mismo progreso)", "OK");
    }

    [MenuItem("Tools/Lost Signal/Fase 3c: puerta y modelo de la terminal")]
    public static void AddDoorAndTerminalModel()
    {
        BuildCatalog catalog = AssetDatabase.LoadAssetAtPath<BuildCatalog>(BuildFolder + "/BuildCatalog.asset");
        if (catalog == null)
        {
            EditorUtility.DisplayDialog("Fase 3c", "Falta el catálogo: corré primero \"Fase 1\".", "OK");
            return;
        }
        var report = new StringBuilder();

        // Puerta: 1 Hierro, se encaja en cualquier pared libre de un hábitat.
        string doorPath = BuildFolder + "/Door.asset";
        BuildableDefinition door = AssetDatabase.LoadAssetAtPath<BuildableDefinition>(doorPath);
        if (door == null)
        {
            door = ScriptableObject.CreateInstance<BuildableDefinition>();
            AssetDatabase.CreateAsset(door, doorPath);
        }
        door.id = "door";
        door.displayName = "Puerta";
        door.kind = BuildableKind.Door;
        door.mustBeInsideHabitat = false;
        door.requiresPressurizedBase = false;
        door.description = "Abre una entrada en cualquier pared libre de un hábitat (el frente ya trae una). Mantiene el aire.";
        door.cost = new List<ItemAmount>();
        ItemDefinition iron = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemFolder}/Iron.asset");
        if (iron != null) door.cost.Add(new ItemAmount { item = iron, amount = 1 });
        EditorUtility.SetDirty(door);

        catalog.buildables.Remove(door);
        int habitatIndex = catalog.buildables.FindIndex(b => b != null && b.kind == BuildableKind.Habitat);
        catalog.buildables.Insert(habitatIndex >= 0 ? habitatIndex + 1 : 0, door);
        report.AppendLine("• Puerta en el menú B (después del hábitat): 1 Hierro");

        // Terminal: el mismo modelo que la tienda de la nave.
        BuildableDefinition terminal = catalog.buildables.Find(b => b != null && b.kind == BuildableKind.UpgradeShop);
        GameObject shopModel = ShopModel(out string source);
        if (terminal != null && shopModel != null)
        {
            terminal.model = shopModel;
            terminal.modelScale = 1f;
            EditorUtility.SetDirty(terminal);
            report.AppendLine($"• Terminal de mejoras → {shopModel.name} ({source})");
        }
        else report.AppendLine("• Terminal: no encontré el modelo de la tienda (corré antes \"Fase 3b\")");

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Fase 3c", report.ToString(), "OK");
    }

    /// <summary>Modelo que usa la tienda de la escena abierta; si no, water_storage del pack.</summary>
    private static GameObject ShopModel(out string source)
    {
        UpgradeShop shop = Object.FindFirstObjectByType<UpgradeShop>(FindObjectsInactive.Include);
        if (shop != null)
        {
            foreach (Renderer r in shop.GetComponentsInChildren<Renderer>(true))
            {
                GameObject original = PrefabUtility.GetCorrespondingObjectFromOriginalSource(r.gameObject);
                if (original != null)
                {
                    source = "igual que la tienda de la escena";
                    return original.transform.root.gameObject;
                }
            }
        }
        source = "water_storage, el de la tienda de la nave";
        return FindModel("water_storage");
    }

    private static string ModelFor(BuildableDefinition definition)
    {
        switch (definition.kind)
        {
            case BuildableKind.OxygenGenerator: return "eco_module";
            case BuildableKind.Storage: return "cargo_B";
            case BuildableKind.Lamp: return "lights";
            case BuildableKind.Factory:
                string produced = definition.producedItem != null ? definition.producedItem.name.ToLowerInvariant() : "";
                return produced.Contains("fuel") ? "cargodepot_A" : "drill_structure";
            default: return null; // el hábitat sigue procedural (sus paredes se abren al unir módulos)
        }
    }

    /// <summary>Busca el FBX por nombre exacto, prefiriendo la carpeta fbx(unity) del pack.</summary>
    private static GameObject FindModel(string fileName)
    {
        GameObject fallback = null;
        foreach (string guid in AssetDatabase.FindAssets(fileName + " t:Model", new[] { KayKitFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) != fileName) continue;
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) continue;
            if (path.Contains("fbx(unity)")) return asset;
            if (fallback == null) fallback = asset;
        }
        return fallback;
    }
}
#endif
