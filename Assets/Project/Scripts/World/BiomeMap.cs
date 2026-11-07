using UnityEngine;

/// <summary>
/// Mezcla el ambiente según la posición del jugador:
///   centro y costados → bioma base
///   norte lejano      → mesetas áridas con polvo en suspensión
///   sur lejano        → zona fría (cada vez más fría cuanto más al sur)
/// Ajusta niebla, sol y partículas de polvo, y expone la temperatura actual para la Fase 1.
/// </summary>
public class BiomeMap : MonoBehaviour
{
    public static BiomeMap Instance { get; private set; }

    [Header("Biomas")]
    [SerializeField] private BiomeDefinition baseBiome;
    [SerializeField] private BiomeDefinition northBiome;
    [SerializeField] private BiomeDefinition southBiome;

    [Header("Límites (eje Z del mundo)")]
    [SerializeField] private float northStart = 260f;
    [SerializeField] private float northFull = 380f;
    [SerializeField] private float southStart = -260f;
    [SerializeField] private float southFull = -380f;
    [Tooltip("Temperatura en el extremo sur del mapa.")]
    [SerializeField] private float deepSouthTemperature = -100f;
    [SerializeField] private float mapSouthEdge = -590f;

    [Header("Referencias")]
    [SerializeField] private Transform player;
    [SerializeField] private Light sun;
    [SerializeField] private Material dustMaterial;

    [Header("Transición")]
    [SerializeField, Min(0.01f)] private float blendSpeed = 1.2f;
    [SerializeField] private int maxDustParticlesPerSecond = 450;

    public BiomeDefinition CurrentBiome { get; private set; }
    public float CurrentTemperature { get; private set; }
    public float DustAmount { get; private set; }

    private Color fogColor;
    private float fogDensity;
    private Color sunColor;
    private float sunIntensity;
    private ParticleSystem dust;

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (player == null)
        {
            Inventory inventory = FindFirstObjectByType<Inventory>();
            if (inventory != null) player = inventory.transform;
        }
        if (sun == null) sun = FindDirectionalLight();

        CreateDust();
        Sample(out Color fc, out float fd, out Color sc, out float si, out float dustTarget, out float temp, out BiomeDefinition dominant);
        fogColor = fc; fogDensity = fd; sunColor = sc; sunIntensity = si; DustAmount = dustTarget; CurrentTemperature = temp;
        CurrentBiome = dominant;
        Apply();
    }

    private void Update()
    {
        if (player == null || baseBiome == null) return;

        Sample(out Color fc, out float fd, out Color sc, out float si, out float dustTarget, out float temp, out BiomeDefinition dominant);

        float t = 1f - Mathf.Exp(-blendSpeed * Time.deltaTime);
        fogColor = Color.Lerp(fogColor, fc, t);
        fogDensity = Mathf.Lerp(fogDensity, fd, t);
        sunColor = Color.Lerp(sunColor, sc, t);
        sunIntensity = Mathf.Lerp(sunIntensity, si, t);
        DustAmount = Mathf.Lerp(DustAmount, dustTarget, t);
        CurrentTemperature = Mathf.Lerp(CurrentTemperature, temp, t);
        Apply();

        if (dominant != CurrentBiome)
        {
            CurrentBiome = dominant;
            if (dominant != null) NotificationQueue.Enqueue($"Zona: {dominant.displayName}");
        }
    }

    private void Sample(out Color fc, out float fd, out Color sc, out float si, out float dustTarget, out float temp, out BiomeDefinition dominant)
    {
        float z = player != null ? player.position.z : 0f;
        float wN = northBiome != null ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(northStart, northFull, z)) : 0f;
        float wS = southBiome != null ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(southStart, southFull, z)) : 0f;
        float wB = Mathf.Clamp01(1f - wN - wS);

        fc = baseBiome.fogColor * wB;
        fd = baseBiome.fogDensity * wB;
        sc = baseBiome.sunColor * wB;
        si = baseBiome.sunIntensity * wB;
        dustTarget = baseBiome.dust * wB;
        temp = baseBiome.temperatureCelsius * wB;

        if (wN > 0f) Add(northBiome, wN, ref fc, ref fd, ref sc, ref si, ref dustTarget, ref temp);
        if (wS > 0f)
        {
            Add(southBiome, wS, ref fc, ref fd, ref sc, ref si, ref dustTarget, ref temp);
            // Cuanto más al sur, más frío.
            float depth = Mathf.InverseLerp(southFull, mapSouthEdge, z);
            temp += (deepSouthTemperature - southBiome.temperatureCelsius) * depth * wS;
        }

        dominant = wN > 0.5f ? northBiome : wS > 0.5f ? southBiome : baseBiome;
    }

    private static void Add(BiomeDefinition b, float w, ref Color fc, ref float fd, ref Color sc, ref float si, ref float dustTarget, ref float temp)
    {
        fc += b.fogColor * w;
        fd += b.fogDensity * w;
        sc += b.sunColor * w;
        si += b.sunIntensity * w;
        dustTarget += b.dust * w;
        temp += b.temperatureCelsius * w;
    }

    private void Apply()
    {
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
        if (sun != null)
        {
            sun.color = sunColor;
            sun.intensity = sunIntensity;
        }

        if (dust != null && player != null)
        {
            dust.transform.position = player.position + Vector3.up * 4f;
            ParticleSystem.EmissionModule emission = dust.emission;
            emission.rateOverTime = DustAmount * maxDustParticlesPerSecond;
            if (northBiome != null)
            {
                ParticleSystem.MainModule main = dust.main;
                main.startColor = northBiome.dustColor;
            }
        }
    }

    private void CreateDust()
    {
        var go = new GameObject("BiomeDust");
        go.transform.SetParent(transform, false);
        dust = go.AddComponent<ParticleSystem>();
        dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = dust.main;
        main.loop = true;
        main.startLifetime = 7f;
        main.startSpeed = 0.4f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.22f);
        main.maxParticles = 4000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = true;

        ParticleSystem.ShapeModule shape = dust.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(70f, 16f, 70f);

        // Viento que arrastra el polvo
        ParticleSystem.VelocityOverLifetimeModule velocity = dust.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(1.5f, 4f);
        velocity.y = new ParticleSystem.MinMaxCurve(-0.2f, 0.3f);
        velocity.z = new ParticleSystem.MinMaxCurve(-1f, 1f);

        ParticleSystem.EmissionModule emission = dust.emission;
        emission.rateOverTime = 0f;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        if (dustMaterial != null) renderer.sharedMaterial = dustMaterial;

        dust.Play();
    }

    private static Light FindDirectionalLight()
    {
        foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (light.type == LightType.Directional) return light;
        return null;
    }
}
