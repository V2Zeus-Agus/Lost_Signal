#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Fase 1: tienda única (O2 + mochila + traje), eventos de entorno, HUD ambiental,
/// construcción de bases y nidos hundidos. Re-ejecutable.
/// </summary>
public static class Phase1Setup
{
    private const string TutorialScenePath = "Assets/Scenes/SampleScene.unity";
    private const string MarsScenePath = "Assets/Scenes/Mars.unity";
    private const string Root = "Assets/Project";
    private const string ItemFolder = Root + "/Scriptable_objects/ResourceTypes";
    private const string BuildFolder = Root + "/Scriptable_objects/Buildables";
    private const string MaterialFolder = Root + "/Materials/Building";
    private const string MarsPlanetPath = Root + "/Scriptable_objects/Planets/Mars.asset";

    [MenuItem("Tools/Lost Signal/Fase 1: tienda única, traje, eventos, construcción y nidos")]
    public static void ApplyAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var report = new StringBuilder();
        try
        {
            EditorUtility.DisplayProgressBar("Fase 1", "Catálogo de construcción...", 0.1f);
            BuildCatalog catalog = CreateCatalog();
            report.AppendLine($"• Catálogo de construcción: {catalog.buildables.Count} piezas");

            PlanetDefinition mars = AssetDatabase.LoadAssetAtPath<PlanetDefinition>(MarsPlanetPath);
            if (mars != null)
            {
                mars.pressureKPa = 0.6f;
                EditorUtility.SetDirty(mars);
            }

            EditorUtility.DisplayProgressBar("Fase 1", "Tutorial...", 0.3f);
            Scene tutorial = EditorSceneManager.OpenScene(TutorialScenePath);
            report.AppendLine("Tutorial: " + SetupShop(false));
            report.AppendLine("Tutorial: " + SinkNests());
            EditorSceneManager.MarkSceneDirty(tutorial);
            EditorSceneManager.SaveScene(tutorial);

            EditorUtility.DisplayProgressBar("Fase 1", "Mars...", 0.6f);
            Scene marsScene = EditorSceneManager.OpenScene(MarsScenePath);
            report.AppendLine("Mars: " + SetupShop(true));
            report.AppendLine("Mars: " + SinkNests());
            report.AppendLine("Mars: " + SetupEnvironment());
            // Abrir una escena descarga los assets sin usar: se vuelve a cargar el catálogo desde disco.
            catalog = AssetDatabase.LoadAssetAtPath<BuildCatalog>(BuildFolder + "/BuildCatalog.asset");
            report.AppendLine("Mars: " + SetupBuilding(catalog));
            Phase0Setup.AssignSaveIds();
            EditorSceneManager.MarkSceneDirty(marsScene);
            EditorSceneManager.SaveScene(marsScene);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log("[Fase 1] " + report);
        EditorUtility.DisplayDialog("Fase 1", report.ToString(), "OK");
    }

    // ======================================================================
    // Tienda única
    // ======================================================================

