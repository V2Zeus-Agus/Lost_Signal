using UnityEngine;

public class BurstShotStrategy : IFireStrategy
{
    private readonly int projectileCount;
    private readonly float spreadAngle;

    public BurstShotStrategy(int projectileCount, float spreadAngle)
    {
        this.projectileCount = Mathf.Max(2, projectileCount);
        this.spreadAngle = spreadAngle;
    }

    public string Name => "Ráfaga";
    public bool IsAutomatic => false;
    public float CooldownMultiplier => 2.5f;

    public void Fire(Transform muzzle, ProjectilePool pool, float damage, float speed)
    {
        float step = (spreadAngle * 2f) / (projectileCount - 1);

        for (int i = 0; i < projectileCount; i++)
        {
            float angle = -spreadAngle + step * i;
            Quaternion rotation = muzzle.rotation * Quaternion.Euler(0f, angle, 0f);
            pool.Get(muzzle.position, rotation).Launch(damage, speed);
        }
    }
}
