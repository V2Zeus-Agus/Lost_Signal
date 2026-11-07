#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Expande la escena Mars al triple (1200 x 1200 m) sin mover nada de lo existente:
///  - La zona original del terreno se copia vértice por vértice (mismas alturas).
///  - Norte lejano: mesetas/montaña árida con polvo en suspensión.
///  - Sur lejano: zona fría (cada vez más fría cuanto más al sur, sin nieve).
///  - Centro y costados: el ambiente de siempre.
///  - Recursos nuevos: Silicio (poco común) y Azufre (raro), en vetas según el bioma.
/// Se puede volver a correr: regenera solo lo que crea (todo bajo el objeto "Expansion").
/// </summary>
public static class MarsExpansionSetup
{
    private const string MarsScenePath = "Assets/Scenes/Mars.unity";
    private const string Root = "Assets/Project";
    private const string SoFolder = Root + "/Scriptable_objects";
    private const string MatFolder = Root + "/Materials";
    private const string NodeFolder = Root + "/Prefab/Nodes";
    private const string KayKitFolder = "Assets/Unity_assets/KayKit Space Base Bits 1.0/Assets/fbx(unity)/";
    private const string TerrainMeshPath = Root + "/Art/MarsTerrain_Large.asset";
    private const string NavMeshPath = "Assets/Scenes/NavMesh-Mars.asset";

    // Terreno original (LostSignalSetup): 460 m de lado, vértices cada 2.5 m.
    private const float OldTerrainHalf = 230f;
    private const float OldMapHalf = 200f;
    private const float GroundTop = 0.5f;
    private const float Step = 2.5f;

    private const float NewMapHalf = 600f;            // 3x el mapa original (400 → 1200)
    private const float NewTerrainHalf = NewMapHalf + 30f;

    // Biomas (eje Z): coinciden con BiomeMap
    private const float NorthStart = 260f, NorthFull = 380f;
    private const float SouthStart = -260f, SouthFull = -380f;

    private enum Biome { Base, North, South }

