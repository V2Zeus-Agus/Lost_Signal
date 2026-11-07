#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

/// <summary>
/// Actualización "inventario + consola + naves caídas":
///  1) Íconos de cada item renderizados desde su propio modelo (el prefab del nodo).
///  2) Inventario interactuable (Tab/I) y consola de desarrollador (F8) en el tutorial y en Mars.
///  3) Mars: dos naves caídas nuevas (norte y sur) que se desarman de a tandas.
///  4) Mars: recursos de la expansión mucho más repartidos (de a pares) y +10 Silicio.
/// Se puede volver a correr: reutiliza lo que ya creó.
/// </summary>
public static class InventoryDevSetup
{
    private const string TutorialScenePath = "Assets/Scenes/SampleScene.unity";
    private const string MarsScenePath = "Assets/Scenes/Mars.unity";
    private const string Root = "Assets/Project";
    private const string IconFolder = Root + "/Art/Icons";
    private const string NodeFolder = Root + "/Prefab/Nodes";
    private const string NavMeshPath = "Assets/Scenes/NavMesh-Mars.asset";

    private const float MapHalf = 600f;
    private const float NorthStart = 260f, NorthFull = 380f;
    private const float SouthStart = -260f, SouthFull = -380f;

    // ======================================================================
    // Todo junto
    // ======================================================================

    [MenuItem("Tools/Lost Signal/Actualizar: inventario, consola F8, naves caídas y recursos")]
    public static void ApplyAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var report = new StringBuilder();
        try
        {
            EditorUtility.DisplayProgressBar("Lost Signal", "Íconos de los items...", 0.05f);
            report.AppendLine(GenerateIconsInternal());

            EditorUtility.DisplayProgressBar("Lost Signal", "Tutorial: inventario y consola...", 0.2f);
            Scene tutorial = EditorSceneManager.OpenScene(TutorialScenePath);
            report.AppendLine("Tutorial: " + SetupPanels());
            EditorSceneManager.MarkSceneDirty(tutorial);
            EditorSceneManager.SaveScene(tutorial);

            EditorUtility.DisplayProgressBar("Lost Signal", "Mars: inventario y consola...", 0.3f);
            Scene mars = EditorSceneManager.OpenScene(MarsScenePath);
            report.AppendLine("Mars: " + SetupPanels());

            EditorUtility.DisplayProgressBar("Lost Signal", "Mars: naves caídas...", 0.4f);
            report.AppendLine(SetupWrecks());

            EditorUtility.DisplayProgressBar("Lost Signal", "Mars: repartiendo recursos...", 0.55f);
            report.AppendLine(SpreadResources());

            Phase0Setup.AssignSaveIds();

            EditorUtility.DisplayProgressBar("Lost Signal", "Mars: NavMesh (puede tardar)...", 0.75f);
            RebakeNavMesh();

            EditorSceneManager.MarkSceneDirty(mars);
            EditorSceneManager.SaveScene(mars);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log("[Lost Signal] Actualización aplicada:\n" + report);
        EditorUtility.DisplayDialog("Lost Signal", report.ToString(), "OK");
    }

    // ======================================================================
    // 1) Íconos desde el modelo propio de cada recurso
    // ======================================================================

    [MenuItem("Tools/Lost Signal/Items/Generar íconos desde los modelos")]
    public static void GenerateIcons()
    {
        string result = GenerateIconsInternal();
        Debug.Log("[Íconos] " + result);
        EditorUtility.DisplayDialog("Íconos", result, "OK");
    }

    private static string GenerateIconsInternal()
    {
        Directory.CreateDirectory(IconFolder);
        Dictionary<ItemDefinition, GameObject> models = FindNodePrefabs();
        var done = new List<string>();

        foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition"))
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (item == null || !models.TryGetValue(item, out GameObject prefab)) continue;

            Texture2D icon = RenderIcon(prefab, 512);
            if (icon == null) continue;

            string path = $"{IconFolder}/{item.name}_Icon.png";
            File.WriteAllBytes(path, icon.EncodeToPNG());
            Object.DestroyImmediate(icon);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 256;
            importer.SaveAndReimport();

            item.icon = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            EditorUtility.SetDirty(item);
            done.Add(item.displayName);
        }

