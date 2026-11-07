#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ajusta la escena del tutorial (SampleScene) sin reconstruirla:
///  - Achica el mapa al 40% (los objetos se acercan al centro y se apoyan en el terreno).
///  - Deja 5 nidos, menos de la mitad de los recursos y de las rocas.
///  - La nave pide 5 de cada recurso.
///  - Agrega la guía (TutorialGuide) y actualiza el texto de "Cómo jugar".
///  - Saca el guardado del tutorial (no tiene que pisar la partida de Mars).
/// La nave, la base, las estaciones y todo lo demás quedan como están.
/// </summary>
public static class TutorialSetup
{
    private const string TutorialScenePath = "Assets/Scenes/SampleScene.unity";
    private const string TutorialNavMeshPath = "Assets/Scenes/NavMesh-Tutorial.asset";
    private const string LayoutMarkerName = "__TutorialLayoutApplied";

    private const float OriginalMapSize = 400f;
    private const float MapScale = 0.4f;          // 60% más chico
    private const float KeepNodesFraction = 0.4f;  // menos de la mitad de los recursos
    private const float KeepRocksFraction = 0.4f;  // la decoración acompaña el tamaño del mapa
    private const int NestCount = 5;
    private const int RequiredPerResource = 5;

    private const string TutorialText =
        "Tu nave se estrelló. Para despegar necesitás 5 de Hierro, 5 de Cristal y 5 de Combustible.\n" +
        "Al empezar vas a ver marcadores sobre todo lo importante, y arriba a la derecha tus objetivos paso a paso.\n" +
        "Tu O2 se gasta fuera de la cúpula de la nave: volvé a recargarlo. Cuidado con los nidos violetas.\n\n" +
        "WASD moverse · Shift correr · Espacio saltar · Mouse mirar · Click disparar · 2/Q cambiar arma · E recolectar/depositar · Esc pausa";

    [MenuItem("Tools/Lost Signal/Tutorial/Ajustar tutorial (mapa chico + guía)")]
    public static void Apply()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.OpenScene(TutorialScenePath);

        GameObject ground = GameObject.Find("Ground");
        GameObject ship = GameObject.Find("Ship");
        GameObject player = GameObject.Find("Player");
        if (ground == null || ship == null || player == null)
        {
            EditorUtility.DisplayDialog("Tutorial", "Faltan Ground, Ship o Player en SampleScene.", "OK");
            return;
        }

        // Versiones anteriores dejaban una marca en la escena: ya no se usa.
        GameObject oldMarker = GameObject.Find(LayoutMarkerName);
        if (oldMarker != null) Undo.DestroyObjectImmediate(oldMarker);

        // Se decide mirando el mapa real: si los muros siguen en el borde original, falta achicar.
        bool layoutAlreadyApplied = CurrentMapHalf() < OriginalMapSize * 0.5f * 0.75f;
        string layoutReport = "El mapa ya estaba achicado: no se volvió a escalar.";
        if (!layoutAlreadyApplied) layoutReport = ShrinkLayout(ground, ship, player);

        int hillsAdded = EnsureBorderHills(ground.GetComponent<Collider>());
        if (hillsAdded > 0) layoutReport += $"\n• Borde completado con {hillsAdded} colinas";

        SetRepairAmounts(ship);
        UpdateTutorialText();
        EnsurePlayerUpgrades(player);
        RemoveSaveSystems();
        EnsureGuide();

