using System;
using UnityEngine;

public class PlayerOxygen : MonoBehaviour
{
    [Tooltip("Capacidad base del tanque, sin mejoras.")]
    [SerializeField] private float maxOxygen = 20f;
    [Tooltip("Segundos extra por nivel en los niveles 1-4 del tanque (después rige OxygenTankProgression).")]
    [SerializeField] private float secondsPerTankLevel = 8f;
    [SerializeField] private float refillPerSecond = 10f;
    [SerializeField] private Transform safeZoneCenter;
    [SerializeField] private float safeZoneRadius = 10f;
    [SerializeField] private Health health;
    [SerializeField] private PlayerUpgrades upgrades;
    [SerializeField] private float suffocationSeconds = 2f;

    public float Current { get; private set; }
    public float Max => maxOxygen + OxygenTankProgression.TotalBonus(TankLevel, secondsPerTankLevel);
    public float SecondsPerTankLevel => secondsPerTankLevel;
    public int TankLevel => upgrades != null ? upgrades.GetLevel(UpgradeIds.OxygenTank) : 0;

    /// <summary>Segundos que agregaría el próximo nivel del tanque.</summary>
    public float NextLevelSeconds => OxygenTankProgression.SecondsForLevel(TankLevel + 1, secondsPerTankLevel);
    public bool InSafeZone { get; private set; }


    public event Action<float, float> OnOxygenChanged;
    public event Action<bool> OnRefillingChanged;

    private bool lowWarningShown;
    private bool isRefilling;
    private bool suffocationWarningShown;

    private void Awake()
    {
        if (upgrades == null) upgrades = GetComponent<PlayerUpgrades>();
        Current = Max;
    }

    private void OnEnable()
    {
        if (upgrades != null) upgrades.OnUpgradeChanged += HandleUpgradeChanged;
    }

    private void OnDisable()
    {
        if (upgrades != null) upgrades.OnUpgradeChanged -= HandleUpgradeChanged;
    }

    private void Update()
    {
        if (health.IsDead || !GameManager.Instance.IsPlaying) return;

        float max = Max;
        InSafeZone = IsNearShip();

        if (InSafeZone)
        {
            Current = Mathf.Min(Current + refillPerSecond * Time.deltaTime, max);
            if (Current >= max * 0.5f) lowWarningShown = false;
        }
        else
        {
            // El planeta define qué tan rápido se va el aire (x2 = dura la mitad).
            float drain = Time.deltaTime * PlanetContext.OxygenDrainMultiplier;
            Current = Mathf.Max(Current - drain, 0f);
            if (!lowWarningShown && Current <= max * 0.25f)
            {
                lowWarningShown = true;
                NotificationQueue.Enqueue("¡Oxígeno bajo! Volvé a la nave");
            }
        }

        OnOxygenChanged?.Invoke(Current, max);

        bool refilling = InSafeZone && Current < max;
        if (refilling != isRefilling)
        {
            isRefilling = refilling;
            OnRefillingChanged?.Invoke(isRefilling);
        }

        if (Current <= 0f && !InSafeZone)
            Suffocate();
        else
            suffocationWarningShown = false;
    }

    private void Suffocate()
    {
        if (!suffocationWarningShown)
        {
            suffocationWarningShown = true;
            NotificationQueue.Enqueue("¡Te estás asfixiando! Volvé a la nave");
        }

        float damage = health.Max / suffocationSeconds * Time.deltaTime;
        if (damage >= health.Current)
            GameManager.Instance.SetDefeatCause("TE QUEDASTE SIN OXÍGENO",
                "Volvé a la nave antes de que se acabe el aire.");

        health.TakeDamage(damage);
    }

    /// <summary>Llena el tanque (consola de desarrollador, futuras recargas).</summary>
    public void Refill()
    {
        Current = Max;
        OnOxygenChanged?.Invoke(Current, Max);
    }

    private void HandleUpgradeChanged(string upgradeId, int level)
    {
        if (upgradeId != UpgradeIds.OxygenTank) return;

        Current = Max; // mejorar el tanque lo llena, como antes
        OnOxygenChanged?.Invoke(Current, Max);
    }

    private bool IsNearShip()
    {
        Vector3 offset = transform.position - safeZoneCenter.position;
        offset.y = 0f;
        return offset.magnitude <= safeZoneRadius;
    }
}
