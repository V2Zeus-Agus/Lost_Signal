#if UNITY_EDITOR
using System.IO;
using System.Linq;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class LostSignalSetup
{
    private const string Root = "Assets/Project";
    private const string SoFolder = Root + "/Scriptable_objects";
    private const string PrefabFolder = Root + "/Prefab";
    private const string MatFolder = Root + "/Materials";
    private const string InputAsset = "Assets/InputSystem_Actions.inputactions";
    private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const float MapSize = 400f;
    private const float ResourceZoneHalf = 95f;
    private static readonly Vector3 WreckPosition = new Vector3(-62f, 0f, 55f);
    private const string SkeletonModel = "Assets/Unity_assets/KayKit Skeletons 1.1/characters/fbx/Skeleton_Minion.fbx";
    private const string SkeletonTexture = "Assets/Unity_assets/KayKit Skeletons 1.1/textures/skeleton_texture_A.png";
    private const string SkeletonAnims = "Assets/Unity_assets/KayKit Skeletons 1.1/Animations/fbx/Rig_Medium/Rig_Medium_MovementBasic.fbx";
    private const string KayKitFolder = "Assets/Unity_assets/KayKit Space Base Bits 1.0/Assets/fbx(unity)/";
    private const string KayKitTexture = "Assets/Unity_assets/KayKit Space Base Bits 1.0/textures/spacebits_texture.png";
    private const float GroundTop = 0.5f;
    private const float OxygenSafeRadius = 10f;
    private const string TutorialText =
        "Tu nave se estrelló en un planeta hostil. Juntá 15 de Hierro, 20 de Cristal (escaso) y 15 de Combustible y depositalos en la nave para repararla.\n" +
        "Tu oxígeno dura 20 segundos: recargalo dentro de la cúpula de la nave y mejoralo en la tiendita de O2 (3 de Hierro = +8 s, hasta 5 mejoras).\n\n" +
        "WASD moverse · Shift correr · Espacio saltar · Mouse mirar · Click disparar · 2/Q cambiar arma · E recolectar/depositar · Esc pausa";

    [MenuItem("Tools/Lost Signal/1 - Import TMP Essentials")]
    public static void ImportTmp()
    {
        string pkg = Path.GetFullPath("Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage");
        if (File.Exists(pkg)) AssetDatabase.ImportPackage(pkg, false);
        else EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Import TMP Essential Resources");
        Debug.Log("[Setup] TMP Essentials imported. Now run: Tools > Lost Signal > 2 - Setup Scene");
    }

    [MenuItem("Tools/Lost Signal/2 - Setup Scene")]
    public static void Setup()
    {
        AssetDatabase.DeleteAsset(Root + "/Scripts/Enemies/States/IdleState.cs");

        if (EditorSceneManager.GetActiveScene().path == MenuScenePath)
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");

        Folder(Root, "Scriptable_objects"); Folder(SoFolder, "Enemies"); Folder(SoFolder, "ResourceTypes");
        Folder(Root, "Prefab"); Folder(PrefabFolder, "Enemies"); Folder(PrefabFolder, "Projectiles"); Folder(PrefabFolder, "Nodes");
        Folder(Root, "Materials");

        ResourceType iron = ResourceAsset("Iron", "Hierro");
        ResourceType crystal = ResourceAsset("Crystal", "Cristal");
        ResourceType fuel = ResourceAsset("Fuel", "Combustible");
        iron.uiColor = new Color(0.75f, 0.82f, 0.95f); EditorUtility.SetDirty(iron);
        crystal.uiColor = new Color(0.3f, 0.95f, 1f); EditorUtility.SetDirty(crystal);
        fuel.uiColor = new Color(1f, 0.78f, 0.2f); EditorUtility.SetDirty(fuel);

        foreach (string leftover in new[] { "Enemy", "Skeleton_Minion" })
        {
            GameObject go = GameObject.Find(leftover);
            if (go != null) Object.DestroyImmediate(go);
        }
        GameObject creatureGo = new GameObject("Creature");
        Bounds creatureBounds = AddModelAtPath(creatureGo.transform, SkeletonModel, 2.1f, TexturedMaterial("Skeleton", SkeletonTexture));
        SetupWalkAnimation(creatureGo);
        FitBoxCollider(creatureGo, creatureBounds);
        NavMeshAgent agent = GetOrAdd<NavMeshAgent>(creatureGo);
        agent.baseOffset = 0f; agent.radius = 0.6f; agent.height = creatureBounds.size.y;
        Health creatureHealth = GetOrAdd<Health>(creatureGo);
        GetOrAdd<Creature>(creatureGo);
        BuildCreatureHealthBar(creatureGo, creatureHealth, creatureBounds.max.y + 0.4f);
        GameObject creaturePrefab = SavePrefab(creatureGo, PrefabFolder + "/Enemies/Creature.prefab");

        EnemyData enemyData = LoadOrCreate<EnemyData>(SoFolder + "/Enemies/CreatureData.asset");
        enemyData.prefab = creaturePrefab.GetComponent<Creature>();
        enemyData.detectionRange = 30f;
        EditorUtility.SetDirty(enemyData);

        GameObject projGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projGo.name = "Projectile";
        projGo.transform.localScale = Vector3.one * 0.2f;
        projGo.GetComponent<Collider>().isTrigger = true;
        Rigidbody rb = GetOrAdd<Rigidbody>(projGo); rb.isKinematic = true; rb.useGravity = false;
        GetOrAdd<Projectile>(projGo);
        Paint(projGo, Mat("Projectile", new Color(1f, 0.85f, 0.2f), true));
        GameObject projectilePrefab = SavePrefab(projGo, PrefabFolder + "/Projectiles/Projectile.prefab");

        Material ironMat = Mat("IronOre", new Color(0.42f, 0.45f, 0.52f));
        ironMat.SetFloat("_Metallic", 0.85f); ironMat.SetFloat("_Smoothness", 0.55f);
        GameObject ironNode = NodePrefab("IronNode", "rocks_A", 2.6f, ironMat, iron);
        GameObject crystalNode = NodePrefab("CrystalNode", "rock_B", 2.2f, Mat("Crystal", new Color(0.2f, 0.9f, 1f), true), crystal);
        GameObject fuelNode = NodePrefab("FuelNode", "containers_A", 1.8f, Mat("FuelBarrel", new Color(1f, 0.72f, 0.1f), true), fuel);

        GameObject player = GameObject.Find("Player");
        GameObject cam = GameObject.Find("Main Camera");
        GameObject ship = GameObject.Find("Ship");
        GameObject ground = GameObject.Find("Ground");
        if (player == null || cam == null || ship == null || ground == null)
        {
            Debug.LogError("[Setup] Missing scene objects: Player, Main Camera, Ship or Ground.");
            return;
        }

        FlatZones.Clear();
        FlatZones.Add((Flat(ship.transform.position), 26f, 48f));
        FlatZones.Add((Flat(WreckPosition), 12f, 26f));
        BuildTerrain(ground);
        GameObject oldBounds = GameObject.Find("MapBounds"); if (oldBounds != null) Object.DestroyImmediate(oldBounds);
        Transform bounds = new GameObject("MapBounds").transform;
        float half = MapSize * 0.5f;
        Wall(bounds, new Vector3(half + 0.5f, 10f, 0f), new Vector3(1f, 40f, MapSize + 2f));
        Wall(bounds, new Vector3(-half - 0.5f, 10f, 0f), new Vector3(1f, 40f, MapSize + 2f));
        Wall(bounds, new Vector3(0f, 10f, half + 0.5f), new Vector3(MapSize + 2f, 40f, 1f));
        Wall(bounds, new Vector3(0f, 10f, -half - 0.5f), new Vector3(MapSize + 2f, 40f, 1f));
        BuildSkyAndAtmosphere();

        player.tag = "Player";
        CapsuleCollider capsule = player.GetComponent<CapsuleCollider>();
        if (capsule != null) Object.DestroyImmediate(capsule);
        CharacterController cc = GetOrAdd<CharacterController>(player);
        cc.height = 2f; cc.radius = 0.5f; cc.center = Vector3.zero;

        Transform pivot = player.transform.Find("CameraPivot");
        if (pivot == null) { pivot = new GameObject("CameraPivot").transform; pivot.SetParent(player.transform, false); }
        pivot.localPosition = new Vector3(0f, 0.6f, 0f); pivot.localRotation = Quaternion.identity;
        cam.transform.SetParent(pivot, false);
        cam.transform.localPosition = Vector3.zero; cam.transform.localRotation = Quaternion.identity;

        Transform weaponT = cam.transform.Find("Weapon");
        if (weaponT == null) { weaponT = new GameObject("Weapon").transform; weaponT.SetParent(cam.transform, false); }
        weaponT.localPosition = new Vector3(0.3f, -0.3f, 0.7f); weaponT.localRotation = Quaternion.identity;
        Transform gunT = weaponT.Find("GunModel");
        if (gunT == null)
        {
            GameObject gun = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gun.name = "GunModel";
            Object.DestroyImmediate(gun.GetComponent<Collider>());
            gun.transform.SetParent(weaponT, false);
            Paint(gun, Mat("Gun", new Color(0.2f, 0.2f, 0.25f)));
            gunT = gun.transform;
        }
        gunT.localScale = new Vector3(0.05f, 0.06f, 0.3f);
        Transform muzzle = weaponT.Find("Muzzle");
        if (muzzle == null) { muzzle = new GameObject("Muzzle").transform; muzzle.SetParent(weaponT, false); }
        muzzle.localPosition = new Vector3(0f, 0f, 0.3f);
        muzzle.localRotation = Quaternion.LookRotation(new Vector3(-0.3f, 0.3f, 29f));

        PlayerController pc = GetOrAdd<PlayerController>(player);
        Health playerHealth = GetOrAdd<Health>(player);
        Inventory inventory = GetOrAdd<Inventory>(player);
        PlayerInteractor interactor = GetOrAdd<PlayerInteractor>(player);
        PlayerInputHandler input = GetOrAdd<PlayerInputHandler>(player);
        Weapon weapon = GetOrAdd<Weapon>(weaponT.gameObject);
        Stamina stamina = GetOrAdd<Stamina>(player);
        PlayerOxygen oxygen = GetOrAdd<PlayerOxygen>(player);
        Set(pc, "stamina", stamina);
        SetFloat(stamina, "regenPerSecond", 24f);
        SetFloat(pc, "gravity", -10f);
        SetFloat(pc, "jumpHeight", 2.4f);
        Set(oxygen, "safeZoneCenter", ship.transform);
        Set(oxygen, "health", playerHealth);
        SetFloat(oxygen, "maxOxygen", 20f);
        SetFloat(oxygen, "safeZoneRadius", OxygenSafeRadius);

        GameObject old = GameObject.Find("Systems"); if (old != null) Object.DestroyImmediate(old);
        Transform systems = new GameObject("Systems").transform;

        ProjectilePool pool = new GameObject("ProjectilePool").AddComponent<ProjectilePool>();
        pool.transform.SetParent(systems);
        Set(pool, "projectilePrefab", projectilePrefab.GetComponent<Projectile>());

        EnemyFactory factory = new GameObject("EnemyFactory").AddComponent<EnemyFactory>();
        factory.transform.SetParent(systems);
        Set(factory, "enemyData", enemyData);
        Set(factory, "player", player.transform);
        SetInt(factory, "maxCreatures", 28);

        var nestList = new System.Collections.Generic.List<Vector3>();
        (float radius, int count, float offset)[] rings = { (60f, 8, 22.5f), (110f, 10, 0f), (165f, 12, 15f) };
        foreach (var ring in rings)
        {
            for (int i = 0; i < ring.count; i++)
            {
                float a = (ring.offset + i * 360f / ring.count) * Mathf.Deg2Rad;
                nestList.Add(new Vector3(Mathf.Cos(a) * ring.radius, 0.5f, Mathf.Sin(a) * ring.radius));
            }
        }

        Vector3 safeCenter = Flat(player.transform.position);
        nestList.RemoveAll(n => Vector3.Distance(Flat(n), safeCenter) < 45f
                             || Vector3.Distance(Flat(n), Flat(ship.transform.position)) < 30f
                             || Vector3.Distance(Flat(n), Flat(WreckPosition)) < 25f);
        Vector3[] nestPositions = nestList.ToArray();
        Material nestMat = Mat("Nest", new Color(0.5f, 0.1f, 0.6f));
        for (int i = 0; i < nestPositions.Length; i++)
        {
            GameObject nest = new GameObject("CreatureNest_" + (i + 1));
            nest.transform.SetParent(systems);
            nest.transform.position = OnGround(nestPositions[i]);
            BuildNaturalNest(nest.transform);
            CreatureNest nestComp = nest.AddComponent<CreatureNest>();
            Set(nestComp, "factory", factory);
            SetInt(nestComp, "maxAlive", 2);
            SetFloat(nestComp, "spawnInterval", 10f);
        }

        BuildShipArt(ship, player);

        ShipRepair repair = GetOrAdd<ShipRepair>(ship);
        CrashedShip crashed = GetOrAdd<CrashedShip>(ship);
        Set(crashed, "shipRepair", repair);
        SerializedObject repairSo = new SerializedObject(repair);
        SerializedProperty reqs = repairSo.FindProperty("requirements");
        reqs.ClearArray();
        ResourceType[] types = { iron, crystal, fuel };
        for (int i = 0; i < types.Length; i++)
        {
            reqs.InsertArrayElementAtIndex(i);
            SerializedProperty el = reqs.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("resourceType").objectReferenceValue = types[i];
            el.FindPropertyRelative("amount").intValue = types[i] == crystal ? 20 : 15;
        }
        repairSo.ApplyModifiedPropertiesWithoutUndo();

        GameManager gm = new GameObject("GameManager").AddComponent<GameManager>();
        gm.transform.SetParent(systems);
        Set(gm, "playerHealth", playerHealth);
        Set(gm, "shipRepair", repair);

        GameAudio gameAudio = new GameObject("GameAudio").AddComponent<GameAudio>();
        gameAudio.transform.SetParent(systems);
        Set(gameAudio, "weapon", weapon);
        Set(gameAudio, "player", pc);
        Set(gameAudio, "oxygen", oxygen);
        Set(gameAudio, "shipRepair", repair);
        Set(gameAudio, "enemyFactory", factory);
        Set(gameAudio, "shoot", Clip("sfx_shoot"));
        Set(gameAudio, "jump", Clip("sfx_jump"));
        Set(gameAudio, "enemyDeath", Clip("sfx_enemy_death"));
        Set(gameAudio, "oxygenRefillLoop", Clip("sfx_oxygen_refill"));
        Set(gameAudio, "repair", Clip("sfx_repair"));
        Set(gameAudio, "ambience", Clip("music_ambience"));
        Set(gameAudio, "playerHealth", playerHealth);
        Set(gameAudio, "hurt", Clip("sfx_hurt"));
        Set(gameAudio, "heal", Clip("sfx_heal"));
        SerializedObject audioSo = new SerializedObject(gameAudio);
        SerializedProperty steps = audioSo.FindProperty("footsteps");
        steps.arraySize = 3;
        for (int i = 0; i < 3; i++) steps.GetArrayElementAtIndex(i).objectReferenceValue = Clip("sfx_step_" + (i + 1));
        audioSo.ApplyModifiedPropertiesWithoutUndo();

        GameObject oldShop = GameObject.Find("OxygenShop"); if (oldShop != null) Object.DestroyImmediate(oldShop);
        GameObject shop = new GameObject("OxygenShop");
        Vector3 toPlayer = Flat(player.transform.position) - Flat(ship.transform.position);
        Vector3 side = Vector3.Cross(Vector3.up, toPlayer.normalized);
        shop.transform.position = Flat(ship.transform.position) + toPlayer.normalized * 7f + side * 3f + Vector3.up * GroundTop;
        FitBoxCollider(shop, AddModel(shop.transform, "water_storage", 2.4f));
        OxygenShop shopComp = shop.AddComponent<OxygenShop>();
        Set(shopComp, "playerOxygen", oxygen);
        Set(shopComp, "costType", iron);
        SetFloat(shopComp, "secondsPerUpgrade", 8f);
        SetInt(shopComp, "costPerUpgrade", 3);

        GameObject oldMed = GameObject.Find("HealStation"); if (oldMed != null) Object.DestroyImmediate(oldMed);
        GameObject med = new GameObject("HealStation");
        med.transform.position = Flat(ship.transform.position) + toPlayer.normalized * 6f - side * 4f + Vector3.up * GroundTop;
        med.transform.rotation = Quaternion.LookRotation(-toPlayer.normalized);
        FitBoxCollider(med, AddModel(med.transform, "basemodule_B", 3.2f));
        GameObject medLight = new GameObject("GreenLight");
        medLight.transform.SetParent(med.transform, false);
        medLight.transform.localPosition = new Vector3(0f, 3.5f, 0f);
        Light ml = medLight.AddComponent<Light>();
        ml.type = LightType.Point; ml.color = new Color(0.3f, 1f, 0.45f); ml.range = 6f; ml.intensity = 2.5f;
        HealStation heal = med.AddComponent<HealStation>();
        Set(heal, "playerHealth", playerHealth);
        SetInt(heal, "maxUses", 2);
        SetInt(shopComp, "maxUpgrades", 5);

        BuildWreck(iron, crystal, fuel);

        GameObject oldNodes = GameObject.Find("Nodes"); if (oldNodes != null) Object.DestroyImmediate(oldNodes);
        Transform nodes = new GameObject("Nodes").transform;
        Random.InitState(42);
        GameObject[] nodePrefabs = { ironNode, crystalNode, fuelNode };
        int[] nodeCounts = { 34, 28, 34 };
        for (int k = 0; k < nodePrefabs.Length; k++)
        {
            GameObject prefab = nodePrefabs[k];
            int placed = 0, tries = 0;
            while (placed < nodeCounts[k] && tries++ < 1500)
            {
                Vector3 p = new Vector3(Random.Range(-ResourceZoneHalf, ResourceZoneHalf), 0f, Random.Range(-ResourceZoneHalf, ResourceZoneHalf));
                if (Vector3.Distance(Flat(p), Flat(ship.transform.position)) < 12f) continue;
                if (Vector3.Distance(Flat(p), Flat(player.transform.position)) < 5f) continue;
                if (Vector3.Distance(Flat(p), Flat(WreckPosition)) < 14f) continue;
                GameObject n = (GameObject)PrefabUtility.InstantiatePrefab(prefab, nodes);
                n.transform.position = OnGround(p);
                n.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                placed++;
            }
        }

        Set(pc, "cameraPivot", pivot);
        Set(interactor, "playerCamera", cam.GetComponent<Camera>());
        Set(interactor, "inventory", inventory);
        Set(weapon, "muzzle", muzzle);
        Set(weapon, "projectilePool", pool);

        var refs = AssetDatabase.LoadAllAssetsAtPath(InputAsset).OfType<InputActionReference>().ToArray();
        Set(input, "moveAction", Ref(refs, "Move"));
        Set(input, "lookAction", Ref(refs, "Look"));
        Set(input, "jumpAction", Ref(refs, "Jump"));
        Set(input, "sprintAction", Ref(refs, "Sprint"));
        Set(input, "attackAction", Ref(refs, "Attack"));
        Set(input, "interactAction", Ref(refs, "Interact"));
        Set(input, "switchWeaponAction", Ref(refs, "Next"));
        Set(input, "pauseAction", Ref(refs, "Pause"));
        Set(input, "playerController", pc);
        Set(input, "weapon", weapon);
        Set(input, "interactor", interactor);

        BuildUI(playerHealth, inventory, repair, weapon, interactor, stamina, oxygen);

        BuildDecor(ship, player);

        NavMeshSurface surface = GetOrAdd<NavMeshSurface>(ground);
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();
        string navPath = "Assets/Scenes/NavMesh-Ground.asset";
        AssetDatabase.DeleteAsset(navPath);
        AssetDatabase.CreateAsset(surface.navMeshData, navPath);

        var scene = EditorSceneManager.GetActiveScene();
        string gameScenePath = scene.path;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        BuildMainMenuScene(scene.name);

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MenuScenePath, true),
            new EditorBuildSettingsScene(gameScenePath, true)
        };
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(MenuScenePath);
        Debug.Log("[Setup] Done! Scenes MainMenu + " + scene.name + " are ready. Press Play from MainMenu.");
    }

    private static void BuildUI(Health health, Inventory inventory, ShipRepair repair, Weapon weapon, PlayerInteractor interactor, Stamina stamina, PlayerOxygen oxygen)
    {
        GameObject oldCanvas = GameObject.Find("UI Canvas"); if (oldCanvas != null) Object.DestroyImmediate(oldCanvas);

        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        GameObject canvasGo = NewCanvas("UI Canvas");
        Transform c = canvasGo.transform;

        Image redTint = FullScreenImage(c, "DamageTint", UISprite("damage_vignette"), new Color(1f, 1f, 1f, 0.55f));
        Image crack = FullScreenImage(c, "ScreenCrack", UISprite("screen_crack"), new Color(1f, 1f, 1f, 0.85f));
        DamageOverlay overlay = canvasGo.AddComponent<DamageOverlay>();
        Set(overlay, "playerHealth", health);
        Set(overlay, "crack", crack);
        Set(overlay, "redTint", redTint);

        Text(c, "HealthLabel", "VIDA", 26, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(40, -30), new Vector2(200, 30));
        Slider healthBar = Bar(c, "HealthBar", new Color(0.85f, 0.2f, 0.2f), new Vector2(0, 1), new Vector2(40, -65), new Vector2(400, 26));
        Text(c, "StaminaLabel", "STAMINA", 20, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(40, -98), new Vector2(200, 24));
        Slider staminaBar = Bar(c, "StaminaBar", new Color(1f, 0.8f, 0.2f), new Vector2(0, 1), new Vector2(40, -122), new Vector2(400, 14));

        TMP_Text oxygenText = Text(c, "OxygenText", "O2", 32, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(-400, 165), new Vector2(800, 44));
        Slider oxygenBar = Bar(c, "OxygenBar", new Color(0.3f, 0.75f, 1f), new Vector2(0.5f, 0), new Vector2(-350, 115), new Vector2(700, 30));
        TMP_Text resources = Text(c, "ResourcesText", "", 24, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(40, -150), new Vector2(900, 160));
        TMP_Text repairText = Text(c, "RepairText", "Nave: 0%", 30, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(-300, -25), new Vector2(600, 40));
        Slider repairBar = Bar(c, "RepairBar", new Color(0.2f, 0.85f, 0.4f), new Vector2(0.5f, 1), new Vector2(-300, -70), new Vector2(600, 22));
        TMP_Text notification = Text(c, "NotificationText", "", 36, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(-450, -110), new Vector2(900, 60));
        notification.color = new Color(1f, 0.9f, 0.3f);
        Text(c, "Crosshair", "+", 40, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(-25, 25), new Vector2(50, 50));
        TMP_Text prompt = Text(c, "PromptText", "", 30, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(-400, -60), new Vector2(800, 50));
        TMP_Text weaponText = Text(c, "WeaponText", "", 30, TextAlignmentOptions.Right, new Vector2(1, 0), new Vector2(-540, 90), new Vector2(500, 50));

        HUD hud = canvasGo.AddComponent<HUD>();
        Set(hud, "playerHealth", health);
        Set(hud, "inventory", inventory);
        Set(hud, "shipRepair", repair);
        Set(hud, "weapon", weapon);
        Set(hud, "interactor", interactor);
        Set(hud, "healthBar", healthBar);
        Set(hud, "stamina", stamina);
        Set(hud, "oxygen", oxygen);
        Set(hud, "staminaBar", staminaBar);
        Set(hud, "oxygenBar", oxygenBar);
        Set(hud, "oxygenText", oxygenText);
        Set(hud, "resourcesText", resources);
        Set(hud, "repairBar", repairBar);
        Set(hud, "repairText", repairText);
        Set(hud, "weaponText", weaponText);
        Set(hud, "promptText", prompt);
        Set(hud, "notificationText", notification);

        GameScreens screens = canvasGo.AddComponent<GameScreens>();
        GameObject tutorial = Panel(c, "TutorialPanel", "CÓMO JUGAR", TutorialText);
        RectTransform tutBody = (RectTransform)tutorial.transform.Find("Body");
        tutBody.anchoredPosition = new Vector2(-700, 190); tutBody.sizeDelta = new Vector2(1400, 240);
        tutBody.GetComponent<TMP_Text>().fontSize = 28;
        Button(tutorial.transform, "¡A jugar!", 0, screens.OnTutorialContinueButton);
        Set(screens, "tutorialPanel", tutorial);

        GameObject pause = Panel(c, "PausePanel", "PAUSA", "");
        GameObject victory = Panel(c, "VictoryPanel", "¡DESPEGASTE!", "Reparaste la nave y escapaste del planeta.");
        GameObject defeat = Panel(c, "DefeatPanel", "TE ELIMINARON", "No sobreviviste al planeta.");

        Button(pause.transform, "Reanudar", 0, screens.OnResumeButton);
        Button(pause.transform, "Opciones", 1, screens.OnOptionsButton);
        Button(pause.transform, "Reiniciar", 2, screens.OnRestartButton);
        Button(pause.transform, "Menú principal", 3, screens.OnMainMenuButton);
        Button(victory.transform, "Reiniciar", 0, screens.OnRestartButton);
        Button(victory.transform, "Menú principal", 1, screens.OnMainMenuButton);
        Button(defeat.transform, "Reiniciar", 0, screens.OnRestartButton);
        Button(defeat.transform, "Menú principal", 1, screens.OnMainMenuButton);

        Set(screens, "pausePanel", pause);
        Set(screens, "victoryPanel", victory);
        Set(screens, "defeatPanel", defeat);
        Set(screens, "defeatTitle", defeat.transform.Find("Title").GetComponent<TMP_Text>());
        Set(screens, "defeatMessage", defeat.transform.Find("Body").GetComponent<TMP_Text>());

        PlayerController pc = Object.FindFirstObjectByType<PlayerController>();
        GameObject options = OptionsPanel(canvasGo, pc, screens.OnBackButton);
        Set(screens, "optionsPanel", options);
    }

    private static GameObject OptionsPanel(GameObject canvasGo, PlayerController pc, UnityEngine.Events.UnityAction back)
    {
        GameObject options = Panel(canvasGo.transform, "OptionsPanel", "OPCIONES", "");
        Slider sens = OptionSlider(options.transform, "Sensibilidad del mouse", 0, 0.02f, 0.5f, 0.1f, out TMP_Text sensValue);
        Slider vol = OptionSlider(options.transform, "Volumen", 1, 0f, 1f, 1f, out TMP_Text volValue);
        Button(options.transform, "Volver", 2, back);
        OptionsMenu optionsMenu = canvasGo.AddComponent<OptionsMenu>();
        Set(optionsMenu, "playerController", pc);
        Set(optionsMenu, "sensitivitySlider", sens);
        Set(optionsMenu, "volumeSlider", vol);
        Set(optionsMenu, "sensitivityValueText", sensValue);
        Set(optionsMenu, "volumeValueText", volValue);
        return options;
    }

    private static void BuildMainMenuScene(string gameSceneName)
    {
        var menuScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        Camera cam = Object.FindFirstObjectByType<Camera>();
        if (cam != null) cam.clearFlags = CameraClearFlags.Skybox;
        RenderSettings.skybox = SpaceSkyMaterial();

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        GameObject canvasGo = NewCanvas("Menu Canvas");
        Transform c = canvasGo.transform;

        MainMenu menu = canvasGo.AddComponent<MainMenu>();
        SetString(menu, "gameScene", gameSceneName);

        GameObject main = Panel(c, "MainPanel", "LOST SIGNAL",
            "Sobreviví en un planeta hostil y repará tu nave para escapar.");
        main.SetActive(true);
        Button(main.transform, "Jugar", 0, menu.OnPlayButton);
        Button(main.transform, "Opciones", 1, menu.OnOptionsButton);
        Button(main.transform, "Salir", 2, menu.OnQuitButton);

        AudioSource menuMusic = canvasGo.AddComponent<AudioSource>();
        menuMusic.clip = Clip("music_ambience");
        menuMusic.loop = true;
        menuMusic.playOnAwake = true;
        menuMusic.volume = 0.35f;

        GameObject options = OptionsPanel(canvasGo, null, menu.OnBackButton);
        Set(menu, "mainPanel", main);
        Set(menu, "optionsPanel", options);

        EditorSceneManager.SaveScene(menuScene, MenuScenePath);
    }

    private static GameObject NewCanvas(string name)
    {
        GameObject canvasGo = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        return canvasGo;
    }

    private static void BuildCreatureHealthBar(GameObject creature, Health health, float height)
    {
        GameObject bar = new GameObject("HealthBar", typeof(RectTransform), typeof(Canvas));
        bar.transform.SetParent(creature.transform, false);
        bar.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        RectTransform rt = bar.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(120f, 14f);
        rt.localPosition = new Vector3(0f, height, 0f);
        rt.localScale = Vector3.one * 0.01f;

        GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(bar.transform, false);
        Stretch(bg.GetComponent<RectTransform>());
        bg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(bar.transform, false);
        RectTransform fillRt = fill.GetComponent<RectTransform>();
        Stretch(fillRt);
        fill.GetComponent<Image>().color = new Color(0.9f, 0.2f, 0.2f);

        CreatureHealthBar hb = creature.AddComponent<CreatureHealthBar>();
        Set(hb, "health", health);
        Set(hb, "barRoot", bar);
        Set(hb, "fill", fillRt);
    }

    private static readonly System.Collections.Generic.List<(Vector3 center, float inner, float outer)> FlatZones =
        new System.Collections.Generic.List<(Vector3, float, float)>();

    private static float TerrainHeight(float x, float z)
    {
        float dunes = (Mathf.PerlinNoise(x * 0.011f + 100f, z * 0.011f + 100f) - 0.5f) * 11f;
        float bumps = (Mathf.PerlinNoise(x * 0.045f + 300f, z * 0.045f + 300f) - 0.5f) * 2.6f;
        float ridges = Mathf.Pow(Mathf.PerlinNoise(x * 0.006f + 700f, z * 0.006f + 700f), 3f) * 9f;
        float flat = 1f;
        foreach (var zone in FlatZones)
        {
            float d = Vector3.Distance(new Vector3(x, 0f, z), zone.center);
            flat = Mathf.Min(flat, Mathf.SmoothStep(0f, 1f, (d - zone.inner) / (zone.outer - zone.inner)));
        }
        return GroundTop + (dunes + bumps + ridges) * flat;
    }

    private static Vector3 OnGround(Vector3 p) => new Vector3(p.x, TerrainHeight(p.x, p.z), p.z);

    private static void BuildTerrain(GameObject ground)
    {
        const float size = MapSize + 60f;
        const int res = 185;
        float stepSize = size / (res - 1);
        float start = -size * 0.5f;

        Vector3[] verts = new Vector3[res * res];
        Vector2[] uvs = new Vector2[res * res];
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                float wx = start + x * stepSize, wz = start + z * stepSize;
                verts[z * res + x] = new Vector3(wx, TerrainHeight(wx, wz), wz);
                uvs[z * res + x] = new Vector2((float)x / (res - 1), (float)z / (res - 1));
            }

        int[] tris = new int[(res - 1) * (res - 1) * 6];
        int t = 0;
        for (int z = 0; z < res - 1; z++)
            for (int x = 0; x < res - 1; x++)
            {
                int i = z * res + x;
                tris[t++] = i; tris[t++] = i + res; tris[t++] = i + 1;
                tris[t++] = i + 1; tris[t++] = i + res; tris[t++] = i + res + 1;
            }

        Mesh mesh = new Mesh { name = "MarsTerrain", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.vertices = verts; mesh.uv = uvs; mesh.triangles = tris;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();

        Folder(Root, "Art");
        string meshPath = Root + "/Art/MarsTerrain.asset";
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);

        ground.transform.position = Vector3.zero;
        ground.transform.rotation = Quaternion.identity;
        ground.transform.localScale = Vector3.one;
        foreach (Collider col in ground.GetComponents<Collider>()) Object.DestroyImmediate(col);
        GetOrAdd<MeshFilter>(ground).sharedMesh = mesh;
        GetOrAdd<MeshCollider>(ground).sharedMesh = mesh;
        GetOrAdd<MeshRenderer>(ground).sharedMaterial = GroundMaterial();
    }

    private static Material GroundMaterial()
    {
        string texPath = Root + "/Art/MarsGround.png";
        if (!File.Exists(Path.GetFullPath(texPath)))
        {
            const int n = 1024;
            Texture2D tex = new Texture2D(n, n, TextureFormat.RGB24, false);
            Color light = new Color(0.90f, 0.52f, 0.30f), dark = new Color(0.62f, 0.30f, 0.17f), dust = new Color(0.95f, 0.70f, 0.48f);
            Color[] px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)n * 40f, v = y / (float)n * 40f;
                    float a = Mathf.PerlinNoise(u * 0.5f, v * 0.5f);
                    float b = Mathf.PerlinNoise(u * 3f + 50f, v * 3f + 50f);
                    Color c = Color.Lerp(dark, light, Mathf.SmoothStep(0.2f, 0.8f, a));
                    c = Color.Lerp(c, dust, Mathf.SmoothStep(0.65f, 0.95f, b) * 0.5f);
                    c *= 0.92f + Mathf.PerlinNoise(u * 12f, v * 12f) * 0.16f;
                    c.a = 1f;
                    px[y * n + x] = c;
                }
            tex.SetPixels(px); tex.Apply();
            File.WriteAllBytes(Path.GetFullPath(texPath), tex.EncodeToPNG());
            AssetDatabase.ImportAsset(texPath);
        }
        Material m = Mat("MarsGround", Color.white);
        Texture2D t = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        m.SetTexture("_BaseMap", t); m.mainTexture = t;
        m.SetFloat("_Smoothness", 0.05f);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material TexturedMaterial(string name, string texturePath)
    {
        Material m = Mat(name, Color.white);
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (tex != null) { m.SetTexture("_BaseMap", tex); m.mainTexture = tex; }
        EditorUtility.SetDirty(m);
        return m;
    }

    private static void SetupWalkAnimation(GameObject creatureRoot)
    {
        ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(SkeletonAnims);
        if (importer == null) { Debug.LogWarning("[Setup] Rig_Medium animations not found."); return; }

        ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
        string walkName = null;
        foreach (var c in clips)
            if (c.name.ToLower().Contains("walk") && (walkName == null || c.name.EndsWith("_A"))) walkName = c.name;
        if (walkName == null) { Debug.LogWarning("[Setup] No walk clip found in Rig_Medium."); return; }

        bool changed = false;
        foreach (var c in clips)
            if (c.name == walkName && !c.loopTime) { c.loopTime = true; changed = true; }
        if (changed || importer.clipAnimations.Length == 0)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        AnimationClip walk = AssetDatabase.LoadAllAssetsAtPath(SkeletonAnims).OfType<AnimationClip>()
            .FirstOrDefault(c => c.name == walkName);
        if (walk == null) { Debug.LogWarning("[Setup] Could not load clip " + walkName); return; }

        Folder(Root, "Art");
        string ctrlPath = Root + "/Art/SkeletonWalk.controller";
        AssetDatabase.DeleteAsset(ctrlPath);
        var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPathWithClip(ctrlPath, walk);

        Animator animator = creatureRoot.GetComponentInChildren<Animator>();
        if (animator == null) animator = creatureRoot.transform.GetChild(0).gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
    }

    private static void BuildWreck(ResourceType iron, ResourceType crystal, ResourceType fuel)
    {
        GameObject old = GameObject.Find("WreckedShip"); if (old != null) Object.DestroyImmediate(old);
        GameObject wreck = new GameObject("WreckedShip");
        wreck.transform.position = OnGround(WreckPosition);
        wreck.transform.rotation = Quaternion.Euler(0f, 35f, 0f);

        Transform body = new GameObject("BrokenHull").transform;
        body.SetParent(wreck.transform, false);
        body.localRotation = Quaternion.Euler(18f, 0f, 24f);
        body.localPosition = new Vector3(0f, -0.8f, 0f);
        AddModel(body, "lander_B", 8f);
        FitBoxCollider(wreck, LocalBounds(wreck.transform, body.gameObject));

        Transform debris = new GameObject("Debris").transform;
        debris.SetParent(wreck.transform, false);
        Vector3 c = Flat(wreck.transform.position);
        Prop(debris, "cargo_B", 1.6f, c + new Vector3(6f, 0f, 2f), true).transform.rotation = Quaternion.Euler(0f, 20f, 70f);
        Prop(debris, "containers_C", 1.5f, c + new Vector3(-5f, 0f, 4f), true);
        Prop(debris, "solarpanel", 2.2f, c + new Vector3(3f, 0f, -6f), false).transform.rotation = Quaternion.Euler(35f, 60f, 0f);
        Prop(debris, "cargo_A", 1.2f, c + new Vector3(-6f, 0f, -3f), true);

        SalvageWreck salvage = wreck.AddComponent<SalvageWreck>();
        SerializedObject so = new SerializedObject(salvage);
        SerializedProperty list = so.FindProperty("rewards");
        list.ClearArray();
        (ResourceType type, int amount)[] rewards = { (iron, 12), (fuel, 8), (crystal, 3) };
        for (int i = 0; i < rewards.Length; i++)
        {
            list.InsertArrayElementAtIndex(i);
            SerializedProperty el = list.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("resourceType").objectReferenceValue = rewards[i].type;
            el.FindPropertyRelative("amount").intValue = rewards[i].amount;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Bounds AddModel(Transform parent, string model, float size, Material overrideMat = null) =>
        AddModelAtPath(parent, KayKitFolder + model + ".fbx", size, overrideMat);

    private static Bounds AddModelAtPath(Transform parent, string path, float size, Material overrideMat = null)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (source == null)
        {
            Debug.LogWarning("[Setup] Model not found: " + path);
            return new Bounds(Vector3.up * 0.5f, Vector3.one);
        }

        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(source);
        inst.name = source.name;
        inst.transform.SetParent(parent, false);
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;

        Material mat = overrideMat != null ? overrideMat : KayKitMaterial();
        foreach (Renderer r in inst.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
        }
        foreach (Collider col in inst.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);

        Bounds local = LocalBounds(parent, inst);
        float biggest = Mathf.Max(local.size.x, local.size.y, local.size.z);
        if (biggest > 0.0001f) inst.transform.localScale *= size / biggest;

        local = LocalBounds(parent, inst);
        inst.transform.localPosition += new Vector3(-local.center.x, -local.min.y, -local.center.z);
        return LocalBounds(parent, inst);
    }

    private static Bounds LocalBounds(Transform parent, GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        bool init = false;
        Bounds b = new Bounds(Vector3.zero, Vector3.zero);
        foreach (Renderer r in renderers)
        {
            if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
            {
                Mesh baked = new Mesh();
                smr.BakeMesh(baked, false);
                Vector3 pos = smr.transform.position;
                Quaternion rot = smr.transform.rotation;
                foreach (Vector3 v in baked.vertices)
                {
                    Vector3 lp = parent.InverseTransformPoint(pos + rot * v);
                    if (!init) { b = new Bounds(lp, Vector3.zero); init = true; }
                    else b.Encapsulate(lp);
                }
                Object.DestroyImmediate(baked);
                continue;
            }

            Bounds wb = r.bounds;
            Vector3 c = wb.center, e = wb.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c + Vector3.Scale(e, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 lp = parent.InverseTransformPoint(corner);
                if (!init) { b = new Bounds(lp, Vector3.zero); init = true; }
                else b.Encapsulate(lp);
            }
        }
        return b;
    }

    private static void FitBoxCollider(GameObject go, Bounds localBounds)
    {
        BoxCollider box = GetOrAdd<BoxCollider>(go);
        box.center = localBounds.center;
        box.size = localBounds.size;
    }

    private static Material DomeMaterial()
    {
        Material m = Mat("OxygenDome", new Color(0.35f, 0.75f, 1f, 0.12f));
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_Cull", 0f);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHATEST_ON");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        m.SetFloat("_Smoothness", 0.9f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", new Color(0.05f, 0.15f, 0.3f));
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material KayKitMaterial()
    {
        Material m = Mat("KayKit_SpaceBits", Color.white);
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(KayKitTexture);
        if (tex != null) { m.SetTexture("_BaseMap", tex); m.mainTexture = tex; }
        EditorUtility.SetDirty(m);
        return m;
    }

    private static void BuildShipArt(GameObject ship, GameObject player)
    {
        foreach (Collider col in ship.GetComponents<Collider>()) Object.DestroyImmediate(col);
        MeshRenderer mr = ship.GetComponent<MeshRenderer>(); if (mr != null) Object.DestroyImmediate(mr);
        MeshFilter mf = ship.GetComponent<MeshFilter>(); if (mf != null) Object.DestroyImmediate(mf);
        for (int i = ship.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(ship.transform.GetChild(i).gameObject);

        ship.transform.localScale = Vector3.one;
        ship.transform.position = new Vector3(ship.transform.position.x, GroundTop, ship.transform.position.z);
        Vector3 toPlayer = Flat(player.transform.position) - Flat(ship.transform.position);
        ship.transform.rotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, toPlayer.normalized));

        Transform crash = new GameObject("CrashedModel").transform;
        crash.SetParent(ship.transform, false);
        crash.localRotation = Quaternion.Euler(6f, 0f, -9f);
        crash.localPosition = new Vector3(0f, -0.4f, 0f);
        AddModel(crash, "dropship", 9f);

        FitBoxCollider(ship, LocalBounds(ship.transform, crash.gameObject));
        ship.GetComponent<BoxCollider>().size *= 0.6f;

        GameObject oldCamp = GameObject.Find("BaseCamp"); if (oldCamp != null) Object.DestroyImmediate(oldCamp);
        Transform camp = new GameObject("BaseCamp").transform;
        Vector3 away = -toPlayer.normalized;
        Vector3 side = Vector3.Cross(Vector3.up, away);
        Vector3 c = Flat(ship.transform.position);
        Prop(camp, "basemodule_A", 4.5f, c + away * 8f + side * 4f, true);
        Prop(camp, "solarpanel", 2.5f, c + away * 7f - side * 5f, true);
        Prop(camp, "solarpanel", 2.5f, c + away * 9.5f - side * 3f, true);
        Prop(camp, "cargo_A_stacked", 2f, c + side * 7f, true);
        Prop(camp, "containers_B", 1.6f, c + side * 8.5f + away * 2f, true);

        GameObject lamp = Prop(camp, "lights", 5f, c - side * 7f, false);
        GameObject lamp2 = Prop(camp, "lights", 5f, c + away * 4f + side * 9f, false);
        foreach (GameObject l in new[] { lamp, lamp2 })
        {
            GameObject glow = new GameObject("BlueLight");
            glow.transform.SetParent(l.transform, false);
            glow.transform.localPosition = new Vector3(0f, 5.2f, 0f);
            Light pl = glow.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = new Color(0.3f, 0.6f, 1f);
            pl.range = 14f;
            pl.intensity = 3f;
        }

        GameObject dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dome.name = "OxygenDome";
        Object.DestroyImmediate(dome.GetComponent<Collider>());
        dome.transform.SetParent(camp);
        dome.transform.position = new Vector3(c.x, GroundTop, c.z);
        dome.transform.localScale = Vector3.one * (OxygenSafeRadius * 2f);
        MeshRenderer domeRenderer = dome.GetComponent<MeshRenderer>();
        domeRenderer.sharedMaterial = DomeMaterial();
        domeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static GameObject Prop(Transform parent, string model, float size, Vector3 flatPos, bool solid)
    {
        GameObject go = new GameObject(model);
        go.transform.SetParent(parent);
        go.transform.position = OnGround(flatPos);
        go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        Bounds b = AddModel(go.transform, model, size);
        if (solid) FitBoxCollider(go, b);
        return go;
    }

    private static void BuildDecor(GameObject ship, GameObject player)
    {
        GameObject old = GameObject.Find("Decor"); if (old != null) Object.DestroyImmediate(old);
        Transform decor = new GameObject("Decor").transform;
        Random.InitState(7);
        float half = MapSize * 0.5f;

        string[] rocks = { "rock_A", "rock_B", "rocks_A", "rocks_B" };
        int placed = 0, tries = 0;
        while (placed < 90 && tries++ < 1500)
        {
            Vector3 p = new Vector3(Random.Range(-half + 12f, half - 12f), 0f, Random.Range(-half + 12f, half - 12f));
            if (Vector3.Distance(p, Flat(ship.transform.position)) < 22f) continue;
            if (Vector3.Distance(p, Flat(player.transform.position)) < 10f) continue;
            if (Vector3.Distance(p, Flat(WreckPosition)) < 16f) continue;
            Prop(decor, rocks[Random.Range(0, rocks.Length)], Random.Range(3.5f, 8f), p, true);
            placed++;
        }

        string[] hills = { "terrain_tall", "terrain_tall_curved", "terrain_low_curved" };
        const float step = 18f;
        for (float t = -half; t <= half; t += step)
        {
            Hill(decor, hills, new Vector3(t, 0f, half + 4f));
            Hill(decor, hills, new Vector3(t, 0f, -half - 4f));
            Hill(decor, hills, new Vector3(half + 4f, 0f, t));
            Hill(decor, hills, new Vector3(-half - 4f, 0f, t));
        }
    }

    private static void Hill(Transform parent, string[] options, Vector3 flatPos)
    {
        GameObject go = Prop(parent, options[Random.Range(0, options.Length)], Random.Range(18f, 26f), flatPos, false);
        go.transform.position += Vector3.down * Random.Range(0f, 2f);
    }

    private static void BuildSkyAndAtmosphere()
    {
        RenderSettings.skybox = SpaceSkyMaterial();
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1.1f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.55f, 0.32f, 0.22f);
        RenderSettings.fogDensity = 0.0045f;

        Light sun = Object.FindFirstObjectByType<Light>();
        if (sun != null && sun.type == LightType.Directional)
        {
            sun.color = new Color(1f, 0.9f, 0.8f);
            sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
        }
        DynamicGI.UpdateEnvironment();
    }

    private static Material SpaceSkyMaterial()
    {
        string folder = Root + "/Art";
        Folder(Root, "Art");
        string texPath = folder + "/SpaceSky.png";
        string matPath = folder + "/SpaceSky.mat";

        if (!File.Exists(Path.GetFullPath(texPath)))
        {
            File.WriteAllBytes(Path.GetFullPath(texPath), GenerateStarfield(4096, 2048).EncodeToPNG());
            AssetDatabase.ImportAsset(texPath);
            TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
            ti.wrapModeU = TextureWrapMode.Repeat;
            ti.wrapModeV = TextureWrapMode.Clamp;
            ti.mipmapEnabled = false;
            ti.maxTextureSize = 4096;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Skybox/Panoramic"));
            AssetDatabase.CreateAsset(mat, matPath);
        }
        mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
        mat.SetFloat("_Exposure", 1.2f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Texture2D GenerateStarfield(int w, int h)
    {
        var rng = new System.Random(1234);
        Color[] px = new Color[w * h];
        Color deep = new Color(0.015f, 0.012f, 0.035f);
        Color nebulaA = new Color(0.45f, 0.12f, 0.35f);
        Color nebulaB = new Color(0.10f, 0.22f, 0.55f);

        for (int y = 0; y < h; y++)
        {
            float lat = (float)y / h;
            float horizonGlow = Mathf.Clamp01(1f - Mathf.Abs(lat - 0.5f) * 6f) * 0.35f;
            for (int x = 0; x < w; x++)
            {
                float u = (float)x / w;

                float nx = Mathf.Cos(u * Mathf.PI * 2f) * 2f, nz = Mathf.Sin(u * Mathf.PI * 2f) * 2f;
                float n1 = Mathf.PerlinNoise(nx + 10f, lat * 4f + nz);
                float n2 = Mathf.PerlinNoise(nz * 1.7f + 50f, lat * 6f + nx * 1.3f);
                float a = Mathf.SmoothStep(0.55f, 0.85f, n1) * 0.6f;
                float b = Mathf.SmoothStep(0.6f, 0.9f, n2) * 0.5f;
                Color c = deep + nebulaA * a + nebulaB * b;
                c += new Color(0.9f, 0.45f, 0.25f) * horizonGlow * 0.4f;
                c.a = 1f;
                px[y * w + x] = c;
            }
        }

        int stars = (w * h) / 220;
        for (int i = 0; i < stars; i++)
        {
            int x = rng.Next(w), y = rng.Next(h);
            float bright = (float)rng.NextDouble();
            bright = bright * bright * bright;
            Color tint = Color.Lerp(new Color(0.7f, 0.8f, 1f), new Color(1f, 0.9f, 0.75f), (float)rng.NextDouble());
            Color star = tint * (0.35f + bright * 1.2f);
            int idx = y * w + x;
            px[idx] = Color.Lerp(px[idx], star, 0.9f);
            if (bright > 0.6f)
            {
                foreach (var d in new[] { 1, -1, w, -w })
                {
                    int j = idx + d;
                    if (j >= 0 && j < px.Length) px[j] = Color.Lerp(px[j], star, 0.45f);
                }
            }
        }

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static void Wall(Transform parent, Vector3 position, Vector3 size)
    {
        GameObject wall = new GameObject("Wall", typeof(BoxCollider));
        wall.transform.SetParent(parent);
        wall.transform.position = position;
        wall.GetComponent<BoxCollider>().size = size;
    }

    private static Slider OptionSlider(Transform parent, string label, int row, float min, float max, float value, out TMP_Text valueText)
    {
        float y = 120 - row * 110;
        Text(parent, label + "Label", label, 32, TextAlignmentOptions.Left, new Vector2(0.5f, 0.5f), new Vector2(-450, y), new Vector2(450, 50));
        GameObject go = DefaultControls.CreateSlider(new DefaultControls.Resources());
        go.name = label + "Slider";
        go.transform.SetParent(parent, false);
        Slider s = go.GetComponent<Slider>();
        s.minValue = min; s.maxValue = max; s.value = value;
        go.transform.Find("Background").GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.25f);
        go.transform.Find("Fill Area/Fill").GetComponent<Image>().color = new Color(0.3f, 0.8f, 1f);
        Place(go.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0, y - 15), new Vector2(350, 24));
        valueText = Text(parent, label + "Value", "", 32, TextAlignmentOptions.Left, new Vector2(0.5f, 0.5f), new Vector2(370, y), new Vector2(150, 50));
        return s;
    }

    private static TMP_Text Text(Transform parent, string name, string text, float size, TextAlignmentOptions align, Vector2 anchor, Vector2 pos, Vector2 rectSize)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.alignment = align; t.color = Color.white; t.raycastTarget = false;
        Place(go.GetComponent<RectTransform>(), anchor, pos, rectSize);
        return t;
    }

    private static Slider Bar(Transform parent, string name, Color fill, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        GameObject go = DefaultControls.CreateSlider(new DefaultControls.Resources());
        go.name = name;
        go.transform.SetParent(parent, false);
        Slider s = go.GetComponent<Slider>();
        Transform handle = go.transform.Find("Handle Slide Area");
        if (handle != null) Object.DestroyImmediate(handle.gameObject);
        s.interactable = false; s.transition = Selectable.Transition.None;
        s.minValue = 0f; s.maxValue = 1f; s.value = 1f;
        go.transform.Find("Background").GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        go.transform.Find("Fill Area/Fill").GetComponent<Image>().color = fill;
        RectTransform fillArea = (RectTransform)go.transform.Find("Fill Area");
        fillArea.offsetMin = Vector2.zero; fillArea.offsetMax = Vector2.zero;
        ((RectTransform)go.transform.Find("Fill Area/Fill")).sizeDelta = Vector2.zero;
        Place(go.GetComponent<RectTransform>(), anchor, pos, size);
        return s;
    }

    private static GameObject Panel(Transform parent, string name, string title, string body)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.06f, 0.85f);
        Text(go.transform, "Title", title, 72, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(-600, 300), new Vector2(1200, 100));
        if (!string.IsNullOrEmpty(body))
            Text(go.transform, "Body", body, 30, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(-700, 180), new Vector2(1400, 200));
        go.SetActive(false);
        return go;
    }

    private static void Button(Transform parent, string label, int index, UnityEngine.Events.UnityAction action)
    {
        GameObject go = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
        go.name = label + "Button";
        go.transform.SetParent(parent, false);
        go.GetComponentInChildren<TextMeshProUGUI>().text = label;
        go.GetComponentInChildren<TextMeshProUGUI>().fontSize = 32;
        Place(go.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(-150, -60 - index * 90), new Vector2(300, 70));
        UnityEventTools.AddPersistentListener(go.GetComponent<UnityEngine.UI.Button>().onClick, action);
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 topLeftPos, Vector2 size)
    {
        rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = topLeftPos; rt.sizeDelta = size;
    }

    private static InputActionReference Ref(InputActionReference[] refs, string action)
    {
        InputActionReference r = refs.FirstOrDefault(x => x.name == "Player/" + action)
            ?? refs.FirstOrDefault(x => x.action != null && x.action.name == action && x.action.actionMap != null && x.action.actionMap.name == "Player");
        if (r == null) Debug.LogError("[Setup] Action Player/" + action + " not found in InputSystem_Actions.");
        return r;
    }

    private static void Set(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[Setup] Field '{field}' does not exist on {target.GetType().Name}"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetInt(Object target, string field, int value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[Setup] Field '{field}' does not exist on {target.GetType().Name}"); return; }
        p.intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Sprite UISprite(string name)
    {
        string path = Root + "/Art/UI/" + name + ".png";
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) { Debug.LogWarning("[Setup] Missing image: " + name); return null; }
        if (ti.textureType != TextureImporterType.Sprite)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Image FullScreenImage(Transform parent, string name, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>());
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        img.enabled = false;
        return img;
    }

    private static void BuildNaturalNest(Transform nest)
    {
        Material rock = Mat("NestRock", new Color(0.22f, 0.14f, 0.16f));
        Material glow = Mat("NestCrystal", new Color(0.65f, 0.15f, 0.9f), true);

        Transform main = new GameObject("NestRocks").transform;
        main.SetParent(nest, false);
        AddModel(main, "rocks_B", 4.5f, rock);

        for (int i = 0; i < 3; i++)
        {
            Transform shard = new GameObject("NestShard").transform;
            shard.SetParent(nest, false);
            float a = i * 120f + 30f;
            shard.localPosition = Quaternion.Euler(0f, a, 0f) * Vector3.forward * 2.6f;
            shard.localRotation = Quaternion.Euler(Random.Range(-15f, 15f), a, Random.Range(-20f, 20f));
            AddModel(shard, "rock_B", Random.Range(1.2f, 1.8f), glow);
        }
    }

    private static AudioClip Clip(string name)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/" + name + ".wav");
        if (clip == null) Debug.LogWarning("[Setup] Missing sound: " + name);
        return clip;
    }

    private static void SetFloat(Object target, string field, float value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[Setup] Field '{field}' does not exist on {target.GetType().Name}"); return; }
        p.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetString(Object target, string field, string value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[Setup] Field '{field}' does not exist on {target.GetType().Name}"); return; }
        p.stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
        return asset;
    }

    private static ResourceType ResourceAsset(string file, string display)
    {
        ResourceType t = LoadOrCreate<ResourceType>($"{SoFolder}/ResourceTypes/{file}.asset");
        t.displayName = display;
        EditorUtility.SetDirty(t);
        return t;
    }

    private static GameObject NodePrefab(string name, string model, float size, Material overrideMat, ResourceType type)
    {
        GameObject go = new GameObject(name);
        Bounds b = AddModel(go.transform, model, size, overrideMat);
        FitBoxCollider(go, b);
        ResourceNode node = go.AddComponent<ResourceNode>();
        Set(node, "resourceType", type);
        SetInt(node, "amountPerHarvest", 1);
        SetInt(node, "harvestsLeft", 1);
        return SavePrefab(go, $"{PrefabFolder}/Nodes/{name}.prefab");
    }

    private static GameObject SavePrefab(GameObject go, string path)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    private static Material Mat(string name, Color color, bool emissive = false)
    {
        string path = $"{MatFolder}/{name}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        m.color = color;
        if (emissive) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * 2f); }
        EditorUtility.SetDirty(m);
        return m;
    }

    private static void Paint(GameObject go, Material m)
    {
        Renderer r = go.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = m;
    }

    private static void Folder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
#endif