        if (!layoutAlreadyApplied || hillsAdded > 0) RebakeNavMesh(ground);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[Tutorial] " + layoutReport);
        EditorUtility.DisplayDialog("Tutorial", "Tutorial ajustado:\n" + layoutReport +
            "\n• La nave pide 5 de cada recurso\n• Guía de objetivos agregada\n• El tutorial ya no guarda partida", "OK");
    }

    // ---------- Mapa ----------

    private static float CurrentMapHalf()
    {
        GameObject bounds = GameObject.Find("MapBounds");
        if (bounds == null) return OriginalMapSize * 0.5f;
        float max = 0f;
        foreach (Transform wall in bounds.transform)
            max = Mathf.Max(max, Mathf.Abs(wall.position.x), Mathf.Abs(wall.position.z));
        return max;
    }

    private static string ShrinkLayout(GameObject ground, GameObject ship, GameObject player)
    {
        Collider groundCollider = ground.GetComponent<Collider>();
        Vector3 shipFlat = Flat(ship.transform.position);
        Vector3 playerFlat = Flat(player.transform.position);
        var random = new System.Random(42);

        // Límites del mapa
        float newHalf = OriginalMapSize * MapScale * 0.5f;
        GameObject bounds = GameObject.Find("MapBounds");
        if (bounds != null)
        {
            foreach (Transform wall in bounds.transform)
            {
                BoxCollider box = wall.GetComponent<BoxCollider>();
                Vector3 p = wall.position;
                bool alongX = Mathf.Abs(p.x) > Mathf.Abs(p.z);
                wall.position = alongX
                    ? new Vector3(Mathf.Sign(p.x) * (newHalf + 0.5f), p.y, 0f)
                    : new Vector3(0f, p.y, Mathf.Sign(p.z) * (newHalf + 0.5f));
                if (box != null)
                    box.size = alongX ? new Vector3(1f, box.size.y, newHalf * 2f + 2f)
                                      : new Vector3(newHalf * 2f + 2f, box.size.y, 1f);
            }
        }

        // Nave rota
        GameObject wreck = GameObject.Find("WreckedShip");
        Vector3 wreckFlat = wreck != null ? Flat(wreck.transform.position) * MapScale : Vector3.zero;
        if (wreck != null) MoveScaled(wreck.transform, groundCollider);

        // Nodos de recursos: se queda el 40% de cada tipo
        int nodesBefore = 0, nodesAfter = 0;
        var nodesByType = new Dictionary<ItemDefinition, List<ResourceNode>>();
        foreach (ResourceNode node in Object.FindObjectsByType<ResourceNode>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID))
        {
            if (node.Item == null) continue;
            if (!nodesByType.TryGetValue(node.Item, out var list))
                nodesByType[node.Item] = list = new List<ResourceNode>();
            list.Add(node);
        }
        foreach (var pair in nodesByType)
        {
            List<ResourceNode> list = pair.Value;
            nodesBefore += list.Count;
            int keep = Mathf.Max(RequiredPerResource + 2, Mathf.FloorToInt(list.Count * KeepNodesFraction));
            Shuffle(list, random);
            int kept = 0;
            foreach (ResourceNode node in list)
            {
                Vector3 target = Flat(node.transform.position) * MapScale;
                bool blocked = Vector3.Distance(target, shipFlat) < 12f
                               || Vector3.Distance(target, playerFlat) < 6f
                               || (wreck != null && Vector3.Distance(target, wreckFlat) < 9f);
                if (kept >= keep || blocked)
                {
                    Undo.DestroyObjectImmediate(node.gameObject);
                    continue;
                }
                MoveScaled(node.transform, groundCollider);
                kept++;
            }
            nodesAfter += kept;
        }

        // Nidos: quedan 5, repartidos y lejos de la base
        CreatureNest[] allNests = Object.FindObjectsByType<CreatureNest>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
        var candidates = allNests
            .Where(n => Vector3.Distance(Flat(n.transform.position) * MapScale, shipFlat) >= 28f
                     && (wreck == null || Vector3.Distance(Flat(n.transform.position) * MapScale, wreckFlat) >= 12f))
            .ToList();
        List<CreatureNest> chosen = FarthestPointSample(candidates, NestCount, shipFlat);
        foreach (CreatureNest nest in allNests)
        {
            if (chosen.Contains(nest)) MoveScaled(nest.transform, groundCollider);
            else Undo.DestroyObjectImmediate(nest.gameObject);
        }

        // Decoración: colinas del borde (se mantiene la mitad) y rocas sueltas (40%)
        int rocksKept = 0, hillsKept = 0;
        GameObject decor = GameObject.Find("Decor");
        if (decor != null)
        {
            var children = new List<Transform>();
            foreach (Transform child in decor.transform) children.Add(child);

            int hillIndex = 0;
            foreach (Transform child in children)
            {
                Vector3 p = child.position;
                bool isHill = Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.z)) >= OriginalMapSize * 0.5f - 6f;
                Vector3 target = Flat(p) * MapScale;

                if (isHill)
                {
                    // Se crearon de a 4 por paso (arriba, abajo, derecha, izquierda): se salta un paso de cada dos.
                    if ((hillIndex++ / 4) % 2 != 0) { Undo.DestroyObjectImmediate(child.gameObject); continue; }
                    MoveScaled(child, groundCollider);
                    hillsKept++;
                }
                else
                {
                    bool blocked = Vector3.Distance(target, shipFlat) < 22f
                                   || Vector3.Distance(target, playerFlat) < 10f
                                   || (wreck != null && Vector3.Distance(target, wreckFlat) < 12f);
                    if (blocked || random.NextDouble() > KeepRocksFraction)
                    {
                        Undo.DestroyObjectImmediate(child.gameObject);
                        continue;
                    }
                    MoveScaled(child, groundCollider);
                    rocksKept++;
                }
            }
        }

        return $"• Mapa al {MapScale * 100f:0}% ({OriginalMapSize * MapScale:0} m de lado)\n" +
               $"• Nidos: {allNests.Length} → {chosen.Count}\n" +
               $"• Nodos de recursos: {nodesBefore} → {nodesAfter}\n" +
               $"• Decoración: {rocksKept} rocas, {hillsKept} colinas de borde";
    }

    /// <summary>
    /// Si un lado del borde quedó sin colinas, lo completa copiando en espejo las del lado opuesto.
    /// </summary>
    private static int EnsureBorderHills(Collider ground)
    {
        GameObject decor = GameObject.Find("Decor");
        if (decor == null) return 0;

        float edge = OriginalMapSize * MapScale * 0.5f;
        var sides = new Dictionary<string, List<Transform>>
        {
            { "top", new List<Transform>() }, { "bottom", new List<Transform>() },
            { "right", new List<Transform>() }, { "left", new List<Transform>() }
        };

        foreach (Transform child in decor.transform)
        {
            Vector3 p = child.position;
            if (p.z > edge - 6f) sides["top"].Add(child);
            else if (p.z < -edge + 6f) sides["bottom"].Add(child);
            else if (p.x > edge - 6f) sides["right"].Add(child);
            else if (p.x < -edge + 6f) sides["left"].Add(child);
        }

        int added = 0;
        added += MirrorSide(sides["bottom"], sides["top"], decor.transform, ground, mirrorX: false);
        added += MirrorSide(sides["top"], sides["bottom"], decor.transform, ground, mirrorX: false);
        added += MirrorSide(sides["left"], sides["right"], decor.transform, ground, mirrorX: true);
        added += MirrorSide(sides["right"], sides["left"], decor.transform, ground, mirrorX: true);
        return added;
    }

    private static int MirrorSide(List<Transform> target, List<Transform> source, Transform parent, Collider ground, bool mirrorX)
    {
        if (target.Count >= 3 || source.Count == 0) return 0;

        int added = 0;
        foreach (Transform original in source)
        {
            Vector3 p = original.position;
            float offset = p.y - GroundY(ground, p.x, p.z, p.y);
            float x = mirrorX ? -p.x : p.x;
            float z = mirrorX ? p.z : -p.z;

            GameObject copy = Object.Instantiate(original.gameObject, parent);
            copy.name = original.name;
            copy.transform.position = new Vector3(x, GroundY(ground, x, z, p.y) + offset, z);
            copy.transform.rotation = original.rotation * Quaternion.Euler(0f, 180f, 0f);
            Undo.RegisterCreatedObjectUndo(copy, "Completar borde");
            added++;
        }
        return added;
    }

    /// <summary>Acerca el objeto al centro del mapa y lo apoya en el terreno con la misma altura relativa.</summary>
    private static void MoveScaled(Transform t, Collider ground)
    {
        Vector3 old = t.position;
        float offset = old.y - GroundY(ground, old.x, old.z, old.y);
        float x = old.x * MapScale, z = old.z * MapScale;
        Undo.RecordObject(t, "Achicar tutorial");
        t.position = new Vector3(x, GroundY(ground, x, z, old.y) + offset, z);
    }

    private static float GroundY(Collider ground, float x, float z, float fallback)
    {
        if (ground == null) return fallback;
        var ray = new Ray(new Vector3(x, 500f, z), Vector3.down);
        return ground.Raycast(ray, out RaycastHit hit, 1000f) ? hit.point.y : fallback;
    }

    private static List<CreatureNest> FarthestPointSample(List<CreatureNest> candidates, int count, Vector3 origin)
    {
        var result = new List<CreatureNest>();
        if (candidates.Count == 0) return result;

        // Primero el más lejano a la base, después siempre el más lejano a los ya elegidos.
        CreatureNest first = candidates.OrderByDescending(n => Vector3.Distance(Flat(n.transform.position), origin)).First();
        result.Add(first);
        while (result.Count < count && result.Count < candidates.Count)
        {
            CreatureNest best = null;
            float bestDistance = -1f;
            foreach (CreatureNest c in candidates)
            {
                if (result.Contains(c)) continue;
                float d = result.Min(r => Vector3.Distance(Flat(c.transform.position), Flat(r.transform.position)));
                if (d > bestDistance) { bestDistance = d; best = c; }
            }
            result.Add(best);
        }
        return result;
    }

    private static void RebakeNavMesh(GameObject ground)
    {
        NavMeshSurface surface = ground.GetComponent<NavMeshSurface>();
        if (surface == null) return;

        surface.BuildNavMesh();
        // Asset propio del tutorial: Mars sigue usando NavMesh-Ground.asset.
        AssetDatabase.DeleteAsset(TutorialNavMeshPath);
        AssetDatabase.CreateAsset(surface.navMeshData, TutorialNavMeshPath);
        EditorUtility.SetDirty(surface);
    }

    // ---------- Reglas y textos ----------

    private static void SetRepairAmounts(GameObject ship)
    {
        ShipRepair repair = ship.GetComponent<ShipRepair>();
        if (repair == null) return;
        var so = new SerializedObject(repair);
        SerializedProperty reqs = so.FindProperty("requirements");
        for (int i = 0; i < reqs.arraySize; i++)
            reqs.GetArrayElementAtIndex(i).FindPropertyRelative("amount").intValue = RequiredPerResource;
        so.ApplyModifiedProperties();
    }

    private static void UpdateTutorialText()
    {
        GameScreens screens = Object.FindFirstObjectByType<GameScreens>(FindObjectsInactive.Include);
        if (screens == null) return;
        var so = new SerializedObject(screens);
        GameObject panel = so.FindProperty("tutorialPanel").objectReferenceValue as GameObject;
        Transform body = panel != null ? panel.transform.Find("Body") : null;
        TMP_Text text = body != null ? body.GetComponent<TMP_Text>() : null;
        if (text == null) return;
        Undo.RecordObject(text, "Texto del tutorial");
        text.text = TutorialText;
        EditorUtility.SetDirty(text);
    }

    private static void EnsurePlayerUpgrades(GameObject player)
    {
        // La estación de O2 necesita PlayerUpgrades para vender la mejora.
        PlayerUpgrades upgrades = player.GetComponent<PlayerUpgrades>();
        if (upgrades == null) upgrades = Undo.AddComponent<PlayerUpgrades>(player);

        PlayerOxygen oxygen = player.GetComponent<PlayerOxygen>();
        if (oxygen != null)
        {
            var so = new SerializedObject(oxygen);
            so.FindProperty("upgrades").objectReferenceValue = upgrades;
            so.ApplyModifiedProperties();
        }
        OxygenShop shop = Object.FindFirstObjectByType<OxygenShop>(FindObjectsInactive.Include);
        if (shop != null)
        {
            var so = new SerializedObject(shop);
            so.FindProperty("playerUpgrades").objectReferenceValue = upgrades;
            so.ApplyModifiedProperties();
        }
    }

    private static void RemoveSaveSystems()
    {
        // El tutorial no guarda: si tuviera SaveManager, pisaría la partida de Mars.
        GameObject core = GameObject.Find("CoreSystems");
        if (core != null) Undo.DestroyObjectImmediate(core);
        foreach (SaveManager manager in Object.FindObjectsByType<SaveManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Undo.DestroyObjectImmediate(manager);
    }

    private static void EnsureGuide()
    {
        if (Object.FindFirstObjectByType<TutorialGuide>(FindObjectsInactive.Include) != null) return;
        var go = new GameObject("TutorialGuide");
        Undo.RegisterCreatedObjectUndo(go, "Agregar guía");
        go.AddComponent<TutorialGuide>();
    }

    // ---------- Utilidades ----------

    private static Vector3 Flat(Vector3 p) => new Vector3(p.x, 0f, p.z);

    private static void Shuffle<T>(List<T> list, System.Random random)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
#endif
