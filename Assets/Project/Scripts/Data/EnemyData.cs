using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Game Data/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Prefab")]
    public Creature prefab;



    [Header("Stats")]
    public float maxHealth = 50f;
    public float moveSpeed = 3.5f;
    public float damage = 10f;
    public float attackCooldown = 1.2f;



    [Header("Ranges")]
    public float detectionRange = 30f;
    public float attackRange = 2f;

    [Header("Patrol")]
    public float patrolRadius = 8f;
    [Range(0.1f, 1f)] public float patrolSpeedMultiplier = 0.5f;
    public float minPatrolWait = 1f;
    public float maxPatrolWait = 3f;
}
