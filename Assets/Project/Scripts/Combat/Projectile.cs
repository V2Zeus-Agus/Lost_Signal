using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private float knockbackForce = 7f;

    private ProjectilePool pool;
    private float damage;
    private float speed;
    private float timer;

    private void Awake()
    {
        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    public void SetPool(ProjectilePool owner) => pool = owner;

    public void Launch(float damageAmount, float travelSpeed)
    {
        damage = damageAmount;
        speed = travelSpeed;
        timer = lifetime;
    }

    private void Update()
    {
        transform.position += transform.forward * (speed * Time.deltaTime);

        timer -= Time.deltaTime;
        if (timer <= 0f) ReturnToPool();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.isTrigger || other.CompareTag("Player")) return;

        IDamageable target = other.GetComponentInParent<IDamageable>();
        target?.TakeDamage(damage);

        IPushable pushable = other.GetComponentInParent<IPushable>();
        pushable?.Push(transform.forward, knockbackForce);

        ReturnToPool();
    }

    private void ReturnToPool() => pool.Release(this);
}
