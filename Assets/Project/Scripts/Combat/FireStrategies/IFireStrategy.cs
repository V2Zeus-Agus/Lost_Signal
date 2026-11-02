using UnityEngine;

public interface IFireStrategy
{
    string Name { get; }

    bool IsAutomatic { get; }

    float CooldownMultiplier { get; }

    void Fire(Transform muzzle, ProjectilePool pool, float damage, float speed);
}
