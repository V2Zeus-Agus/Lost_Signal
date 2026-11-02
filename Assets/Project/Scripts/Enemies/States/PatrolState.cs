using UnityEngine;

public class PatrolState : ICreatureState
{
    private readonly Creature creature;
    private float waitTimer;

    public PatrolState(Creature creature)
    {
        this.creature = creature;
    }

    public void Enter() => PickNextPoint();

    public void Tick()
    {
        if (creature.DistanceToTarget <= creature.Data.detectionRange)
        {
            creature.ChangeState(creature.Chase);
            return;
        }

        if (!creature.HasReachedDestination) return;

        waitTimer -= Time.deltaTime;
        if (waitTimer <= 0f) PickNextPoint();
    }

    public void Exit() { }

    private void PickNextPoint()
    {
        waitTimer = Random.Range(creature.Data.minPatrolWait, creature.Data.maxPatrolWait);
        creature.MoveToRandomPointNearHome();
    }
}