    private static string SetupShop(bool fullShop)
    {
        PlayerUpgrades upgrades = Object.FindFirstObjectByType<PlayerUpgrades>(FindObjectsInactive.Include);
        PlayerOxygen oxygen = Object.FindFirstObjectByType<PlayerOxygen>(FindObjectsInactive.Include);
        OxygenShop oldOxygen = Object.FindFirstObjectByType<OxygenShop>(FindObjectsInactive.Include);
        UpgradeStation oldStation = Object.FindFirstObjectByType<UpgradeStation>(FindObjectsInactive.Include);
        UpgradeShop shop = Object.FindFirstObjectByType<UpgradeShop>(FindObjectsInactive.Include);

        // Valores de O2 que ya estaban configurados.
        ItemDefinition oxygenItem = LoadItem("Iron");
        int earlyCost = 3, scaledBase = OxygenTankProgression.DefaultScaledBaseCost;
        if (oldOxygen != null)
        {
            var oso = new SerializedObject(oldOxygen);
            oxygenItem = oso.FindProperty("costType").objectReferenceValue as ItemDefinition ?? oxygenItem;
            earlyCost = oso.FindProperty("costPerUpgrade").intValue;
            scaledBase = oso.FindProperty("scaledBaseCost").intValue;
        }

        // ¿Dónde queda la tienda? En Mars, donde estaba la de la mochila; en el tutorial, donde estaba la de O2.
        GameObject host = shop != null ? shop.gameObject
                        : fullShop && oldStation != null ? oldStation.gameObject
                        : oldOxygen != null ? oldOxygen.gameObject : null;
        if (host == null) return "no encontré dónde poner la tienda";

        List<(string description, (string item, int amount)[] cost)> backpackLevels = null;
        if (oldStation != null)
        {
            backpackLevels = new List<(string, (string, int)[])>();
            var sso = new SerializedObject(oldStation);
            SerializedProperty levels = sso.FindProperty("levels");
            for (int i = 0; i < levels.arraySize; i++)
            {
                SerializedProperty level = levels.GetArrayElementAtIndex(i);
                SerializedProperty cost = level.FindPropertyRelative("cost");
                var costs = new (string, int)[cost.arraySize];
                for (int c = 0; c < cost.arraySize; c++)
                {
                    SerializedProperty el = cost.GetArrayElementAtIndex(c);
                    var item = el.FindPropertyRelative("item").objectReferenceValue as ItemDefinition;
                    costs[c] = (item != null ? item.name : "", el.FindPropertyRelative("amount").intValue);
                }
                backpackLevels.Add((level.FindPropertyRelative("description").stringValue, costs));
            }
        }

        if (shop == null) shop = host.AddComponent<UpgradeShop>();
        host.name = "UpgradeShop";

        var so = new SerializedObject(shop);
        so.FindProperty("shopName").stringValue = "Tienda de mejoras";
        so.FindProperty("playerUpgrades").objectReferenceValue = upgrades;
        so.FindProperty("playerOxygen").objectReferenceValue = oxygen;
        so.FindProperty("offerOxygen").boolValue = true;
        so.FindProperty("oxygenCostItem").objectReferenceValue = oxygenItem;
        so.FindProperty("oxygenEarlyCost").intValue = earlyCost;
        so.FindProperty("oxygenScaledBaseCost").intValue = scaledBase;
        so.FindProperty("offerBackpack").boolValue = fullShop;
        so.FindProperty("offerSuit").boolValue = fullShop;

        if (fullShop)
        {
            if (backpackLevels == null || backpackLevels.Count == 0)
                backpackLevels = new List<(string, (string, int)[])>
                {
                    ("+10 items (40)", new[] { ("Iron", 9), ("Silicon", 3) }),
                    ("+10 items (50)", new[] { ("Iron", 12), ("Silicon", 6), ("Sulfur", 2) }),
                };
            WriteLevels(so.FindProperty("backpackLevels"), backpackLevels);

            WriteLevels(so.FindProperty("suitLevels"), new List<(string, (string, int)[])>
            {
                ("Aguanta -80 °C · tormenta +45% O2 · meteoritos -20%",
                    new[] { ("Iron", 10), ("Silicon", 5), ("Crystal", 3) }),
                ("Aguanta -100 °C (toda la Cuenca Fría) · meteoritos -40%",
                    new[] { ("Iron", 18), ("Silicon", 9), ("Crystal", 6), ("Sulfur", 2) }),
                ("Aguanta -120 °C (también olas de frío) · meteoritos -60%",
                    new[] { ("Iron", 24), ("Silicon", 12), ("Crystal", 9), ("Sulfur", 4) }),
            });
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        // Se borran las tiendas viejas (la lógica quedó en UpgradeShop / IUpgradeOffer).
        var removed = new List<string>();
        if (oldStation != null) { Object.DestroyImmediate(oldStation); removed.Add("UpgradeStation"); }
        if (oldOxygen != null)
        {
            if (oldOxygen.gameObject == host) Object.DestroyImmediate(oldOxygen);
            else Object.DestroyImmediate(oldOxygen.gameObject); // la estación de O2 desaparece del mapa
            removed.Add("OxygenShop");
        }

        string offers = fullShop ? "O2, mochila y traje" : "solo O2";
        return $"tienda única lista ({offers})" + (removed.Count > 0 ? $"; borrado: {string.Join(", ", removed)}" : "");
    }

    private static void WriteLevels(SerializedProperty list, List<(string description, (string item, int amount)[] cost)> levels)
    {
        list.ClearArray();
        for (int i = 0; i < levels.Count; i++)
        {
            list.InsertArrayElementAtIndex(i);
            SerializedProperty level = list.GetArrayElementAtIndex(i);
            level.FindPropertyRelative("description").stringValue = levels[i].description;
            SerializedProperty cost = level.FindPropertyRelative("cost");
            cost.ClearArray();
            int index = 0;
            foreach (var (item, amount) in levels[i].cost)
            {
                ItemDefinition definition = LoadItem(item);
                if (definition == null) continue;
                cost.InsertArrayElementAtIndex(index);
                SerializedProperty el = cost.GetArrayElementAtIndex(index++);
                el.FindPropertyRelative("item").objectReferenceValue = definition;
                el.FindPropertyRelative("amount").intValue = amount;
            }
        }
    }

    // ======================================================================
    // Eventos + HUD ambiental + título del HUD
    // ======================================================================

    private static string SetupEnvironment()
    {
        GameObject go = GameObject.Find("EnvironmentEvents");
        if (go == null) go = new GameObject("EnvironmentEvents");
        EnvironmentEventManager events = GetOrAdd<EnvironmentEventManager>(go);
        if (go.GetComponent<EnvironmentHUD>() == null) go.AddComponent<EnvironmentHUD>();

        var so = new SerializedObject(events);
        so.FindProperty("biomes").objectReferenceValue = Object.FindFirstObjectByType<BiomeMap>(FindObjectsInactive.Include);
        so.FindProperty("playerUpgrades").objectReferenceValue = Object.FindFirstObjectByType<PlayerUpgrades>(FindObjectsInactive.Include);
        so.FindProperty("shipRepair").objectReferenceValue = Object.FindFirstObjectByType<ShipRepair>(FindObjectsInactive.Include);
        SerializedProperty loot = so.FindProperty("meteorLoot");
        string[] lootItems = { "Crystal", "Silicon", "Sulfur" };
        loot.arraySize = lootItems.Length;
        for (int i = 0; i < lootItems.Length; i++) loot.GetArrayElementAtIndex(i).objectReferenceValue = LoadItem(lootItems[i]);
        so.ApplyModifiedPropertiesWithoutUndo();

        HUD hud = Object.FindFirstObjectByType<HUD>(FindObjectsInactive.Include);
        if (hud != null)
        {
            var hso = new SerializedObject(hud);
            hso.FindProperty("requirementsTitle").stringValue = "PARA DESPEGAR AL PRÓXIMO PLANETA";
            hso.ApplyModifiedPropertiesWithoutUndo();
        }

        ColdExposure cold = Object.FindFirstObjectByType<ColdExposure>(FindObjectsInactive.Include);
        if (cold != null)
        {
            var cso = new SerializedObject(cold);
            cso.FindProperty("upgrades").objectReferenceValue = cold.GetComponent<PlayerUpgrades>();
            cso.ApplyModifiedPropertiesWithoutUndo();
        }
        return "eventos de entorno + HUD ambiental listos";
    }

    // ======================================================================
    // Construcción
    // ======================================================================

    private static string SetupBuilding(BuildCatalog catalog)
    {
        GameObject go = GameObject.Find("BaseBuilding");
        if (go == null)
        {
            go = new GameObject("BaseBuilding");
            go.AddComponent<SaveableEntity>();
        }
        BaseBuildingManager manager = GetOrAdd<BaseBuildingManager>(go);
        BuildModeController controller = GetOrAdd<BuildModeController>(go);

        var mso = new SerializedObject(manager);
        mso.FindProperty("catalog").objectReferenceValue = catalog;
        mso.ApplyModifiedPropertiesWithoutUndo();

        Inventory inventory = Object.FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        var cso = new SerializedObject(controller);
        cso.FindProperty("manager").objectReferenceValue = manager;
        cso.FindProperty("inventory").objectReferenceValue = inventory;
        cso.FindProperty("playerCamera").objectReferenceValue = inventory != null ? inventory.GetComponentInChildren<Camera>(true) : null;
        cso.FindProperty("ground").objectReferenceValue = GameObject.Find("Ground")?.GetComponent<Collider>();
        cso.ApplyModifiedPropertiesWithoutUndo();
        return "construcción lista (tecla B)";
    }

    private static BuildCatalog CreateCatalog()
    {
        EnsureFolder(BuildFolder);
        EnsureFolder(MaterialFolder);

        string catalogPath = BuildFolder + "/BuildCatalog.asset";
        BuildCatalog catalog = AssetDatabase.LoadAssetAtPath<BuildCatalog>(catalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<BuildCatalog>();
            AssetDatabase.CreateAsset(catalog, catalogPath);
        }

        catalog.hull = LitMaterial("Build_Hull", new Color(0.82f, 0.84f, 0.88f), 0.4f, 0.55f, 0f);
        catalog.trim = LitMaterial("Build_Trim", new Color(1f, 0.5f, 0.15f), 0.2f, 0.4f, 0f);
        catalog.glass = LitMaterial("Build_Glass", new Color(0.45f, 0.8f, 1f), 0.1f, 0.9f, 1.2f);
        catalog.floor = LitMaterial("Build_Floor", new Color(0.26f, 0.28f, 0.31f), 0.3f, 0.3f, 0f);
        catalog.glow = LitMaterial("Build_Glow", new Color(0.3f, 0.95f, 1f), 0f, 0.5f, 3f);
        catalog.hologramValid = TransparentMaterial("Build_HologramValid", new Color(0.3f, 1f, 0.5f, 0.35f));
        catalog.hologramInvalid = TransparentMaterial("Build_HologramInvalid", new Color(1f, 0.25f, 0.2f, 0.35f));

        var previous = new List<BuildableDefinition>(catalog.buildables);
        catalog.buildables = new List<BuildableDefinition>
        {
            Buildable("Habitat", "habitat", "Hábitat", BuildableKind.Habitat, false,
                "Módulo de 6 x 6 m con puerta. Pegá otro al costado y se unen por dentro. Necesita un generador de O2 para tener aire.",
                ("Iron", 20), ("Silicon", 6)),
            Buildable("OxygenGenerator", "oxygen_generator", "Generador de O2", BuildableKind.OxygenGenerator, true,
                "Va adentro de un hábitat: le da aire a todos los módulos unidos. Ahí se recarga el O2 y no hace frío.",
                ("Iron", 3), ("Crystal", 2), ("Silicon", 1), ("Sulfur", 1)),
            Buildable("Storage", "storage", "Cofre", BuildableKind.Storage, false,
                "Guarda 20 pilas de recursos sin tope de items. Se abre con E.",
                ("Iron", 8)),
            Buildable("Lamp", "lamp", "Lámpara", BuildableKind.Lamp, false,
                "Luz para la base o para marcar caminos de noche.",
                ("Iron", 3), ("Crystal", 2)),
        };
        // Lo que agregaron fases posteriores (fábricas, etc.) se conserva.
        foreach (BuildableDefinition extra in previous)
            if (extra != null && !catalog.buildables.Contains(extra)) catalog.buildables.Add(extra);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        return catalog;
    }

    private static BuildableDefinition Buildable(string file, string id, string name, BuildableKind kind, bool inside, string description,
        params (string item, int amount)[] cost)
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
        definition.kind = kind;
        definition.mustBeInsideHabitat = inside;
        definition.description = description;
        definition.cost = new List<ItemAmount>();
        foreach (var (item, amount) in cost)
        {
            ItemDefinition itemDefinition = LoadItem(item);
            if (itemDefinition != null) definition.cost.Add(new ItemAmount { item = itemDefinition, amount = amount });
        }
        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static Material LitMaterial(string name, Color color, float metallic, float smoothness, float emission)
    {
        Material material = LoadOrCreateMaterial(name);
        material.SetColor("_BaseColor", color);
        material.color = color;
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        if (emission > 0f)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * emission);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material TransparentMaterial(string name, Color color)
    {
        Material material = LoadOrCreateMaterial(name);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        material.SetColor("_BaseColor", color);
        material.color = color;
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", new Color(color.r, color.g, color.b) * 0.6f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material LoadOrCreateMaterial(string name)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    /// <summary>Crea la carpeta en el AssetDatabase (CreateAsset falla si la carpeta no está importada).</summary>
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    private static ItemDefinition LoadItem(string name) =>
        AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemFolder}/{name}.asset");

    // ======================================================================
    // Nidos hundidos
    // ======================================================================

    /// <summary>Baja la parte visible de cada nido hasta que quede apenas enterrada (no flota).</summary>
    private static string SinkNests()
    {
        Collider ground = GameObject.Find("Ground")?.GetComponent<Collider>();
        if (ground == null) return "no hay Ground";

        int moved = 0, total = 0;
        foreach (CreatureNest nest in Object.FindObjectsByType<CreatureNest>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            total++;
            Renderer[] renderers = nest.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) continue;
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);

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

            float sink = Mathf.Max(b.size.y * 0.15f, 0.3f);
            float delta = (lowest - sink) - b.min.y;
            if (delta >= -0.01f) continue;

            // Se mueven solo las rocas del nido: el punto de aparición de las criaturas queda igual.
            foreach (Transform child in nest.transform) child.position += Vector3.up * delta;
            moved++;
        }
        return $"{moved}/{total} nidos bajados para que no floten";
    }
}
#endif