        AssetDatabase.SaveAssets();
        return done.Count > 0
            ? $"• Íconos generados desde el modelo de cada nodo: {string.Join(", ", done)}"
            : "• Íconos: no encontré prefabs con ResourceNode";
    }

    /// <summary>Busca, para cada item, el prefab de nodo que lo da (su modelo "propio").</summary>
    private static Dictionary<ItemDefinition, GameObject> FindNodePrefabs()
    {
        var result = new Dictionary<ItemDefinition, GameObject>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Root }))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            ResourceNode node = prefab != null ? prefab.GetComponent<ResourceNode>() : null;
            if (node == null || node.Item == null) continue;
            // Prioriza los prefabs de la carpeta Nodes.
            bool preferred = AssetDatabase.GetAssetPath(prefab).StartsWith(NodeFolder);
            if (!result.ContainsKey(node.Item) || preferred) result[node.Item] = prefab;
        }
        return result;
    }

    /// <summary>
    /// Renderiza el prefab dos veces (fondo negro y fondo blanco) y calcula la transparencia
    /// por diferencia: funciona igual en URP o Built-in, sin depender del alpha del render target.
    /// </summary>
    private static Texture2D RenderIcon(GameObject prefab, int size)
    {
        Texture2D onBlack = RenderOnce(prefab, size, Color.black);
        Texture2D onWhite = RenderOnce(prefab, size, Color.white);
        if (onBlack == null || onWhite == null) return null;

        int w = Mathf.Min(onBlack.width, onWhite.width);
        int h = Mathf.Min(onBlack.height, onWhite.height);
        Color[] b = onBlack.GetPixels(0, 0, w, h);
        Color[] wh = onWhite.GetPixels(0, 0, w, h);
        var result = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var pixels = new Color[b.Length];
        for (int i = 0; i < b.Length; i++)
        {
            float diff = Mathf.Max(wh[i].r - b[i].r, Mathf.Max(wh[i].g - b[i].g, wh[i].b - b[i].b));
            float a = Mathf.Clamp01(1f - diff);
            if (a < 0.02f)
            {
                pixels[i] = new Color(0f, 0f, 0f, 0f);
                continue;
            }
            pixels[i] = new Color(Mathf.Clamp01(b[i].r / a), Mathf.Clamp01(b[i].g / a), Mathf.Clamp01(b[i].b / a), a);
        }
        result.SetPixels(pixels);
        result.Apply();
        Object.DestroyImmediate(onBlack);
        Object.DestroyImmediate(onWhite);
        return CropToContent(result, 0.08f);
    }

    /// <summary>Recorta el espacio vacío alrededor del modelo (cuadrado, con un margen).</summary>
    private static Texture2D CropToContent(Texture2D source, float padding)
    {
        Color32[] px = source.GetPixels32();
        int w = source.width, h = source.height;
        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (px[y * w + x].a > 8)
                {
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
        if (maxX < 0) return source;

        int side = Mathf.Max(maxX - minX + 1, maxY - minY + 1);
        int pad = Mathf.RoundToInt(side * padding);
        int size = side + pad * 2;
        int cx = (minX + maxX) / 2, cy = (minY + maxY) / 2;
        var cropped = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var outPx = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int sx = cx - size / 2 + x, sy = cy - size / 2 + y;
                outPx[y * size + x] = sx >= 0 && sx < w && sy >= 0 && sy < h ? px[sy * w + sx] : new Color32(0, 0, 0, 0);
            }
        cropped.SetPixels32(outPx);
        cropped.Apply();
        Object.DestroyImmediate(source);
        return cropped;
    }

    private static Texture2D RenderOnce(GameObject prefab, int size, Color background)
    {
        var preview = new PreviewRenderUtility();
        try
        {
            Camera cam = preview.camera;
            cam.fieldOfView = 22f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 200f;

            preview.lights[0].intensity = 1.3f;
            preview.lights[0].transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            preview.lights[1].intensity = 0.7f;
            preview.lights[1].transform.rotation = Quaternion.Euler(-20f, 160f, 0f);
            preview.ambientColor = new Color(0.45f, 0.45f, 0.5f);

            GameObject instance = preview.InstantiatePrefabInScene(prefab);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 25f, 0f));

            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            if (!any) return null;

            float radius = bounds.extents.magnitude;
            float distance = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.02f;
            Vector3 direction = Quaternion.Euler(22f, -30f, 0f) * Vector3.back;
            cam.transform.position = bounds.center + direction * distance;
            cam.transform.LookAt(bounds.center);

            preview.BeginStaticPreview(new Rect(0, 0, size, size));
            preview.Render(true);
            return preview.EndStaticPreview();
        }
        finally
        {
            preview.Cleanup();
        }
    }

    // ======================================================================
    // 2) Inventario interactuable + consola de desarrollador
    // ======================================================================

    private static string SetupPanels()
    {
        Inventory inventory = Object.FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        if (inventory == null) return "no hay Inventory en la escena";

        InventoryPanel panel = Object.FindFirstObjectByType<InventoryPanel>(FindObjectsInactive.Include);
        if (panel == null) panel = new GameObject("InventoryPanel").AddComponent<InventoryPanel>();
        var so = new SerializedObject(panel);
        so.FindProperty("inventory").objectReferenceValue = inventory;
        so.ApplyModifiedPropertiesWithoutUndo();

        DevConsole console = Object.FindFirstObjectByType<DevConsole>(FindObjectsInactive.Include);
        if (console == null) console = new GameObject("DevConsole").AddComponent<DevConsole>();
        so = new SerializedObject(console);
        so.FindProperty("inventory").objectReferenceValue = inventory;
        so.FindProperty("itemDatabase").objectReferenceValue = FindItemDatabase();
        so.ApplyModifiedPropertiesWithoutUndo();

        return "inventario (Tab/I) y consola (F8) listos";
    }

    private static ItemDatabase FindItemDatabase()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:ItemDatabase"))
        {
            ItemDatabase db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(AssetDatabase.GUIDToAssetPath(guid));
            if (db == null) continue;
            db.CollectAllItems(); // que estén los 5 recursos
            return db;
        }
        return null;
    }

    // ======================================================================
    // 3) Naves caídas en el norte y en el sur
    // ======================================================================

    private const string NorthWreckName = "NaveCaida_Norte";
    private const string SouthWreckName = "NaveCaida_Sur";

    private static string SetupWrecks()
    {
        GameObject original = GameObject.Find("WreckedShip");
        Collider ground = GameObject.Find("Ground")?.GetComponent<Collider>();
        if (original == null || ground == null) return "• Naves caídas: falta WreckedShip o Ground en Mars";

        Physics.SyncTransforms();
        var sb = new StringBuilder();
        sb.AppendLine(SetupWreck(original, ground, NorthWreckName, "nave caída del norte",
            new Vector2(-140f, 470f), Quaternion.Euler(0f, 210f, 0f),
            ("Silicon", 20), ("Iron", 20), ("Sulfur", 4), ("Fuel", 8)));
        sb.Append(SetupWreck(original, ground, SouthWreckName, "nave caída del sur",
            new Vector2(150f, -460f), Quaternion.Euler(0f, 120f, 0f),
            ("Crystal", 15), ("Fuel", 20), ("Iron", 12), ("Silicon", 8)));
        return sb.ToString();
    }

    private static string SetupWreck(GameObject original, Collider ground, string name, string displayName,
        Vector2 target, Quaternion rotation, params (string item, int amount)[] rewards)
    {
        GameObject wreck = GameObject.Find(name);
        bool created = wreck == null;
        if (created)
        {
            if (!TryFindFlatSpot(ground, target, out Vector3 spot))
                return $"• {name}: no encontré un lugar plano cerca de {target}";

            wreck = Object.Instantiate(original);
            wreck.name = name;
            wreck.transform.SetPositionAndRotation(spot, rotation);

            // Id de guardado propio (el clon trae el mismo que la nave original).
            SaveableEntity entity = wreck.GetComponent<SaveableEntity>();
            if (entity != null)
            {
                var eso = new SerializedObject(entity);
                eso.FindProperty("id").stringValue = Guid.NewGuid().ToString("N");
                eso.ApplyModifiedPropertiesWithoutUndo();
            }

            // Baliza para encontrarla de lejos.
            var beacon = new GameObject("Beacon");
            beacon.transform.SetParent(wreck.transform, false);
            beacon.transform.localPosition = new Vector3(0f, 6f, 0f);
            Light light = beacon.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.45f, 0.15f);
            light.range = 30f;
            light.intensity = 8f;
        }

        SalvageWreck salvage = wreck.GetComponent<SalvageWreck>();
        var so = new SerializedObject(salvage);
        so.FindProperty("displayName").stringValue = displayName;
        SerializedProperty list = so.FindProperty("rewards");
        list.ClearArray();
        var text = new List<string>();
        for (int i = 0; i < rewards.Length; i++)
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{Root}/Scriptable_objects/ResourceTypes/{rewards[i].item}.asset");
            if (item == null) continue;
            list.InsertArrayElementAtIndex(list.arraySize);
            SerializedProperty el = list.GetArrayElementAtIndex(list.arraySize - 1);
            el.FindPropertyRelative("resourceType").objectReferenceValue = item;
            el.FindPropertyRelative("amount").intValue = rewards[i].amount;
            text.Add($"{rewards[i].amount} {item.displayName}");
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        Vector3 p = wreck.transform.position;
        return $"• {name} {(created ? "creada" : "actualizada")} en ({p.x:0}, {p.z:0}): {string.Join(", ", text)}";
    }

    /// <summary>Busca en espiral un lugar plano y libre de objetos para apoyar la nave.</summary>
    private static bool TryFindFlatSpot(Collider ground, Vector2 target, out Vector3 spot)
    {
        for (int ring = 0; ring < 12; ring++)
        {
            int samples = ring == 0 ? 1 : ring * 8;
            for (int s = 0; s < samples; s++)
            {
                float angle = s * Mathf.PI * 2f / samples;
                Vector2 c = target + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring * 12f;
                if (!GroundPoint(ground, c.x, c.y, 0.96f, out spot)) continue;
                if (!IsFlat(ground, c, 10f, spot.y, 1f)) continue;
                if (HasObstacles(ground, spot, 12f)) continue;
                return true;
            }
        }
        spot = Vector3.zero;
        return false;
    }

    private static bool IsFlat(Collider ground, Vector2 c, float radius, float height, float tolerance)
    {
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            if (!RaycastHeight(ground, c.x + Mathf.Cos(a) * radius, c.y + Mathf.Sin(a) * radius, out float h)) return false;
            if (Mathf.Abs(h - height) > tolerance) return false;
        }
        return true;
    }

    private static bool HasObstacles(Collider ground, Vector3 point, float radius)
    {
        foreach (Collider hit in Physics.OverlapSphere(point + Vector3.up * 2f, radius, ~0, QueryTriggerInteraction.Ignore))
            if (hit != ground) return true;
        return false;
    }

    // ======================================================================
    // 4) Recursos más repartidos + 10 Silicio
    // ======================================================================

    private const int ExtraSilicon = 10;
    private const string SpreadMarker = "SpreadSilicon";

    // Preferencia por bioma de cada recurso (base, norte, sur). 1 = siempre acepta.
    private static readonly Dictionary<string, Vector3> BiomePreference = new Dictionary<string, Vector3>
    {
        ["iron"] = new Vector3(1f, 0.8f, 0.9f),
        ["fuel"] = new Vector3(1f, 0.55f, 0.9f),
        ["crystal"] = new Vector3(0.55f, 0.3f, 1f),
        ["silicon"] = new Vector3(0.45f, 1f, 0.3f),
        ["sulfur"] = new Vector3(0.4f, 1f, 0.15f),
    };

    private static string SpreadResources()
    {
        Collider ground = GameObject.Find("Ground")?.GetComponent<Collider>();
        GameObject expansion = GameObject.Find("Expansion");
        GameObject ship = GameObject.Find("Ship");
        if (ground == null || expansion == null) return "• Recursos: falta Ground o Expansion en Mars";
        Vector3 shipFlat = ship != null ? Flat(ship.transform.position) : Vector3.zero;

        // +10 Silicio (una sola vez: queda marcado con un objeto vacío).
        int addedSilicon = 0;
        if (expansion.transform.Find(SpreadMarker) == null)
        {
            GameObject siliconPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NodeFolder + "/SiliconNode.prefab");
            Transform parent = new GameObject(SpreadMarker).transform;
            parent.SetParent(expansion.transform);
            for (int i = 0; siliconPrefab != null && i < ExtraSilicon; i++)
            {
                PrefabUtility.InstantiatePrefab(siliconPrefab, parent);
                addedSilicon++;
            }
        }

        // Nodos a repartir: todos los de la expansión. Los del mapa original quedan donde estaban.
        var byItem = new Dictionary<string, List<ResourceNode>>();
        var expansionNodes = new HashSet<ResourceNode>(expansion.GetComponentsInChildren<ResourceNode>(true));
        foreach (ResourceNode node in expansionNodes)
        {
            if (node.Item == null) continue;
            if (!byItem.TryGetValue(node.Item.Id, out var list)) byItem[node.Item.Id] = list = new List<ResourceNode>();
            list.Add(node);
        }

        // Puntos que hay que respetar: nodos originales, naves, nave del jugador.
        var fixedPoints = new List<Vector3> { shipFlat };
        foreach (ResourceNode node in Object.FindObjectsByType<ResourceNode>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (!expansionNodes.Contains(node)) fixedPoints.Add(Flat(node.transform.position));
        foreach (SalvageWreck wreck in Object.FindObjectsByType<SalvageWreck>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            fixedPoints.Add(Flat(wreck.transform.position));

        // Grupos de a 2 (un par por punto), intercalando recursos para que no queden todos juntos.
        var groups = new List<List<ResourceNode>>();
        foreach (var pair in byItem)
            for (int i = 0; i < pair.Value.Count; i += 2)
                groups.Add(pair.Value.GetRange(i, Mathf.Min(2, pair.Value.Count - i)));
        Random.InitState(9001);
        for (int i = groups.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            List<ResourceNode> tmp = groups[i];
            groups[i] = groups[j];
            groups[j] = tmp;
        }

        // Separación pensada para cubrir todo el mapa: se ajusta a la cantidad de grupos.
        float area = (MapHalf * 2f - 60f) * (MapHalf * 2f - 60f);
        float spacing = Mathf.Sqrt(area / Mathf.Max(groups.Count, 1)) * 0.75f;

        Physics.SyncTransforms();
        var centers = new List<Vector3>();
        int moved = 0;
        foreach (List<ResourceNode> group in groups)
        {
            string id = group[0].Item.Id;
            Vector3 pref = BiomePreference.TryGetValue(id, out Vector3 v) ? v : Vector3.one;
            float minDistance = spacing;
            bool placed = false;

            for (int attempt = 0; attempt < 600 && !placed; attempt++)
            {
                if (attempt > 0 && attempt % 150 == 0) minDistance *= 0.8f; // si no entra, relaja un poco

                float x = Random.Range(-MapHalf + 30f, MapHalf - 30f);
                float z = Random.Range(-MapHalf + 30f, MapHalf - 30f);
                float chance = z > (NorthStart + NorthFull) * 0.5f ? pref.y
                             : z < (SouthStart + SouthFull) * 0.5f ? pref.z : pref.x;
                if (Random.value > chance) continue;

                var c = new Vector3(x, 0f, z);
                if (Vector3.Distance(c, shipFlat) < 55f) continue;
                if (TooClose(c, centers, minDistance) || TooClose(c, fixedPoints, 22f)) continue;
                if (!GroundPoint(ground, x, z, 0.85f, out Vector3 first) || Blocked(ground, first)) continue;

                // El segundo nodo del par, a unos pasos.
                Vector3 second = first;
                if (group.Count > 1)
                {
                    bool ok = false;
                    for (int k = 0; k < 10 && !ok; k++)
                    {
                        Vector2 off = Random.insideUnitCircle.normalized * Random.Range(3f, 5.5f);
                        ok = GroundPoint(ground, x + off.x, z + off.y, 0.85f, out second) && !Blocked(ground, second);
                    }
                    if (!ok) continue;
                }

                Place(group[0], first);
                if (group.Count > 1) Place(group[1], second);
                centers.Add(c);
                moved += group.Count;
                placed = true;
            }

            // Último recurso: cualquier lugar válido, sin exigir separación.
            for (int attempt = 0; attempt < 400 && !placed; attempt++)
            {
                float x = Random.Range(-MapHalf + 30f, MapHalf - 30f);
                float z = Random.Range(-MapHalf + 30f, MapHalf - 30f);
                if (Vector3.Distance(new Vector3(x, 0f, z), shipFlat) < 55f) continue;
                if (!GroundPoint(ground, x, z, 0.85f, out Vector3 point) || Blocked(ground, point)) continue;
                foreach (ResourceNode node in group) Place(node, point + new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f)));
                moved += group.Count;
                placed = true;
            }
        }

        // Ordena la jerarquía: todo bajo Expansion/ResourceVeins (vacía las carpetas viejas).
        Transform veins = expansion.transform.Find("ResourceVeins");
        if (veins == null) { veins = new GameObject("ResourceVeins").transform; veins.SetParent(expansion.transform); }
        foreach (ResourceNode node in expansionNodes) node.transform.SetParent(veins, true);

        int total = expansionNodes.Count;
        return $"• Recursos: {moved}/{total} nodos de la expansión repartidos de a pares por todo el mapa " +
               $"(~{spacing:0} m entre grupos)" + (addedSilicon > 0 ? $" · +{addedSilicon} Silicio" : "");
    }

    private static void Place(ResourceNode node, Vector3 point)
    {
        node.transform.position = point;
        node.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
    }

    private static bool TooClose(Vector3 c, List<Vector3> points, float distance)
    {
        float sq = distance * distance;
        foreach (Vector3 p in points)
            if ((p.x - c.x) * (p.x - c.x) + (p.z - c.z) * (p.z - c.z) < sq) return true;
        return false;
    }

    /// <summary>Hay una roca, pared o decoración en ese lugar (ignora el suelo y otros nodos).</summary>
    private static bool Blocked(Collider ground, Vector3 point)
    {
        foreach (Collider hit in Physics.OverlapSphere(point + Vector3.up * 1.2f, 1.6f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit == ground || hit.GetComponentInParent<ResourceNode>() != null) continue;
            return true;
        }
        return false;
    }

    // ======================================================================
    // Helpers
    // ======================================================================

    private static Vector3 Flat(Vector3 p) => new Vector3(p.x, 0f, p.z);

    private static bool RaycastHeight(Collider collider, float x, float z, out float height)
    {
        if (collider.Raycast(new Ray(new Vector3(x, 1000f, z), Vector3.down), out RaycastHit hit, 3000f))
        {
            height = hit.point.y;
            return true;
        }
        height = 0f;
        return false;
    }

    private static bool GroundPoint(Collider ground, float x, float z, float minNormalY, out Vector3 point)
    {
        if (ground.Raycast(new Ray(new Vector3(x, 1000f, z), Vector3.down), out RaycastHit hit, 3000f) && hit.normal.y >= minNormalY)
        {
            point = hit.point;
            return true;
        }
        point = Vector3.zero;
        return false;
    }

    private static void RebakeNavMesh()
    {
        GameObject ground = GameObject.Find("Ground");
        NavMeshSurface surface = ground != null ? ground.GetComponent<NavMeshSurface>() : null;
        if (surface == null) return;
        surface.BuildNavMesh();
        AssetDatabase.DeleteAsset(NavMeshPath);
        AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
        EditorUtility.SetDirty(surface);
    }
}
#endif
