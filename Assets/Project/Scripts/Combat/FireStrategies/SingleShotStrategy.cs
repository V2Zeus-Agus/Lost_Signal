using UnityEngine;

public class SingleShotStrategy : IFireStrategy
{
    public string Name => "Disparo simple";
    public bool IsAutomatic => true;
    public float CooldownMultiplier => 1f;

    public void Fire(Transform muzzle, ProjectilePool pool, float damage, float speed)
    {
        pool.Get(muzzle.position, muzzle.rotation).Launch(damage, speed);
    }
}
