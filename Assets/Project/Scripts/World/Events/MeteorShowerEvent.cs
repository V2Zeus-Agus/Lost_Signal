using UnityEngine;

/// <summary>
/// Lluvia de meteoritos: caen cerca del jugador con una marca de aviso 2,5 s antes.
/// Solo puede pasar si el jugador está avanzado (lo decide el manager) y es poco probable.
/// </summary>
public class MeteorShowerEvent : EnvironmentEvent
{
    private float nextImpact;

    public override string Id => "meteor_shower";
    public override string DisplayName => "Lluvia de meteoritos";
    public override string StartMessage => "ALERTA: ¡Lluvia de meteoritos! Mirá las marcas rojas o metete en un hábitat";
    public override string EndMessage => "La lluvia de meteoritos terminó. Los fragmentos tienen recursos";
    protected override Vector2 DurationRange => new Vector2(35f, 50f);

    protected override void OnBegin(EnvironmentContext context) => nextImpact = 2f;

    protected override void OnTick(EnvironmentContext context, float deltaTime)
    {
        if (context.player == null || Remaining < 4f) return;
        nextImpact -= deltaTime;
        if (nextImpact > 0f) return;
        nextImpact = Random.Range(1.2f, 2.6f);

        // Uno de cada tres cae bastante cerca: hay que moverse.
        float radius = Random.value < 0.33f ? Random.Range(3f, 12f) : Random.Range(12f, 60f);
        Vector2 offset = Random.insideUnitCircle.normalized * radius;
        Vector3 flat = context.player.position + new Vector3(offset.x, 0f, offset.y);
        if (!Physics.Raycast(flat + Vector3.up * 300f, Vector3.down, out RaycastHit hit, 600f, ~0, QueryTriggerInteraction.Ignore))
            return;

        Meteor.Launch(hit.point, context);
    }

    protected override void OnEnd(EnvironmentContext context) { }
}
