using System;
using UnityEngine;

public class PlayerOxygen : MonoBehaviour
{
    [SerializeField] private float maxOxygen = 20f;
    [SerializeField] private float refillPerSecond = 10f;
    [SerializeField] private Transform safeZoneCenter;
    [SerializeField] private float safeZoneRadius = 10f;
    [SerializeField] private Health health;

    public float Current { get; private set; }
    public float Max => maxOxygen;
    public bool InSafeZone { get; private set; }

    public event Action<float, float> OnOxygenChanged;
    public event Action<bool> OnRefillingChanged;

    private bool lowWarningShown;
    private bool isRefilling;

    private void Awake()
    {
        Current = maxOxygen;
    }

    private void Update()
    {
        if (health.IsDead || !GameManager.Instance.IsPlaying) return;

        InSafeZone = IsNearShip();

        if (InSafeZone)
        {
            Current = Mathf.Min(Current + refillPerSecond * Time.deltaTime, maxOxygen);
            if (Current >= maxOxygen * 0.5f) lowWarningShown = false;
        }
        else
        {
            Current = Mathf.Max(Current - Time.deltaTime, 0f);
            if (!lowWarningShown && Current <= maxOxygen * 0.25f)
            {
                lowWarningShown = true;
                NotificationQueue.Enqueue("¡Oxígeno bajo! Volvé a la nave");
            }
        }

        OnOxygenChanged?.Invoke(Current, maxOxygen);

        bool refilling = InSafeZone && Current < maxOxygen;
        if (refilling != isRefilling)
        {
            isRefilling = refilling;
            OnRefillingChanged?.Invoke(isRefilling);
        }

        if (Current <= 0f && !InSafeZone)
        {
            GameManager.Instance.SetDefeatCause("TE QUEDASTE SIN OXÍGENO",
                "Volvé a la nave antes de que se acabe el aire.");
            health.TakeDamage(health.Max);
        }
    }

    public void UpgradeCapacity(float extraSeconds)
    {
        maxOxygen += extraSeconds;
        Current = maxOxygen;
        OnOxygenChanged?.Invoke(Current, maxOxygen);
    }

    private bool IsNearShip()
    {
        Vector3 offset = transform.position - safeZoneCenter.position;
        offset.y = 0f;
        return offset.magnitude <= safeZoneRadius;
    }
}
