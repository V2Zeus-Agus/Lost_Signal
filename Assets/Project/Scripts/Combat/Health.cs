using System;
using UnityEngine;

public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;

    public float Current { get; private set; }
    public float Max => maxHealth;
    public bool IsDead => Current <= 0f;

    public event Action<float, float> OnHealthChanged;
    public event Action OnDied;

    private void Awake()
    {
        Current = maxHealth;
    }

    public void Initialize(float max)
    {
        maxHealth = max;
        Current = max;
        OnHealthChanged?.Invoke(Current, maxHealth);
    }

    public void RestoreFull()
    {
        if (IsDead) return;
        Current = maxHealth;
        OnHealthChanged?.Invoke(Current, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;

        Current = Mathf.Max(Current - amount, 0f);
        OnHealthChanged?.Invoke(Current, maxHealth);

        if (IsDead) OnDied?.Invoke();
    }
}
