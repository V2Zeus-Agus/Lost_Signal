using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Decide cuándo pasa un evento de entorno y lo actualiza. Uno a la vez.
/// - Tormenta de polvo (norte) y ola de frío (sur): pueden pasar desde el principio.
/// - Lluvia de meteoritos: solo si el jugador está avanzado (ProgressionGate) y con probabilidad baja.
/// Solo cuenta el tiempo jugado (en pausa no avanza). Observer: avisa con OnEventStarted/Ended.
/// </summary>
public class EnvironmentEventManager : MonoBehaviour
{
    public static EnvironmentEventManager Instance { get; private set; }

    [Header("Referencias")]
    [SerializeField] private BiomeMap biomes;
    [SerializeField] private PlayerUpgrades playerUpgrades;
    [SerializeField] private ShipRepair shipRepair;
    [Tooltip("Recursos que pueden dar los fragmentos de meteorito.")]
    [SerializeField] private ItemDefinition[] meteorLoot;

    [Header("Ritmo")]
    [Tooltip("Segundos de juego antes del primer evento posible.")]
    [SerializeField] private float firstRollDelay = 240f;
    [Tooltip("Cada cuántos segundos se tira el dado (si no hay evento activo).")]
    [SerializeField] private float rollInterval = 180f;

    [Header("Probabilidad por tirada")]
    [SerializeField, Range(0f, 1f)] private float dustStormChance = 0.3f;
    [SerializeField, Range(0f, 1f)] private float coldWaveChance = 0.25f;
    [Tooltip("Solo si el jugador está avanzado. Baja a propósito.")]
    [SerializeField, Range(0f, 1f)] private float meteorChance = 0.08f;

    private readonly EnvironmentContext context = new EnvironmentContext();
    private readonly DustStormEvent dustStorm = new DustStormEvent();
    private readonly ColdWaveEvent coldWave = new ColdWaveEvent();
    private readonly MeteorShowerEvent meteorShower = new MeteorShowerEvent();
    private float timer;

    public EnvironmentEvent Current { get; private set; }
    public bool IsAdvanced => ProgressionGate.IsAdvanced(playerUpgrades, shipRepair);

    public event Action<EnvironmentEvent> OnEventStarted;
    public event Action<EnvironmentEvent> OnEventEnded;

    private void Awake()
    {
        Instance = this;
        if (biomes == null) biomes = FindFirstObjectByType<BiomeMap>();
        if (playerUpgrades == null) playerUpgrades = FindFirstObjectByType<PlayerUpgrades>();
        if (shipRepair == null) shipRepair = FindFirstObjectByType<ShipRepair>();

        context.biomes = biomes;
        context.upgrades = playerUpgrades;
        context.player = playerUpgrades != null ? playerUpgrades.transform : null;
        context.playerHealth = playerUpgrades != null ? playerUpgrades.GetComponent<Health>() : null;
        context.meteorLoot = meteorLoot;
        timer = rollInterval - firstRollDelay; // la primera tirada llega a los firstRollDelay segundos
    }

    private void OnDestroy()
    {
        if (Current != null) Current.End(context);
        EnvironmentEffects.OxygenDrainBonus = 0f;
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;
        float dt = Time.deltaTime;

        if (Current != null)
        {
            Current.Tick(context, dt);
            if (Current.IsFinished) Stop();
            return;
        }

        timer += dt;
        if (timer < rollInterval) return;
        timer = 0f;
        Roll();
    }

    private void Roll()
    {
        // Los meteoritos van primero porque son raros; si no salen, se prueba con el clima.
        if (IsAdvanced && UnityEngine.Random.value < meteorChance) { BeginEvent(meteorShower); return; }

        float r = UnityEngine.Random.value;
        if (r < dustStormChance) BeginEvent(dustStorm);
        else if (r < dustStormChance + coldWaveChance) BeginEvent(coldWave);
    }

    private void BeginEvent(EnvironmentEvent environmentEvent)
    {
        if (Current != null || !environmentEvent.CanStart(context)) return;
        Current = environmentEvent;
        Current.Begin(context);
        NotificationQueue.Enqueue(Current.StartMessage);
        OnEventStarted?.Invoke(Current);
    }

    public void Stop()
    {
        if (Current == null) return;
        EnvironmentEvent ended = Current;
        Current.End(context);
        Current = null;
        timer = 0f;
        NotificationQueue.Enqueue(ended.EndMessage);
        OnEventEnded?.Invoke(ended);
    }

    /// <summary>Consola de desarrollador: forzar un evento ("dust_storm", "cold_wave", "meteor_shower").</summary>
    public void Force(string id)
    {
        Stop();
        foreach (EnvironmentEvent e in new EnvironmentEvent[] { dustStorm, coldWave, meteorShower })
            if (e.Id == id) BeginEvent(e);
    }
}
