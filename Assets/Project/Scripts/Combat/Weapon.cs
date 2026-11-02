using System;
using UnityEngine;

public class Weapon : MonoBehaviour
{
    [SerializeField] private Transform muzzle;
    [SerializeField] private ProjectilePool projectilePool;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float projectileSpeed = 40f;
    [SerializeField] private float shotsPerSecond = 6f;

    private IFireStrategy[] strategies;
    private int currentIndex;
    private float nextFireTime;

    public string CurrentStrategyName => strategies[currentIndex].Name;
    public bool IsAutomatic => strategies[currentIndex].IsAutomatic;

    public event Action<string> OnStrategyChanged;
    public event Action OnFired;

    private void Awake()
    {
        strategies = new IFireStrategy[]
        {
            new SingleShotStrategy(),
            new BurstShotStrategy(3, 8f)
        };
    }

    public void TryFire()
    {
        if (Time.time < nextFireTime) return;

        IFireStrategy strategy = strategies[currentIndex];
        nextFireTime = Time.time + strategy.CooldownMultiplier / shotsPerSecond;
        strategy.Fire(muzzle, projectilePool, damage, projectileSpeed);
        OnFired?.Invoke();
    }

    public void NextStrategy()
    {
        currentIndex = (currentIndex + 1) % strategies.Length;
        OnStrategyChanged?.Invoke(CurrentStrategyName);
    }
}
