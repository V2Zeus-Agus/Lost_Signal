using UnityEngine;
using UnityEngine.AI;

public class CreatureNest : MonoBehaviour
{
    [SerializeField] private EnemyFactory factory;
    [SerializeField] private float spawnInterval = 8f;
    [SerializeField] private float spawnRadius = 4f;
    [SerializeField] private int maxAlive = 3;

    private int aliveCount;
    private float timer;
    private bool announced;

    private void Update()
    {
        if (!GameManager.Instance.IsPlaying || aliveCount >= maxAlive) return;

        timer += Time.deltaTime;
        if (timer < spawnInterval) return;

        timer = 0f;
        Spawn();
    }




    private void Spawn()
    {
        Vector2 offset = Random.insideUnitCircle * spawnRadius;
        Vector3 candidate = transform.position + new Vector3(offset.x, 0f, offset.y);

        if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, spawnRadius, NavMesh.AllAreas))
            return;



        Creature creature = factory.Create(hit.position);
        if (creature == null) return;



        creature.Died += OnCreatureDied;
        aliveCount++;

        if (!announced)
        {
            announced = true;
            NotificationQueue.Enqueue("¡Un nido está activo!");
        }
    }

    private void OnCreatureDied(Creature creature)
    {
        creature.Died -= OnCreatureDied;
        aliveCount--;
    }
}
