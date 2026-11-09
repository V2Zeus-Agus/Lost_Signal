#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// - Rareza de cada recurso (los poco comunes y raros se recuperan del cofre al morir).
/// - Cofre de muerte en Mars (DeathChestManager con id de guardado).
/// - Rocas de decoración un poco hundidas en el suelo, en el tutorial y en Mars, para que no floten.
/// Re-ejecutable.
/// </summary>
public static class DeathAndDecorSetup
{
    private const string TutorialScenePath = "Assets/Scenes/SampleScene.unity";
    private const string MarsScenePath = "Assets/Scenes/Mars.unity";
    private const string ItemFolder = "Assets/Project/Scriptable_objects/ResourceTypes";

    [MenuItem("Tools/Lost Signal/Ajustes: cofre al morir, rareza y rocas hundidas")]
    public static void ApplyAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var report = new StringBuilder();

        report.AppendLine(SetRarities());

        var tutorial = EditorSceneManager.OpenScene(TutorialScenePath);
        report.AppendLine("Tutorial: " + SinkRocks());
        EditorSceneManager.MarkSceneDirty(tutorial);
        EditorSceneManager.SaveScene(tutorial);

        var mars = EditorSceneManager.OpenScene(MarsScenePath);
        report.AppendLine("Mars: " + SinkRocks());
        report.AppendLine(SetupDeathChests());
        Phase0Setup.AssignSaveIds();
        EditorSceneManager.MarkSceneDirty(mars);
        EditorSceneManager.SaveScene(mars);
        AssetDatabase.SaveAssets();

        Debug.Log("[Lost Signal] " + report);
        EditorUtility.DisplayDialog("Lost Signal", report.ToString(), "OK");
    }

    // ---------- Rareza ----------

    private static string SetRarities()
    {
        var rarities = new Dictionary<string, ItemRarity>
        {
            ["Iron"] = ItemRarity.Common,
            ["Fuel"] = ItemRarity.Common,
            ["Crystal"] = ItemRarity.Uncommon,
            ["Silicon"] = ItemRarity.Uncommon,
            ["Sulfur"] = ItemRarity.Rare,
        };
        var done = new List<string>();
        foreach (var pair in rarities)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemFolder}/{pair.Key}.asset");
            if (item == null) continue;
            item.rarity = pair.Value;
            EditorUtility.SetDirty(item);
            done.Add($"{item.displayName} {pair.Value}");
        }
        return "• Rareza: " + string.Join(", ", done);
    }

    // ---------- Cofre de muerte ----------

    private static string SetupDeathChests()
    {
        DeathChestManager manager = Object.FindFirstObjectByType<DeathChestManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            var go = new GameObject("DeathChests");
            go.AddComponent<SaveableEntity>();
            manager = go.AddComponent<DeathChestManager>();
        }
        var so = new SerializedObject(manager);
        so.FindProperty("playerInventory").objectReferenceValue = Object.FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        so.ApplyModifiedPropertiesWithoutUndo();
        return "• Cofre al morir listo (DeathChests)";
    }

    // ---------- Rocas ----------

    /// <summary>
    /// Baja cada roca hasta que su punto más bajo quede un poco bajo el suelo más bajo que pisa
    /// (en pendientes la roca "colgaba" del lado de abajo). Solo baja, nunca sube.
    /// </summary>
    private static string SinkRocks()
    {
        Collider ground = GameObject.Find("Ground")?.GetComponent<Collider>();
        if (ground == null) return "no hay Ground";

        var roots = new List<Transform>();
        GameObject decor = GameObject.Find("Decor");
        if (decor != null) roots.Add(decor.transform);
        GameObject expansion = GameObject.Find("Expansion");
        Transform expansionRocks = expansion != null ? expansion.transform.Find("Rocks") : null;
        if (expansionRocks != null) roots.Add(expansionRocks);

        int moved = 0, checkedCount = 0;
        foreach (Transform root in roots)
        {
            foreach (Transform rock in root)
            {
                string n = rock.name.ToLowerInvariant();
                if (!n.StartsWith("rock")) continue; // solo rocas (no colinas ni nidos)
                checkedCount++;
                if (!TryGetBounds(rock, out Bounds b)) continue;

                float lowest = float.MaxValue;
                foreach (Vector2 c in new[]
                         {
                             new Vector2(b.center.x, b.center.z),
                             new Vector2(b.min.x, b.min.z), new Vector2(b.max.x, b.min.z),
                             new Vector2(b.min.x, b.max.z), new Vector2(b.max.x, b.max.z),
                         })
                {
                    if (ground.Raycast(new Ray(new Vector3(c.x, b.max.y + 500f, c.y), Vector3.down), out RaycastHit hit, 2000f))
                        lowest = Mathf.Min(lowest, hit.point.y);
                }
                if (lowest == float.MaxValue) continue;

                float sink = Mathf.Max(b.size.y * 0.12f, 0.15f);
                float delta = (lowest - sink) - b.min.y;
                if (delta >= -0.01f) continue; // ya está enterrada lo suficiente
                rock.position += Vector3.up * delta;
                moved++;
            }
        }
        return $"{moved}/{checkedCount} rocas bajadas para que no floten";
    }

    private static bool TryGetBounds(Transform t, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
        {
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return any;
    }
}
#endif
