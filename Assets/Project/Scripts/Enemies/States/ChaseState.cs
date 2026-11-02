public class ChaseState : ICreatureState
{
    private const float LoseTargetMultiplier = 1.5f;

    private readonly Creature creature;

    public ChaseState(Creature creature)
    {
        this.creature = creature;
    }

    public void Enter() { }

    public void Tick()
    {
        float distance = creature.DistanceToTarget;

        if (distance <= creature.Data.attackRange)
        {
            creature.ChangeState(creature.Attack);
            return;
        }

        if (distance > creature.Data.detectionRange * LoseTargetMultiplier)
        {
            creature.ChangeState(creature.Patrol);
            return;
        }

        creature.MoveToTarget();
    }

    public void Exit() { }
}
