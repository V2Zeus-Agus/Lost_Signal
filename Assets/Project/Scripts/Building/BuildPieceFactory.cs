using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Factory: arma cada pieza construible con primitivas (sin prefabs), tanto la real como el
/// holograma de vista previa. Así el estilo es uniforme y se pueden sumar piezas desde datos.
/// </summary>
public static class BuildPieceFactory
{
    /// <summary>Caja que ocupa la pieza (espacio local), para chequear si entra.</summary>
    public static Bounds Footprint(BuildableKind kind)
    {
        switch (kind)
        {
            case BuildableKind.Habitat: return new Bounds(new Vector3(0f, 1.9f, 0f), new Vector3(5.7f, 2.9f, 5.7f));
            case BuildableKind.OxygenGenerator: return new Bounds(new Vector3(0f, 0.95f, 0f), new Vector3(1.5f, 1.9f, 1.5f));
            case BuildableKind.Storage: return new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(1.4f, 0.9f, 0.85f));
            case BuildableKind.Factory: return new Bounds(new Vector3(0f, 1.4f, 0f), new Vector3(3.2f, 2.6f, 3.2f));
            case BuildableKind.UpgradeShop: return new Bounds(new Vector3(0f, 0.7f, 0f), new Vector3(1.6f, 1.4f, 1.6f));
            case BuildableKind.Door: return new Bounds(new Vector3(0f, 1.5f, 0f), new Vector3(2f, 3f, 0.4f));
            default: return new Bounds(new Vector3(0f, 1.1f, 0f), new Vector3(0.7f, 2.2f, 0.7f));
        }
    }

    public static GameObject Create(BuildableDefinition definition, BuildCatalog catalog, bool hologram)
    {
        var root = new GameObject(hologram ? "Hologram_" + definition.displayName : definition.displayName);
        var b = new Builder(root.transform, catalog, hologram);

        bool useModel = definition.model != null && definition.kind != BuildableKind.Habitat;
        switch (definition.kind)
        {
            case BuildableKind.Habitat: BuildHabitat(b); break;
            case BuildableKind.OxygenGenerator: if (useModel) BuildGeneratorModel(b, definition); else BuildGenerator(b); break;
            case BuildableKind.Storage: if (useModel) BuildStorageModel(b, definition); else BuildStorage(b); break;
            case BuildableKind.Factory: if (useModel) BuildFactoryModel(b, definition); else BuildFactory(b, definition); break;
            case BuildableKind.UpgradeShop: BuildUpgradeTerminal(b, definition, useModel); break;
            case BuildableKind.Door: BuildDoorPiece(b); break;
            default: if (useModel) BuildLampModel(b, definition); else BuildLamp(b); break;
        }

        if (!hologram) root.AddComponent<PlacedPiece>().Init(definition);
        return root;
    }

    // ---------- Piezas ----------

    private static void BuildHabitat(Builder b)
    {
        float half = Habitat.Size * 0.5f;
        float wallY = Habitat.FloorTop + Habitat.WallHeight * 0.5f;
        float top = Habitat.FloorTop + Habitat.WallHeight;

        b.Box("Foundation", new Vector3(0f, -1.25f, 0f), new Vector3(Habitat.Size, 2.5f, Habitat.Size), b.Floor, true);
        b.Box("Floor", new Vector3(0f, Habitat.FloorTop * 0.5f, 0f), new Vector3(Habitat.Size, Habitat.FloorTop, Habitat.Size), b.Floor, true);
        b.Box("Roof", new Vector3(0f, top + 0.1f, 0f), new Vector3(Habitat.Size, 0.2f, Habitat.Size), b.Hull, true);
        b.Box("RoofTrim", new Vector3(0f, top + 0.22f, 0f), new Vector3(Habitat.Size - 0.4f, 0.06f, Habitat.Size - 0.4f), b.Trim, false);

        // Postes en las esquinas (sin collider: no estorban al unir módulos).
        for (int i = 0; i < 4; i++)
        {
            float sx = i % 2 == 0 ? 1f : -1f, sz = i < 2 ? 1f : -1f;
            b.Box("Post", new Vector3(sx * (half - 0.12f), wallY, sz * (half - 0.12f)), new Vector3(0.24f, Habitat.WallHeight, 0.24f), b.Trim, false);
        }

        var sides = new GameObject[4];
        var doorSides = new GameObject[4];
        for (int side = 0; side < 4; side++)
        {
            Quaternion facing = Quaternion.LookRotation(Habitat.SideDirections[side]);
            Transform sideRoot = b.Group("Side" + side);
            sideRoot.localRotation = facing;
            var sb = new Builder(sideRoot, b.Catalog, b.Hologram);

            // Todas las paredes nacen cerradas: para entrar hay que construir una Puerta (requisito).
            {
                float z = half - 0.1f;
                sb.Box("Wall", new Vector3(0f, wallY, z), new Vector3(Habitat.Size, Habitat.WallHeight, 0.2f), b.Hull, true);
                sb.Box("Window", new Vector3(0f, Habitat.FloorTop + 2f, z), new Vector3(3f, 0.6f, 0.26f), b.Glass, false);

                // Versión con puerta: se activa si el jugador construye una "Puerta" en esta pared.
                if (!b.Hologram)
                {
                    Transform doorRoot = b.Group("Side" + side + "_Door");
                    doorRoot.localRotation = facing;
                    BuildDoorWall(new Builder(doorRoot, b.Catalog, b.Hologram), b);
                    doorRoot.gameObject.SetActive(false);
                    doorSides[side] = doorRoot.gameObject;
                }
            }
            sides[side] = sideRoot.gameObject;
        }

        if (b.Catalog.habitatRoofDecor != null)
            b.Model(b.Catalog.habitatRoofDecor, new Bounds(new Vector3(0f, top + 0.2f + 0.6f, 0f), new Vector3(2.6f, 1.2f, 2.6f)), 1f, 0f);

        if (b.Hologram) return;

        Light light = b.PointLight(new Vector3(0f, top - 0.4f, 0f), 9f);
        Habitat habitat = b.Root.gameObject.AddComponent<Habitat>();
        habitat.Init(sides, doorSides, light);

        NavMeshObstacle obstacle = b.Root.gameObject.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = new Vector3(0f, 1.75f, 0f);
        obstacle.size = new Vector3(Habitat.Size, 3.5f, Habitat.Size);
        obstacle.carving = true;
    }

    /// <summary>Pared con hueco de puerta (1,6 x 2,4 m) y rampa afuera, en el espacio local del lado.</summary>
    private static void BuildDoorWall(Builder sb, Builder b)
    {
        float half = Habitat.Size * 0.5f;
        float wallY = Habitat.FloorTop + Habitat.WallHeight * 0.5f;
        float top = Habitat.FloorTop + Habitat.WallHeight;
        float z = half - 0.1f;
        float doorHalf = 0.8f, doorTop = Habitat.FloorTop + 2.4f;
        float sideWidth = half - doorHalf;
        sb.Box("WallL", new Vector3(-(doorHalf + sideWidth * 0.5f), wallY, z), new Vector3(sideWidth, Habitat.WallHeight, 0.2f), b.Hull, true);
        sb.Box("WallR", new Vector3(doorHalf + sideWidth * 0.5f, wallY, z), new Vector3(sideWidth, Habitat.WallHeight, 0.2f), b.Hull, true);
        sb.Box("Lintel", new Vector3(0f, (doorTop + top) * 0.5f, z), new Vector3(doorHalf * 2f, top - doorTop, 0.2f), b.Trim, true);
        sb.Box("DoorFrameL", new Vector3(-doorHalf - 0.05f, Habitat.FloorTop + 1.2f, z), new Vector3(0.1f, 2.4f, 0.26f), b.Trim, false);
        sb.Box("DoorFrameR", new Vector3(doorHalf + 0.05f, Habitat.FloorTop + 1.2f, z), new Vector3(0.1f, 2.4f, 0.26f), b.Trim, false);
        // Rampa: el piso queda un poco elevado y así se puede entrar caminando.
        GameObject ramp = sb.Box("Ramp", new Vector3(0f, Habitat.FloorTop - 0.45f, half + 0.62f), new Vector3(1.8f, 0.2f, 1.6f), b.Floor, true);
        ramp.transform.localRotation = Quaternion.Euler(28f, 0f, 0f);
    }

    /// <summary>
    /// Puerta: un marco con luz que se encaja en una pared de un hábitat. La pared en sí la
    /// cambia el hábitat (BaseBuildingManager.Recompute); esta pieza solo marca dónde va.
    /// </summary>
    private static void BuildDoorPiece(Builder b)
    {
        float frameZ = 0.18f; // apenas afuera de la pared
        float doorTop = Habitat.FloorTop + 2.4f;
        b.Box("FrameL", new Vector3(-0.9f, Habitat.FloorTop + 1.25f, frameZ), new Vector3(0.14f, 2.5f, 0.14f), b.Trim, false);
        b.Box("FrameR", new Vector3(0.9f, Habitat.FloorTop + 1.25f, frameZ), new Vector3(0.14f, 2.5f, 0.14f), b.Trim, false);
        b.Box("FrameTop", new Vector3(0f, doorTop + 0.1f, frameZ), new Vector3(1.94f, 0.2f, 0.14f), b.Trim, false);
        b.Box("Light", new Vector3(0f, doorTop + 0.1f, frameZ + 0.08f), new Vector3(1.2f, 0.06f, 0.02f), b.Glow, false);
        if (b.Hologram)
        {
            // En la vista previa se ve el hueco que va a quedar.
            b.Box("Opening", new Vector3(0f, Habitat.FloorTop + 1.2f, 0f), new Vector3(1.6f, 2.4f, 0.05f), b.Glow, false);
            return;
        }
        // Collider solo a la altura del dintel (no tapa la entrada) y atravesando la pared, así se
        // le puede apuntar para desarmar tanto desde afuera como desde adentro.
        BoxCollider box = b.Root.gameObject.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, doorTop + 0.1f, -0.05f);
        box.size = new Vector3(2f, 0.3f, 0.6f);
    }

    private static void BuildGenerator(Builder b)
    {
        b.Box("Base", new Vector3(0f, 0.1f, 0f), new Vector3(1.2f, 0.2f, 1.2f), b.Floor, false);
        b.Shape(PrimitiveType.Cylinder, "Tank", new Vector3(0f, 0.9f, 0f), new Vector3(0.9f, 0.7f, 0.9f), b.Hull);
        b.Shape(PrimitiveType.Cylinder, "Ring", new Vector3(0f, 1.1f, 0f), new Vector3(0.95f, 0.06f, 0.95f), b.Glow);
        Transform fan = b.Group("Fan");
        fan.localPosition = new Vector3(0f, 1.66f, 0f);
        var fb = new Builder(fan, b.Catalog, b.Hologram);
        fb.Box("BladeA", Vector3.zero, new Vector3(0.8f, 0.04f, 0.12f), b.Trim, false);
        fb.Box("BladeB", Vector3.zero, new Vector3(0.12f, 0.04f, 0.8f), b.Trim, false);
        b.AddRootCollider(Footprint(BuildableKind.OxygenGenerator));

        if (b.Hologram) return;
        b.Root.gameObject.AddComponent<OxygenGenerator>().Init(fan);
    }

    private static void BuildStorage(Builder b)
    {
        b.Box("Body", new Vector3(0f, 0.4f, 0f), new Vector3(1.4f, 0.8f, 0.85f), b.Hull, false);
        b.Box("Lid", new Vector3(0f, 0.86f, 0f), new Vector3(1.44f, 0.12f, 0.89f), b.Trim, false);
        b.Box("Label", new Vector3(0f, 0.45f, 0.43f), new Vector3(0.5f, 0.2f, 0.02f), b.Glow, false);
        b.AddRootCollider(Footprint(BuildableKind.Storage));

        if (b.Hologram) return;
        b.Root.gameObject.AddComponent<StorageContainer>();
    }

    private static void BuildFactory(Builder b, BuildableDefinition definition)
    {
        Material band = b.Hologram ? b.Catalog.HologramValid : Factory.BandMaterial(definition.producedItem) ?? b.Catalog.Glow;
        b.Box("Base", new Vector3(0f, 0.15f, 0f), new Vector3(3.4f, 0.3f, 3.4f), b.Floor, false);
        b.Box("Body", new Vector3(0f, 1.2f, 0f), new Vector3(2.8f, 1.8f, 2.8f), b.Hull, false);
        b.Box("Band", new Vector3(0f, 1.55f, 0f), new Vector3(2.84f, 0.25f, 2.84f), band, false);
        b.Box("Roof", new Vector3(0f, 2.15f, 0f), new Vector3(2.9f, 0.12f, 2.9f), b.Trim, false);
        b.Shape(PrimitiveType.Cylinder, "Chimney", new Vector3(0.85f, 2.6f, 0.85f), new Vector3(0.5f, 0.5f, 0.5f), b.Hull);
        b.Box("Hopper", new Vector3(0f, 0.75f, 1.55f), new Vector3(1.1f, 0.7f, 0.4f), b.Trim, false);
        b.Box("Output", new Vector3(0f, 0.75f, 1.76f), new Vector3(0.7f, 0.35f, 0.04f), band, false);

        // Taladro que gira arriba (se frena si la fábrica está llena).
        Transform drill = b.Group("Drill");
        drill.localPosition = new Vector3(-0.8f, 2.3f, -0.8f);
        var db = new Builder(drill, b.Catalog, b.Hologram);
        db.Box("BladeA", Vector3.zero, new Vector3(0.9f, 0.08f, 0.16f), band, false);
        db.Box("BladeB", Vector3.zero, new Vector3(0.16f, 0.08f, 0.9f), band, false);
        b.AddRootCollider(Footprint(BuildableKind.Factory));

        if (b.Hologram) return;
        Light light = b.PointLight(new Vector3(1.2f, 2.4f, 1.2f), 5f);
        light.intensity = 2.5f;
        b.Root.gameObject.AddComponent<Factory>().Init(definition, light, drill);
    }

    private static void BuildLamp(Builder b)
    {
        b.Shape(PrimitiveType.Cylinder, "Pole", new Vector3(0f, 0.9f, 0f), new Vector3(0.12f, 0.9f, 0.12f), b.Hull);
        b.Shape(PrimitiveType.Sphere, "Bulb", new Vector3(0f, 1.95f, 0f), Vector3.one * 0.35f, b.Glow);
        b.AddRootCollider(Footprint(BuildableKind.Lamp));
        if (b.Hologram) return;
        b.PointLight(new Vector3(0f, 1.95f, 0f), 12f).intensity = 3f;
    }

    /// <summary>Terminal de mejoras: un pupitre con pantalla que abre la tienda de la nave.</summary>
    private static void BuildUpgradeTerminal(Builder b, BuildableDefinition definition, bool useModel)
    {
        Bounds box = Footprint(BuildableKind.UpgradeShop);
        if (useModel)
        {
            b.Model(definition.model, box, definition.modelScale, definition.modelYaw);
        }
        else
        {
            b.Box("Base", new Vector3(0f, 0.08f, 0f), new Vector3(1.4f, 0.16f, 0.9f), b.Floor, false);
            b.Box("Body", new Vector3(0f, 0.55f, -0.1f), new Vector3(1.2f, 0.8f, 0.6f), b.Hull, false);
            GameObject desk = b.Box("Desk", new Vector3(0f, 1.0f, 0.12f), new Vector3(1.3f, 0.08f, 0.6f), b.Trim, false);
            desk.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            b.Box("ScreenFrame", new Vector3(0f, 1.38f, -0.28f), new Vector3(1.1f, 0.62f, 0.08f), b.Hull, false);
            b.Box("Screen", new Vector3(0f, 1.38f, -0.23f), new Vector3(0.95f, 0.5f, 0.02f), b.Glow, false);
            b.Box("Stripe", new Vector3(0f, 0.3f, 0.21f), new Vector3(1.22f, 0.08f, 0.02f), b.Trim, false);
        }
        b.AddRootCollider(box);
        if (b.Hologram) return;
        b.PointLight(new Vector3(0f, 1.4f, 0.3f), 3f).color = new Color(0.4f, 0.9f, 1f);
        b.Root.gameObject.AddComponent<UpgradeTerminal>();
    }

    // ---------- Piezas con modelo 3D (KayKit) ----------

    private static void BuildGeneratorModel(Builder b, BuildableDefinition definition)
    {
        Bounds box = Footprint(BuildableKind.OxygenGenerator);
        b.Model(definition.model, box, definition.modelScale, definition.modelYaw);
        b.AddRootCollider(box);
        if (b.Hologram) return;
        // Un anillo que brilla y gira: indica que está produciendo aire.
        Transform fan = b.Group("Indicator");
        fan.localPosition = new Vector3(0f, box.max.y + 0.08f, 0f);
        var fb = new Builder(fan, b.Catalog, b.Hologram);
        fb.Box("BladeA", Vector3.zero, new Vector3(0.5f, 0.03f, 0.06f), b.Glow, false);
        fb.Box("BladeB", Vector3.zero, new Vector3(0.06f, 0.03f, 0.5f), b.Glow, false);
        b.PointLight(new Vector3(0f, 1f, 0f), 4f).color = new Color(0.5f, 1f, 0.7f);
        b.Root.gameObject.AddComponent<OxygenGenerator>().Init(fan);
    }

    private static void BuildStorageModel(Builder b, BuildableDefinition definition)
    {
        Bounds box = Footprint(BuildableKind.Storage);
        b.Model(definition.model, box, definition.modelScale, definition.modelYaw);
        b.AddRootCollider(box);
        if (b.Hologram) return;
        b.Root.gameObject.AddComponent<StorageContainer>();
    }

    private static void BuildFactoryModel(Builder b, BuildableDefinition definition)
    {
        Bounds box = Footprint(BuildableKind.Factory);
        b.Model(definition.model, box, definition.modelScale, definition.modelYaw);

        // Franja del color del recurso al frente: se distingue qué produce cada fábrica.
        Material band = b.Hologram ? b.Catalog.HologramValid : Factory.BandMaterial(definition.producedItem) ?? b.Catalog.Glow;
        b.Box("Band", new Vector3(0f, 0.06f, box.extents.z + 0.05f), new Vector3(box.size.x * 0.8f, 0.12f, 0.1f), band, false);
        Transform drill = b.Group("Spinner");
        drill.localPosition = new Vector3(0f, box.max.y + 0.1f, 0f);
        var db = new Builder(drill, b.Catalog, b.Hologram);
        db.Box("BladeA", Vector3.zero, new Vector3(0.7f, 0.05f, 0.1f), band, false);
        db.Box("BladeB", Vector3.zero, new Vector3(0.1f, 0.05f, 0.7f), band, false);
        b.AddRootCollider(box);

        if (b.Hologram) return;
        Light light = b.PointLight(new Vector3(0f, box.max.y, box.extents.z), 5f);
        light.intensity = 2.5f;
        b.Root.gameObject.AddComponent<Factory>().Init(definition, light, drill);
    }

    private static void BuildLampModel(Builder b, BuildableDefinition definition)
    {
        Bounds box = Footprint(BuildableKind.Lamp);
        Bounds fitted = b.Model(definition.model, box, definition.modelScale, definition.modelYaw);
        b.AddRootCollider(box);
        if (b.Hologram) return;
        b.PointLight(new Vector3(0f, fitted.max.y - 0.2f, 0f), 12f).intensity = 3f;
    }

    // ---------- Ayudante para armar primitivas ----------

    private class Builder
    {
        public readonly Transform Root;
        public readonly BuildCatalog Catalog;
        public readonly bool Hologram;

        public Builder(Transform root, BuildCatalog catalog, bool hologram)
        {
            Root = root;
            Catalog = catalog;
            Hologram = hologram;
        }

        // En el holograma todo usa el mismo material (verde/rojo lo cambia BuildHologram).
        private Material M(Material m) => Hologram ? Catalog.HologramValid : m;
        public Material Hull => M(Catalog.Hull);
        public Material Trim => M(Catalog.Trim);
        public Material Glass => M(Catalog.Glass);
        public Material Floor => M(Catalog.Floor);
        public Material Glow => M(Catalog.Glow);

        public Transform Group(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            return go.transform;
        }

        public GameObject Box(string name, Vector3 position, Vector3 size, Material material, bool collider) =>
            Make(PrimitiveType.Cube, name, position, size, material, collider);

        public GameObject Shape(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material) =>
            Make(type, name, position, scale, material, false);

        private GameObject Make(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material, bool collider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!collider || Hologram) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(Root, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            if (Hologram) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>
        /// Instancia un modelo importado y lo escala para que entre en la caja (apoyado en el
        /// piso de la caja y centrado). Sin colliders propios; en el holograma usa el material verde.
        /// Devuelve los bounds finales en espacio local.
        /// </summary>
        public Bounds Model(GameObject prefab, Bounds box, float scaleMultiplier, float yaw)
        {
            GameObject go = Object.Instantiate(prefab, Root, false);
            go.name = "Model_" + prefab.name;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one;
            foreach (Collider col in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);

            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            Bounds local = LocalBounds(renderers);
            if (local.size.sqrMagnitude < 1e-6f) return box;

            float scale = Mathf.Min(box.size.x / Mathf.Max(local.size.x, 0.001f),
                                    box.size.z / Mathf.Max(local.size.z, 0.001f),
                                    box.size.y / Mathf.Max(local.size.y, 0.001f)) * scaleMultiplier;
            go.transform.localScale = Vector3.one * scale;
            Vector3 center = local.center * scale;
            float bottom = local.min.y * scale;
            go.transform.localPosition = new Vector3(box.center.x - center.x, box.min.y - bottom, box.center.z - center.z);

            foreach (Renderer r in renderers)
            {
                if (!Hologram) continue;
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = Catalog.HologramValid;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return new Bounds(new Vector3(box.center.x, box.min.y + local.size.y * scale * 0.5f, box.center.z), local.size * scale);
        }

        /// <summary>Bounds de los renderers medidos en el espacio local de Root.</summary>
        private Bounds LocalBounds(Renderer[] renderers)
        {
            bool has = false;
            var result = new Bounds();
            foreach (Renderer r in renderers)
            {
                Bounds w = r.bounds;
                Vector3 min = w.min, max = w.max;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                    Vector3 p = Root.InverseTransformPoint(corner);
                    if (!has) { result = new Bounds(p, Vector3.zero); has = true; }
                    else result.Encapsulate(p);
                }
            }
            return result;
        }

        public void AddRootCollider(Bounds bounds)
        {
            if (Hologram) return;
            BoxCollider box = Root.gameObject.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;
        }

        public Light PointLight(Vector3 position, float range)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(Root, false);
            go.transform.localPosition = position;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = range;
            light.intensity = 2f;
            light.color = new Color(1f, 0.93f, 0.8f);
            return light;
        }
    }
}
