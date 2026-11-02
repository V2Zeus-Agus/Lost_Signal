public class AttackState : ICreatureState
{
    private const float ExitRangeMultiplier = 1.2f;

    private readonly Creature creature;

    public AttackState(Creature creature)
    {
        this.creature = creature;
    }

    public void Enter() => creature.StopMoving();

    public void Tick()
    {
        if (creature.DistanceToTarget > creature.Data.attackRange * ExitRangeMultiplier)
        {
            creature.ChangeState(creature.Chase);
            return;
        }

        creature.FaceTarget();
        creature.TryAttack();
    }

    public void Exit() { }
}
