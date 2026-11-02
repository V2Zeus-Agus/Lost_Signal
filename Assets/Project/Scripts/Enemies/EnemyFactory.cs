using System;
using UnityEngine;

public class EnemyFactory : MonoBehaviour
{
    [SerializeField] private EnemyData enemyData;
    [SerializeField] private Transform player;
    [SerializeField] private int maxCreatures = 28;

    private IDamageable playerDamageable;
    private GameObjectPool<Creature> pool;

    public int ActiveCount => pool.ActiveCount;

    public event Action<Vector3> OnCreatureKilled;

    private void Awake()
    {
        playerDamageable = player.GetComponent<IDamageable>();
        pool = new GameObjectPool<Creature>(CreateCreature, maxCreatures);
    }

    public Creature Create(Vector3 position)
    {
        if (!pool.TryGet(position, Quaternion.identity, out Creature creature))
            return null;

        creature.Setup(enemyData, player, playerDamageable, position);
        return creature;
    }

    private Creature CreateCreature(Vector3 position, Quaternion rotation)
    {
        Creature creature = Instantiate(enemyData.prefab, position, rotation, transform);
        creature.Died += ReturnToPool;
        return creature;
    }

    private void ReturnToPool(Creature creature)
    {
        OnCreatureKilled?.Invoke(creature.transform.position);
        pool.Release(creature);
    }
}