    [MenuItem("Tools/Lost Signal/Mars/Expandir mapa (x3 + biomas + recursos)")]
    public static void Expand()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MarsScenePath) == null)
        {
            EditorUtility.DisplayDialog("Mars", "No encontré Assets/Scenes/Mars.unity. Corré primero Fase 0 → 0 - Crear flujo.", "OK");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(MarsScenePath);
        GameObject ground = GameObject.Find("Ground");
        if (ground == null || ground.GetComponent<MeshCollider>() == null)
        {
            EditorUtility.DisplayDialog("Mars", "Falta el objeto Ground con MeshCollider en Mars.", "OK");
            return;
        }

        try
        {
            EditorUtility.DisplayProgressBar("Mars", "Recursos nuevos (Silicio y Azufre)...", 0.05f);
            ItemDefinition iron = LoadItem("Iron");
            ItemDefinition crystal = LoadItem("Crystal");
            ItemDefinition fuel = LoadItem("Fuel");
            ItemDefinition silicon = EnsureItem("Silicon", "silicon", "Silicio", new Color(0.35f, 1f, 0.25f));
            ItemDefinition sulfur = EnsureItem("Sulfur", "sulfur", "Azufre", new Color(1f, 0.9f, 0.15f));

            GameObject ironNode = AssetDatabase.LoadAssetAtPath<GameObject>(NodeFolder + "/IronNode.prefab");
            GameObject crystalNode = AssetDatabase.LoadAssetAtPath<GameObject>(NodeFolder + "/CrystalNode.prefab");
            GameObject fuelNode = AssetDatabase.LoadAssetAtPath<GameObject>(NodeFolder + "/FuelNode.prefab");
            GameObject siliconNode = EnsureNodePrefab("SiliconNode", ironNode, silicon, NodeMaterial("SiliconOre", new Color(0.3f, 0.95f, 0.2f), 0.2f, 0.5f, emissive: true));
            GameObject sulfurNode = EnsureNodePrefab("SulfurNode", crystalNode, sulfur, NodeMaterial("SulfurOre", new Color(1f, 0.85f, 0.1f), 0.1f, 0.35f, emissive: true));

            EditorUtility.DisplayProgressBar("Mars", "Terreno x3 con biomas...", 0.15f);
            BuildTerrain(ground);

            EditorUtility.DisplayProgressBar("Mars", "Límites del mapa...", 0.35f);
            RemoveOldBorderHills();
            MoveWalls();

            GameObject old = GameObject.Find("Expansion");
            if (old != null) Object.DestroyImmediate(old);
            Transform expansion = new GameObject("Expansion").transform;
            Collider groundCollider = ground.GetComponent<MeshCollider>();

            EditorUtility.DisplayProgressBar("Mars", "Decoración de los biomas...", 0.45f);
            int decor = BuildDecor(expansion, groundCollider);

            EditorUtility.DisplayProgressBar("Mars", "Vetas de recursos...", 0.6f);
            var tables = new Dictionary<Biome, (GameObject prefab, float weight, int min, int max)[]>
            {
                // Centro y costados: hierro y combustible comunes, cristal y silicio poco comunes, azufre raro
                [Biome.Base] = new[] { (ironNode, 34f, 4, 6), (fuelNode, 34f, 4, 6), (crystalNode, 14f, 3, 4), (siliconNode, 13f, 3, 4), (sulfurNode, 5f, 2, 3) },
                // Mesetas áridas: el silicio abunda, algo más de azufre
                [Biome.North] = new[] { (ironNode, 28f, 4, 6), (fuelNode, 18f, 3, 5), (crystalNode, 10f, 3, 4), (siliconNode, 32f, 3, 5), (sulfurNode, 12f, 2, 3) },
                // Zona fría: más cristal, casi nada de azufre
                [Biome.South] = new[] { (ironNode, 30f, 4, 6), (fuelNode, 30f, 4, 6), (crystalNode, 26f, 3, 4), (siliconNode, 10f, 3, 4), (sulfurNode, 4f, 2, 3) },
            };
            Dictionary<string, int> placed = PlaceResourceVeins(expansion, groundCollider, tables);

            EditorUtility.DisplayProgressBar("Mars", "Ambiente por bioma...", 0.7f);
            SetupBiomeMap();
            SetupInventoryHUD();

            EditorUtility.DisplayProgressBar("Mars", "Base de datos de items y planeta...", 0.75f);
            Phase0Setup.ConfigureOpenScene(); // ItemDatabase con los items nuevos + ids de guardado
            UpdateMarsPlanet(new[] { iron, crystal, fuel, silicon, sulfur }, sulfur);

            EditorUtility.DisplayProgressBar("Mars", "NavMesh (puede tardar)...", 0.85f);
            RebakeNavMesh(ground);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            var sb = new System.Text.StringBuilder("Mars expandido a 1200 x 1200 m.\n\nVetas nuevas:\n");
            foreach (var pair in placed) sb.AppendLine($"• {pair.Key}: {pair.Value} nodos");
            sb.AppendLine($"\n• Decoración nueva: {decor} piezas");
            sb.AppendLine("• Biomas: Mesetas de Polvo (norte) y Cuenca Fría (sur)");
            Debug.Log("[Mars] " + sb);
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Mars", sb.ToString(), "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // ======================================================================
    // Costo de la nave en Mars
    // ======================================================================

    private const int ShipCostMultiplier = 7;

    [MenuItem("Tools/Lost Signal/Mars/Costo de la nave (x7 + Silicio + Azufre)")]
    public static void ApplyShipCost()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.OpenScene(MarsScenePath);

        ShipRepair repair = Object.FindFirstObjectByType<ShipRepair>(FindObjectsInactive.Include);
        if (repair == null)
        {
            EditorUtility.DisplayDialog("Mars", "No encontré ShipRepair en Mars.", "OK");
            return;
        }

        // Costo base (el que pedía la nave antes) x7, más los recursos nuevos.
        (ItemDefinition item, int amount)[] cost =
        {
            (LoadItem("Iron"), 15 * ShipCostMultiplier),
            (LoadItem("Crystal"), 20 * ShipCostMultiplier),
            (LoadItem("Fuel"), 15 * ShipCostMultiplier),
            (LoadItem("Silicon"), 20),
            (LoadItem("Sulfur"), 5),
        };

        var so = new SerializedObject(repair);
        SerializedProperty reqs = so.FindProperty("requirements");
        reqs.ClearArray();
        var summary = new System.Text.StringBuilder();
        int index = 0;
        foreach (var (item, amount) in cost)
        {
            if (item == null)
            {
                Debug.LogWarning("[Mars] Falta un recurso (¿corriste Expandir mapa para crear Silicio y Azufre?).");
                continue;
            }
            reqs.InsertArrayElementAtIndex(index);
            SerializedProperty el = reqs.GetArrayElementAtIndex(index);
            el.FindPropertyRelative("resourceType").objectReferenceValue = item;
            el.FindPropertyRelative("amount").intValue = amount;
            summary.AppendLine($"• {item.displayName}: {amount}");
            index++;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Mars] Costo de la nave:\n" + summary);
        EditorUtility.DisplayDialog("Mars", "Nuevo costo de la nave en Mars:\n" + summary, "OK");
    }

    // ======================================================================
    // Tienda de mochila
    // ======================================================================

    [MenuItem("Tools/Lost Signal/Mars/Tienda de mochila (+10 items, máx 50)")]
    public static void CreateBackpackShop()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.OpenScene(MarsScenePath);

        OxygenShop oxygenShop = Object.FindFirstObjectByType<OxygenShop>(FindObjectsInactive.Include);
        GameObject ground = GameObject.Find("Ground");
        if (oxygenShop == null || ground == null)
        {
            EditorUtility.DisplayDialog("Mars", "No encontré la estación de O2 o el Ground en Mars.", "OK");
            return;
        }

        // Se reemplaza si ya existía (para poder volver a correr la herramienta).
        GameObject old = GameObject.Find("BackpackShop");
        if (old != null) Object.DestroyImmediate(old);

        // Misma forma que la estación de O2, con otro color.
        GameObject shop = Object.Instantiate(oxygenShop.gameObject);
        shop.name = "BackpackShop";
        Object.DestroyImmediate(shop.GetComponent<OxygenShop>());
        foreach (SaveableEntity entity in shop.GetComponentsInChildren<SaveableEntity>()) Object.DestroyImmediate(entity);

        Vector3 flat = new Vector3(12.5f, 0f, -8.5f);
        Collider groundCollider = ground.GetComponent<Collider>();
        float y = RaycastHeight(groundCollider, flat.x, flat.z, out float h) ? h : oxygenShop.transform.position.y;
        float lift = oxygenShop.transform.position.y - (RaycastHeight(groundCollider, oxygenShop.transform.position.x, oxygenShop.transform.position.z, out float oh) ? oh : oxygenShop.transform.position.y);
        shop.transform.position = new Vector3(flat.x, y + lift, flat.z);

        Material mat = NodeMaterial("BackpackShop", new Color(1f, 0.55f, 0.15f), 0.3f, 0.4f, emissive: true);
        foreach (Renderer r in shop.GetComponentsInChildren<Renderer>())
        {
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
        }

        UpgradeStation station = shop.AddComponent<UpgradeStation>();
        ItemDefinition iron = LoadItem("Iron"), silicon = LoadItem("Silicon"), sulfur = LoadItem("Sulfur");
        var so = new SerializedObject(station);
        so.FindProperty("upgradeId").stringValue = UpgradeIds.Backpack;
        so.FindProperty("displayName").stringValue = "Mejorar mochila";
        PlayerUpgrades upgrades = Object.FindFirstObjectByType<PlayerUpgrades>(FindObjectsInactive.Include);
        so.FindProperty("playerUpgrades").objectReferenceValue = upgrades;

        SerializedProperty levels = so.FindProperty("levels");
        levels.ClearArray();
        AddLevel(levels, 0, "+10 items (40)", (iron, 9), (silicon, 3));
        AddLevel(levels, 1, "+10 items (50)", (iron, 12), (silicon, 6), (sulfur, 2));
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = shop;
        EditorUtility.DisplayDialog("Mars",
            "Tienda de mochila creada al lado de la estación de O2.\n\n" +
            "• Nivel 1 (40 items): 9 Hierro + 3 Silicio\n" +
            "• Nivel 2 (50 items): 12 Hierro + 6 Silicio + 2 Azufre\n\n" +
            "Los costos se pueden cambiar en el Inspector (UpgradeStation).", "OK");
    }

    private static void AddLevel(SerializedProperty levels, int index, string description, params (ItemDefinition item, int amount)[] cost)
    {
        levels.InsertArrayElementAtIndex(index);
        SerializedProperty level = levels.GetArrayElementAtIndex(index);
        level.FindPropertyRelative("description").stringValue = description;
        SerializedProperty list = level.FindPropertyRelative("cost");
        list.ClearArray();
        for (int i = 0; i < cost.Length; i++)
        {
            list.InsertArrayElementAtIndex(i);
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("item").objectReferenceValue = cost[i].item;
            entry.FindPropertyRelative("amount").intValue = cost[i].amount;
        }
    }

    // ======================================================================
    // Ajustes de balance: clima, stamina, nidos, recursos, mochila, silicio
    // ======================================================================

    private const int NorthNests = 1;
    private const float NestSpacing = 110f;
    private const float NestMinDistanceFromBase = 70f;

    [MenuItem("Tools/Lost Signal/Mars/Ajustes: clima, nidos, recursos, mochila")]
    public static void ApplyBalanceUpdate()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var report = new System.Text.StringBuilder();

        // 1) Silicio con un color que contraste mucho con el hierro (verde lima).
        ItemDefinition silicon = EnsureItem("Silicon", "silicon", "Silicio", new Color(0.35f, 1f, 0.25f));
        NodeMaterial("SiliconOre", new Color(0.3f, 0.95f, 0.2f), 0.2f, 0.5f, emissive: true);
        report.AppendLine("• Silicio: verde lima (nodos y HUD)");

        // 2) Temperaturas de los biomas.
        string biomeFolder = SoFolder + "/Biomes";
        SetBiomeTemperature(biomeFolder + "/Mars_Base.asset", -10f);
        SetBiomeTemperature(biomeFolder + "/Mars_DustMesas.asset", 15f);
        SetBiomeTemperature(biomeFolder + "/Mars_ColdBasin.asset", -100f);
        report.AppendLine("• Temperaturas: centro -10 °C, norte 15 °C, sur -100 °C");

        // 3) Stamina en el tutorial (la escena Mars se ajusta abajo).
        Scene tutorial = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        SetStaminaDrain();
        EditorSceneManager.MarkSceneDirty(tutorial);
        EditorSceneManager.SaveScene(tutorial);

        Scene scene = EditorSceneManager.OpenScene(MarsScenePath);
        SetStaminaDrain();
        report.AppendLine("• Stamina: se gasta 4 veces más lento (recarga igual)");

        GameObject ground = GameObject.Find("Ground");
        GameObject ship = GameObject.Find("Ship");
        Collider groundCollider = ground != null ? ground.GetComponent<Collider>() : null;
        Vector3 shipFlat = ship != null ? new Vector3(ship.transform.position.x, 0f, ship.transform.position.z) : Vector3.zero;

        // 4) BiomeMap: el extremo sur también a -100.
        BiomeMap biomeMap = Object.FindFirstObjectByType<BiomeMap>(FindObjectsInactive.Include);
        if (biomeMap != null)
        {
            var so = new SerializedObject(biomeMap);
            so.FindProperty("deepSouthTemperature").floatValue = -100f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 5) Frío extremo: visor congelado + daño muy lento.
        Inventory inventory = Object.FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (inventory != null && inventory.GetComponent<ColdExposure>() == null)
            inventory.gameObject.AddComponent<ColdExposure>();
        report.AppendLine("• Frío: el visor se congela; tras 20 s, -1 de vida cada 4 s");

        // 6) Mochila 40% más barata.
        foreach (UpgradeStation station in Object.FindObjectsByType<UpgradeStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(station);
            if (so.FindProperty("upgradeId").stringValue != UpgradeIds.Backpack) continue;
            SerializedProperty levels = so.FindProperty("levels");
            levels.ClearArray();
            AddLevel(levels, 0, "+10 items (40)", (LoadItem("Iron"), 9), (silicon, 3));
            AddLevel(levels, 1, "+10 items (50)", (LoadItem("Iron"), 12), (silicon, 6), (LoadItem("Sulfur"), 2));
            so.ApplyModifiedPropertiesWithoutUndo();
            report.AppendLine("• Mochila: 9 Hierro + 3 Silicio / 12 Hierro + 6 Silicio + 2 Azufre");
        }

        // 7) Nidos repartidos por todo el mapa: ninguno al sur, uno solo al norte.
        if (groundCollider != null)
            report.AppendLine(RedistributeNests(groundCollider, shipFlat));

        // 8) Vetas extra: +40 hierro y +30 silicio.
        if (groundCollider != null)
        {
            Transform parent = GameObject.Find("Expansion")?.transform;
            GameObject oldExtra = GameObject.Find("ExtraVeins");
            if (oldExtra != null) Object.DestroyImmediate(oldExtra);
            Transform extra = new GameObject("ExtraVeins").transform;
            if (parent != null) extra.SetParent(parent);

            Random.InitState(777);
            var centers = new List<Vector3>();
            int iron = PlaceExtraVeins(extra, groundCollider, shipFlat, AssetDatabase.LoadAssetAtPath<GameObject>(NodeFolder + "/IronNode.prefab"), 40, 5, centers);
            int si = PlaceExtraVeins(extra, groundCollider, shipFlat, AssetDatabase.LoadAssetAtPath<GameObject>(NodeFolder + "/SiliconNode.prefab"), 30, 5, centers);
            report.AppendLine($"• Vetas extra: +{iron} Hierro, +{si} Silicio");
        }

        Phase0Setup.AssignSaveIds();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[Mars] Ajustes aplicados:\n" + report);
        EditorUtility.DisplayDialog("Mars", "Ajustes aplicados:\n" + report, "OK");
    }

    private static void SetBiomeTemperature(string path, float celsius)
    {
        BiomeDefinition biome = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(path);
        if (biome == null) return;
        biome.temperatureCelsius = celsius;
        EditorUtility.SetDirty(biome);
    }

    private static void SetStaminaDrain()
    {
        foreach (Stamina stamina in Object.FindObjectsByType<Stamina>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(stamina);
            so.FindProperty("drainPerSecond").floatValue = 6.25f; // antes 25: dura el cuádruple
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static string RedistributeNests(Collider ground, Vector3 shipFlat)
    {
        CreatureNest[] nests = Object.FindObjectsByType<CreatureNest>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
        if (nests.Length == 0) return "• Nidos: no hay en la escena";

        Random.InitState(2024);
        var chosen = new List<Vector3>();
        int bandTarget = nests.Length - NorthNests;

        // Franja central y costados (ni el sur ni el norte lejano), bien separados entre sí.
        float spacing = NestSpacing;
        for (int attempt = 0; attempt < 4 && chosen.Count < bandTarget; attempt++)
        {
            for (int i = 0; i < 6000 && chosen.Count < bandTarget; i++)
            {
                float x = Random.Range(-NewMapHalf + 30f, NewMapHalf - 30f);
                float z = Random.Range(SouthStart + 40f, NorthStart - 10f); // nada en la Cuenca Fría
                if (!TryNestPoint(ground, shipFlat, x, z, spacing, chosen, out Vector3 p)) continue;
                chosen.Add(p);
            }
            spacing *= 0.8f; // si no entran todos, se relaja la separación
        }

        // Uno solo en las Mesetas de Polvo.
        for (int i = 0; i < 2000 && chosen.Count < nests.Length; i++)
        {
            float x = Random.Range(-NewMapHalf + 60f, NewMapHalf - 60f);
            float z = Random.Range(NorthFull, NewMapHalf - 40f);
            if (!TryNestPoint(ground, shipFlat, x, z, 0f, chosen, out Vector3 p)) continue;
            chosen.Add(p);
            break;
        }

        int moved = 0, removed = 0;
        for (int i = 0; i < nests.Length; i++)
        {
            if (i < chosen.Count) { nests[i].transform.position = chosen[i]; moved++; }
            else { Object.DestroyImmediate(nests[i].gameObject); removed++; }
        }
        return $"• Nidos: {moved} repartidos (1 al norte, 0 al sur)" + (removed > 0 ? $", {removed} quitados por falta de lugar" : "");
    }

    private static bool TryNestPoint(Collider ground, Vector3 shipFlat, float x, float z, float spacing, List<Vector3> chosen, out Vector3 point)
    {
        point = Vector3.zero;
        if (Vector3.Distance(new Vector3(x, 0f, z), shipFlat) < NestMinDistanceFromBase) return false;
        foreach (Vector3 c in chosen)
            if (new Vector2(c.x - x, c.z - z).sqrMagnitude < spacing * spacing) return false;
        if (!GroundPoint(ground, x, z, 0.85f, out point)) return false;
        point += Vector3.up * 0.5f;
        return true;
    }

    private static int PlaceExtraVeins(Transform parent, Collider ground, Vector3 shipFlat, GameObject prefab, int total, int perVein, List<Vector3> centers)
    {
        if (prefab == null) return 0;
        int placed = 0;
        for (int tries = 0; tries < 3000 && placed < total; tries++)
        {
            float x = Random.Range(-NewMapHalf + 40f, NewMapHalf - 40f);
            float z = Random.Range(-NewMapHalf + 40f, NewMapHalf - 40f);
            var c = new Vector3(x, 0f, z);
            if (Vector3.Distance(c, shipFlat) < 50f) continue;
            bool tooClose = false;
            foreach (Vector3 other in centers)
                if ((other - c).sqrMagnitude < 60f * 60f) { tooClose = true; break; }
            if (tooClose) continue;

            int here = 0;
            for (int i = 0; i < perVein * 4 && here < perVein && placed < total; i++)
            {
                Vector2 offset = Random.insideUnitCircle * 9f;
                if (!GroundPoint(ground, x + offset.x, z + offset.y, 0.85f, out Vector3 point)) continue;
                var node = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                node.transform.position = point;
                node.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                here++;
                placed++;
            }
            if (here > 0) centers.Add(c);
        }
        return placed;
    }

    // ======================================================================
    // Terreno
    // ======================================================================

    private static void BuildTerrain(GameObject ground)
    {
        MeshCollider oldCollider = ground.GetComponent<MeshCollider>();
        int res = Mathf.RoundToInt(NewTerrainHalf * 2f / Step) + 1;
        float start = -NewTerrainHalf;

        var verts = new Vector3[res * res];
        var uvs = new Vector2[res * res];
        for (int zi = 0; zi < res; zi++)
        {
            for (int xi = 0; xi < res; xi++)
            {
                float wx = start + xi * Step, wz = start + zi * Step;
                float h;
                bool inOld = Mathf.Abs(wx) <= OldTerrainHalf + 0.01f && Mathf.Abs(wz) <= OldTerrainHalf + 0.01f;
                if (inOld && RaycastHeight(oldCollider, wx, wz, out float existing))
                    h = existing; // misma altura exacta que el terreno actual
                else
                    h = NewHeight(wx, wz);

                verts[zi * res + xi] = new Vector3(wx, h, wz);
                // Misma escala de textura que el terreno original (460 m por repetición).
                uvs[zi * res + xi] = new Vector2((wx + OldTerrainHalf) / (OldTerrainHalf * 2f), (wz + OldTerrainHalf) / (OldTerrainHalf * 2f));
            }
        }

        // Tres submallas: base, norte (polvo) y sur (frío), con borde irregular.
        var trisBase = new List<int>();
        var trisNorth = new List<int>();
        var trisSouth = new List<int>();
        for (int zi = 0; zi < res - 1; zi++)
        {
            for (int xi = 0; xi < res - 1; xi++)
            {
                int i = zi * res + xi;
                float cx = start + (xi + 0.5f) * Step, cz = start + (zi + 0.5f) * Step;
                List<int> target;
                switch (GroundBiome(cx, cz))
                {
                    case Biome.North: target = trisNorth; break;
                    case Biome.South: target = trisSouth; break;
                    default: target = trisBase; break;
                }
                target.Add(i); target.Add(i + res); target.Add(i + 1);
                target.Add(i + 1); target.Add(i + res); target.Add(i + res + 1);
            }
        }

        var mesh = new Mesh { name = "MarsTerrain_Large", indexFormat = IndexFormat.UInt32 };
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.subMeshCount = 3;
        mesh.SetTriangles(trisBase, 0);
        mesh.SetTriangles(trisNorth, 1);
        mesh.SetTriangles(trisSouth, 2);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        AssetDatabase.DeleteAsset(TerrainMeshPath);
        AssetDatabase.CreateAsset(mesh, TerrainMeshPath);

        MeshRenderer renderer = ground.GetComponent<MeshRenderer>();
        Material baseMat = renderer.sharedMaterials.Length > 0 ? renderer.sharedMaterials[0] : null;
        renderer.sharedMaterials = new[]
        {
            baseMat,
            TintedCopy(baseMat, "MarsGround_Dust", new Color(1.08f, 0.88f, 0.66f)),
            TintedCopy(baseMat, "MarsGround_Cold", new Color(0.72f, 0.74f, 0.82f))
        };
        ground.GetComponent<MeshFilter>().sharedMesh = mesh;
        ground.GetComponent<MeshCollider>().sharedMesh = mesh;

        // La textura del suelo se repite en espejo para que no se vean costuras en el mapa grande.
        if (baseMat != null && baseMat.mainTexture != null)
        {
            string texPath = AssetDatabase.GetAssetPath(baseMat.mainTexture);
            if (AssetImporter.GetAtPath(texPath) is TextureImporter importer && importer.wrapMode != TextureWrapMode.Mirror)
            {
                importer.wrapMode = TextureWrapMode.Mirror;
                importer.SaveAndReimport();
            }
        }
    }

    /// <summary>Altura fuera de la zona original: el mismo relieve de siempre + los biomas.</summary>
    private static float NewHeight(float x, float z)
    {
        float h = OriginalHeight(x, z);

        // Norte: el terreno sube hacia montañas con mesetas escalonadas.
        float north = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(NorthStart, NorthFull + 40f, z));
        if (north > 0f)
        {
            float rise = Mathf.Clamp01((z - NorthStart) / (NewMapHalf - NorthStart)) * 26f;
            float n = Mathf.PerlinNoise(x * 0.0035f + 11f, z * 0.0035f + 37f) * 4f;
            float level = Mathf.Floor(n);
            float frac = n - level;
            float terraces = (level + Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 1f, frac))) * 7f;
            h += north * (rise + terraces);
        }

        // Sur: lomas redondeadas y cuencas planas.
        float south = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SouthStart, SouthFull - 40f, z));
        if (south > 0f)
        {
            float rolls = (Mathf.PerlinNoise(x * 0.008f + 5f, z * 0.008f + 9f) - 0.5f) * 14f;
            h += south * rolls;
            h = Mathf.Lerp(h, Mathf.Max(h, GroundTop - 1f), south); // fondos de cuenca planos
        }

        // Hacia el borde del mapa el terreno sube un poco (acompaña las colinas del límite).
        float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
        h += Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(NewMapHalf - 30f, NewTerrainHalf, edge)) * 12f;
        return h;
    }

    /// <summary>Misma fórmula que LostSignalSetup.TerrainHeight (sin zonas planas, que están dentro del mapa original).</summary>
    private static float OriginalHeight(float x, float z)
    {
        float dunes = (Mathf.PerlinNoise(x * 0.011f + 100f, z * 0.011f + 100f) - 0.5f) * 11f;
        float bumps = (Mathf.PerlinNoise(x * 0.045f + 300f, z * 0.045f + 300f) - 0.5f) * 2.6f;
        float ridges = Mathf.Pow(Mathf.PerlinNoise(x * 0.006f + 700f, z * 0.006f + 700f), 3f) * 9f;
        return GroundTop + dunes + bumps + ridges;
    }

    private static Biome GroundBiome(float x, float z)
    {
        float jitter = (Mathf.PerlinNoise(x * 0.02f + 3f, z * 0.02f + 7f) - 0.5f) * 50f;
        float zj = z + jitter;
        if (zj > (NorthStart + NorthFull) * 0.5f) return Biome.North;
        if (zj < (SouthStart + SouthFull) * 0.5f) return Biome.South;
        return Biome.Base;
    }

    private static Biome BiomeAt(float z) =>
        z > (NorthStart + NorthFull) * 0.5f ? Biome.North : z < (SouthStart + SouthFull) * 0.5f ? Biome.South : Biome.Base;

    private static bool RaycastHeight(Collider collider, float x, float z, out float height)
    {
        var ray = new Ray(new Vector3(x, 1000f, z), Vector3.down);
        if (collider.Raycast(ray, out RaycastHit hit, 3000f))
        {
            height = hit.point.y;
            return true;
        }
        height = 0f;
        return false;
    }

    private static bool GroundPoint(Collider ground, float x, float z, float minNormalY, out Vector3 point)
    {
        var ray = new Ray(new Vector3(x, 1000f, z), Vector3.down);
        if (ground.Raycast(ray, out RaycastHit hit, 3000f) && hit.normal.y >= minNormalY)
        {
            point = hit.point;
            return true;
        }
        point = Vector3.zero;
        return false;
    }

    // ======================================================================
    // Límites
    // ======================================================================

    /// <summary>Saca el anillo de colinas del borde viejo: ahora el mapa sigue más allá.</summary>
    private static void RemoveOldBorderHills()
    {
        GameObject decor = GameObject.Find("Decor");
        if (decor == null) return;
        var remove = new List<GameObject>();
        foreach (Transform child in decor.transform)
        {
            Vector3 p = child.position;
            float edge = Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.z));
            if (edge >= OldMapHalf - 6f && edge <= OldMapHalf + 20f) remove.Add(child.gameObject);
        }
        foreach (GameObject go in remove) Object.DestroyImmediate(go);
    }

    private static void MoveWalls()
    {
        GameObject bounds = GameObject.Find("MapBounds");
        if (bounds == null) bounds = new GameObject("MapBounds");
        var walls = new List<Transform>();
        foreach (Transform wall in bounds.transform) walls.Add(wall);
        foreach (Transform wall in walls) Object.DestroyImmediate(wall.gameObject);

        float h = NewMapHalf;
        Wall(bounds.transform, new Vector3(h + 0.5f, 20f, 0f), new Vector3(1f, 80f, h * 2f + 2f));
        Wall(bounds.transform, new Vector3(-h - 0.5f, 20f, 0f), new Vector3(1f, 80f, h * 2f + 2f));
        Wall(bounds.transform, new Vector3(0f, 20f, h + 0.5f), new Vector3(h * 2f + 2f, 80f, 1f));
        Wall(bounds.transform, new Vector3(0f, 20f, -h - 0.5f), new Vector3(h * 2f + 2f, 80f, 1f));
    }

    private static void Wall(Transform parent, Vector3 position, Vector3 size)
    {
        var wall = new GameObject("Wall", typeof(BoxCollider));
        wall.transform.SetParent(parent);
        wall.transform.position = position;
        wall.GetComponent<BoxCollider>().size = size;
    }

    // ======================================================================
    // Decoración
    // ======================================================================

    private static int BuildDecor(Transform expansion, Collider ground)
    {
        Transform hills = new GameObject("BorderHills").transform;
        hills.SetParent(expansion);
        Transform rocks = new GameObject("Rocks").transform;
        rocks.SetParent(expansion);

        Random.InitState(1234);
        Material dustRock = TintedCopy(KayKitMaterial(), "KayKit_Dust", new Color(1.05f, 0.88f, 0.7f));
        Material coldRock = TintedCopy(KayKitMaterial(), "KayKit_Cold", new Color(0.78f, 0.84f, 0.96f));
        int count = 0;

        // Colinas del nuevo borde
        string[] hillModels = { "terrain_tall", "terrain_tall_curved", "terrain_low_curved" };
        for (float t = -NewMapHalf; t <= NewMapHalf; t += 22f)
        {
            foreach (Vector3 p in new[] { new Vector3(t, 0f, NewMapHalf + 6f), new Vector3(t, 0f, -NewMapHalf - 6f),
                                          new Vector3(NewMapHalf + 6f, 0f, t), new Vector3(-NewMapHalf - 6f, 0f, t) })
            {
                Material mat = MaterialFor(BiomeAt(p.z), dustRock, coldRock);
                if (Prop(hills, hillModels[Random.Range(0, hillModels.Length)], Random.Range(22f, 32f), p, false, ground, mat, sink: Random.Range(0f, 3f)) != null)
                    count++;
            }
        }

        // Rocas sueltas en la zona nueva
        string[] rockModels = { "rock_A", "rock_B", "rocks_A", "rocks_B" };
        int placed = 0, tries = 0;
        while (placed < 320 && tries++ < 6000)
        {
            Vector3 p = RandomNewAreaPoint(15f);
            Biome biome = BiomeAt(p.z);
            float size = biome == Biome.North ? Random.Range(5f, 11f) : Random.Range(3.5f, 8f);
            if (Prop(rocks, rockModels[Random.Range(0, rockModels.Length)], size, p, true, ground, MaterialFor(biome, dustRock, coldRock)) != null)
            {
                placed++;
                count++;
            }
        }

        // Formaciones grandes de meseta en el norte
        string[] mesaModels = { "terrain_tall", "terrain_tall_curved", "terrain_low_curved" };
        placed = 0; tries = 0;
        while (placed < 45 && tries++ < 2000)
        {
            Vector3 p = new Vector3(Random.Range(-NewMapHalf + 30f, NewMapHalf - 30f), 0f, Random.Range(NorthFull, NewMapHalf - 30f));
            if (Prop(rocks, mesaModels[Random.Range(0, mesaModels.Length)], Random.Range(16f, 30f), p, true, ground, dustRock, sink: Random.Range(1f, 4f)) != null)
            {
                placed++;
                count++;
            }
        }
        return count;
    }

    private static Material MaterialFor(Biome biome, Material dust, Material cold) =>
        biome == Biome.North ? dust : biome == Biome.South ? cold : null;

    /// <summary>Un punto al azar en la zona NUEVA del mapa (fuera del área original).</summary>
    private static Vector3 RandomNewAreaPoint(float edgeMargin)
    {
        while (true)
        {
            float x = Random.Range(-NewMapHalf + edgeMargin, NewMapHalf - edgeMargin);
            float z = Random.Range(-NewMapHalf + edgeMargin, NewMapHalf - edgeMargin);
            if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) > OldMapHalf + 15f) return new Vector3(x, 0f, z);
        }
    }

    private static GameObject Prop(Transform parent, string model, float size, Vector3 flat, bool solid, Collider ground,
                                   Material mat, float sink = 0f)
    {
        if (!GroundPoint(ground, flat.x, flat.z, 0f, out Vector3 point)) return null;

        var go = new GameObject(model);
        go.transform.SetParent(parent);
        go.transform.position = point + Vector3.down * sink;
        go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        Bounds b = AddModel(go.transform, KayKitFolder + model + ".fbx", size, mat);
        if (solid) FitBoxCollider(go, b);
        return go;
    }

    // ======================================================================
    // Recursos
    // ======================================================================

    private static Dictionary<string, int> PlaceResourceVeins(Transform expansion, Collider ground,
        Dictionary<Biome, (GameObject prefab, float weight, int min, int max)[]> tables)
    {
        Transform nodes = new GameObject("ResourceVeins").transform;
        nodes.SetParent(expansion);
        Random.InitState(4242);

        var counts = new Dictionary<string, int>();
        var centers = new List<Vector3>();
        int veins = 0, tries = 0;

        while (veins < 70 && tries++ < 4000)
        {
            Vector3 c = RandomNewAreaPoint(30f);
            // Vetas separadas entre sí
            bool tooClose = false;
            foreach (Vector3 other in centers)
                if ((other - c).sqrMagnitude < 45f * 45f) { tooClose = true; break; }
            if (tooClose) continue;

            var table = tables[BiomeAt(c.z)];
            var pick = PickWeighted(table);
            if (pick.prefab == null) continue;

            int amount = Random.Range(pick.min, pick.max + 1);
            int placedHere = 0;
            for (int i = 0; i < amount * 4 && placedHere < amount; i++)
            {
                Vector2 offset = Random.insideUnitCircle * 9f;
                if (!GroundPoint(ground, c.x + offset.x, c.z + offset.y, 0.85f, out Vector3 point)) continue;
                var node = (GameObject)PrefabUtility.InstantiatePrefab(pick.prefab, nodes);
                node.transform.position = point;
                node.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                placedHere++;
            }

            if (placedHere == 0) continue;
            centers.Add(c);
            veins++;

            ResourceNode resource = pick.prefab.GetComponent<ResourceNode>();
            string name = resource != null && resource.Item != null ? resource.Item.displayName : pick.prefab.name;
            counts[name] = (counts.TryGetValue(name, out int n) ? n : 0) + placedHere;
        }
        return counts;
    }

    private static (GameObject prefab, float weight, int min, int max) PickWeighted((GameObject prefab, float weight, int min, int max)[] table)
    {
        float total = 0f;
        foreach (var e in table) total += e.weight;
        float r = Random.Range(0f, total);
        foreach (var e in table)
        {
            if (r < e.weight) return e;
            r -= e.weight;
        }
        return table[table.Length - 1];
    }

    // ======================================================================
    // Items y prefabs
    // ======================================================================

    private static ItemDefinition LoadItem(string assetName) =>
        AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{SoFolder}/ResourceTypes/{assetName}.asset");

    private static ItemDefinition EnsureItem(string assetName, string id, string displayName, Color color)
    {
        string path = $"{SoFolder}/ResourceTypes/{assetName}.asset";
        ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemDefinition>();
            AssetDatabase.CreateAsset(item, path);
        }
        item.id = id;
        item.displayName = displayName;
        item.uiColor = color;
        item.category = ItemCategory.RawResource;
        EditorUtility.SetDirty(item);
        return item;
    }

    private static GameObject EnsureNodePrefab(string name, GameObject template, ItemDefinition item, Material material)
    {
        string path = $"{NodeFolder}/{name}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(template), path);

        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            ResourceNode node = contents.GetComponent<ResourceNode>();
            var so = new SerializedObject(node);
            so.FindProperty("resourceType").objectReferenceValue = item;
            so.ApplyModifiedPropertiesWithoutUndo();

            foreach (Renderer r in contents.GetComponentsInChildren<Renderer>())
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = material;
                r.sharedMaterials = mats;
            }
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static Material NodeMaterial(string name, Color color, float metallic, float smoothness, bool emissive = false)
    {
        string path = $"{MatFolder}/{name}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smoothness);
        if (emissive)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * 0.6f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material KayKitMaterial() => AssetDatabase.LoadAssetAtPath<Material>($"{MatFolder}/KayKit_SpaceBits.mat");

    private static Material TintedCopy(Material source, string name, Color tint)
    {
        if (source == null) return null;
        string path = $"{MatFolder}/{name}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(source);
            AssetDatabase.CreateAsset(m, path);
        }
        else
        {
            m.CopyPropertiesFromMaterial(source);
        }
        Color baseColor = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : Color.white;
        m.SetColor("_BaseColor", new Color(baseColor.r * tint.r, baseColor.g * tint.g, baseColor.b * tint.b, baseColor.a));
        EditorUtility.SetDirty(m);
        return m;
    }

    // ======================================================================
    // Biomas, HUD, planeta y navmesh
    // ======================================================================

    private static void SetupBiomeMap()
    {
        string folder = SoFolder + "/Biomes";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(SoFolder, "Biomes");

        BiomeDefinition baseBiome = EnsureBiome(folder, "Mars_Base", "Llanuras de Marte",
            new Color(0.55f, 0.32f, 0.22f), 0.0045f, new Color(1f, 0.9f, 0.8f), 1.3f, 0f, -10f);
        BiomeDefinition north = EnsureBiome(folder, "Mars_DustMesas", "Mesetas de Polvo",
            new Color(0.74f, 0.52f, 0.33f), 0.012f, new Color(1f, 0.82f, 0.62f), 1.05f, 1f, 15f);
        BiomeDefinition south = EnsureBiome(folder, "Mars_ColdBasin", "Cuenca Fría",
            new Color(0.46f, 0.49f, 0.56f), 0.0065f, new Color(0.82f, 0.88f, 1f), 1.1f, 0.1f, -100f);

        GameObject go = GameObject.Find("BiomeMap");
        if (go == null) go = new GameObject("BiomeMap");
        BiomeMap map = go.GetComponent<BiomeMap>();
        if (map == null) map = go.AddComponent<BiomeMap>();

        var so = new SerializedObject(map);
        so.FindProperty("baseBiome").objectReferenceValue = baseBiome;
        so.FindProperty("northBiome").objectReferenceValue = north;
        so.FindProperty("southBiome").objectReferenceValue = south;
        so.FindProperty("northStart").floatValue = NorthStart;
        so.FindProperty("northFull").floatValue = NorthFull;
        so.FindProperty("southStart").floatValue = SouthStart;
        so.FindProperty("southFull").floatValue = SouthFull;
        so.FindProperty("mapSouthEdge").floatValue = -NewMapHalf + 10f;
        so.FindProperty("dustMaterial").objectReferenceValue = DustMaterial();
        Inventory inventory = Object.FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (inventory != null) so.FindProperty("player").objectReferenceValue = inventory.transform;
        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (light.type == LightType.Directional) { so.FindProperty("sun").objectReferenceValue = light; break; }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static BiomeDefinition EnsureBiome(string folder, string assetName, string displayName, Color fog, float density,
                                               Color sun, float intensity, float dust, float temperature)
    {
        string path = $"{folder}/{assetName}.asset";
        BiomeDefinition b = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(path);
        if (b == null)
        {
            b = ScriptableObject.CreateInstance<BiomeDefinition>();
            AssetDatabase.CreateAsset(b, path);
        }
        b.displayName = displayName;
        b.fogColor = fog;
        b.fogDensity = density;
        b.sunColor = sun;
        b.sunIntensity = intensity;
        b.dust = dust;
        b.dustColor = new Color(0.88f, 0.66f, 0.45f, 0.4f);
        b.temperatureCelsius = temperature;
        EditorUtility.SetDirty(b);
        return b;
    }

    private static Material DustMaterial()
    {
        string texPath = Root + "/Art/DustParticle.png";
        if (!File.Exists(Path.GetFullPath(texPath)))
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            tex.Apply();
            File.WriteAllBytes(Path.GetFullPath(texPath), tex.EncodeToPNG());
            AssetDatabase.ImportAsset(texPath);
            if (AssetImporter.GetAtPath(texPath) is TextureImporter importer)
            {
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }

        string matPath = MatFolder + "/DustParticle.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (m == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, matPath);
        }
        Texture2D t = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        m.SetTexture("_BaseMap", t);
        m.SetColor("_BaseColor", Color.white);
        // Transparente con mezcla alfa
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return m;
    }

    private static void SetupInventoryHUD()
    {
        if (Object.FindFirstObjectByType<InventoryHUD>(FindObjectsInactive.Include) != null) return;
        var go = new GameObject("InventoryHUD");
        InventoryHUD hud = go.AddComponent<InventoryHUD>();
        Inventory inventory = Object.FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        var so = new SerializedObject(hud);
        so.FindProperty("inventory").objectReferenceValue = inventory;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void UpdateMarsPlanet(ItemDefinition[] items, ItemDefinition rare)
    {
        PlanetDefinition mars = AssetDatabase.LoadAssetAtPath<PlanetDefinition>(SoFolder + "/Planets/Mars.asset");
        if (mars == null) return;
        mars.availableResources = new List<ItemDefinition>();
        foreach (ItemDefinition item in items) if (item != null) mars.availableResources.Add(item);
        EditorUtility.SetDirty(mars);
        AssetDatabase.SaveAssets();
    }

    private static void RebakeNavMesh(GameObject ground)
    {
        NavMeshSurface surface = ground.GetComponent<NavMeshSurface>();
        if (surface == null) return;
        surface.BuildNavMesh();
        AssetDatabase.DeleteAsset(NavMeshPath);
        AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
        EditorUtility.SetDirty(surface);
    }

    // ---------- Reutiliza los helpers de modelos de LostSignalSetup ----------

    private static Bounds AddModel(Transform parent, string path, float size, Material mat)
    {
        MethodInfo m = typeof(LostSignalSetup).GetMethod("AddModelAtPath", BindingFlags.NonPublic | BindingFlags.Static);
        return (Bounds)m.Invoke(null, new object[] { parent, path, size, mat });
    }

    private static void FitBoxCollider(GameObject go, Bounds bounds)
    {
        MethodInfo m = typeof(LostSignalSetup).GetMethod("FitBoxCollider", BindingFlags.NonPublic | BindingFlags.Static);
        m.Invoke(null, new object[] { go, bounds });
    }
}
#endif
