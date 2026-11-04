using System;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(Health))]
public class Creature : MonoBehaviour, IPushable
{
    public event Action<Creature> Died;

    public EnemyData Data { get; private set; }

    public PatrolState Patrol { get; private set; }
    public ChaseState Chase { get; private set; }
    public AttackState Attack { get; private set; }

    private NavMeshAgent agent;
    private Health health;
    private Transform target;
    private IDamageable targetDamageable;
    private ICreatureState currentState;
    private float nextAttackTime;
    private Vector3 homePosition;
    private Vector3 knockback;
    private const float KnockbackDecay = 25f;

    public float DistanceToTarget => Vector3.Distance(transform.position, target.position);

    public bool HasReachedDestination =>
        agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();

        Patrol = new PatrolState(this);
        Chase = new ChaseState(this);
        Attack = new AttackState(this);
    }

    private void OnEnable() => health.OnDied += HandleDeath;
    private void OnDisable() => health.OnDied -= HandleDeath;



    public void Setup(EnemyData data, Transform targetTransform, IDamageable damageableTarget, Vector3 spawnPosition)
    {
        Data = data;


        target = targetTransform;
        targetDamageable = damageableTarget;

        agent.Warp(spawnPosition);
        agent.speed = data.moveSpeed;
        homePosition = spawnPosition;
        nextAttackTime = 0f;
        knockback = Vector3.zero;
        agent.stoppingDistance = data.attackRange * 0.8f;
        health.Initialize(data.maxHealth);



        //health.OnDied += HandleDeath;
        ChangeState(Patrol);
    }

    private void Update()
    {
        if (currentState == null || !GameManager.Instance.IsPlaying) return;
        ApplyKnockback();
        currentState.Tick();
    }

    public void Push(Vector3 direction, float force)
    {
        direction.y = 0f;
        knockback += direction.normalized * force;
    }

    private void ApplyKnockback()
    {
        if (knockback.sqrMagnitude < 0.01f || !agent.isOnNavMesh) return;
        agent.Move(knockback * Time.deltaTime);
        knockback = Vector3.MoveTowards(knockback, Vector3.zero, KnockbackDecay * Time.deltaTime);
    }

    public void ChangeState(ICreatureState newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState.Enter();
    }

    public void MoveToTarget()
    {
        if (!agent.isOnNavMesh) return;
        agent.speed = Data.moveSpeed;
        agent.isStopped = false;
        agent.SetDestination(target.position);
    }

    public void MoveToRandomPointNearHome()
    {
        if (!agent.isOnNavMesh) return;

        Vector2 offset = UnityEngine.Random.insideUnitCircle * Data.patrolRadius;
        Vector3 candidate = homePosition + new Vector3(offset.x, 0f, offset.y);
        if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, Data.patrolRadius, NavMesh.AllAreas)) return;

        agent.speed = Data.moveSpeed * Data.patrolSpeedMultiplier;
        agent.isStopped = false;
        agent.SetDestination(hit.position);
    }

    public void StopMoving()
    {
        if (!agent.isOnNavMesh) return;
        agent.isStopped = true;
        agent.ResetPath();
    }

    public void FaceTarget()
    {
        Vector3 direction = target.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) return;

        Quaternion look = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, 10f * Time.deltaTime);
    }

    public void TryAttack()
    {
        if (Time.time < nextAttackTime) return;

        nextAttackTime = Time.time + Data.attackCooldown;
        targetDamageable.TakeDamage(Data.damage);
    }

    private void HandleDeath()
    {
        NotificationQueue.Enqueue("Criatura eliminada");
        Died?.Invoke(this);
    }
}
