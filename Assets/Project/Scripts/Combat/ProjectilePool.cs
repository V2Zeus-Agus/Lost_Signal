using UnityEngine;

public class ProjectilePool : MonoBehaviour
{
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private int initialSize = 30;

    private GameObjectPool<Projectile> pool;

    private void Awake()
    {
        pool = new GameObjectPool<Projectile>(CreateProjectile);
        pool.Prewarm(initialSize, transform.position);
    }

    public Projectile Get(Vector3 position, Quaternion rotation)
    {
        pool.TryGet(position, rotation, out Projectile projectile);
        return projectile;
    }

    public void Release(Projectile projectile) => pool.Release(projectile);

    private Projectile CreateProjectile(Vector3 position, Quaternion rotation)
    {
        Projectile projectile = Instantiate(projectilePrefab, position, rotation, transform);
        projectile.SetPool(this);
        return projectile;
    }
}
