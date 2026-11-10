using UnityEngine;

/// <summary>Lo que un evento necesita del mundo (se lo pasa el manager).</summary>
public class EnvironmentContext
{
    public BiomeMap biomes;
    public Transform player;
    public PlayerUpgrades upgrades;
    public Health playerHealth;
    public ItemDefinition[] meteorLoot; // qué pueden dar los fragmentos de meteorito

    public int SuitLevel => upgrades != null ? upgrades.GetLevel(UpgradeIds.Suit) : 0;
    public bool PlayerSheltered => player != null && ShelterRegistry.IsSheltered(player.position);
}

/// <summary>
/// Un evento de entorno (tormenta, ola de frío, meteoritos). El manager elige cuál arranca
/// y lo actualiza; cada evento sabe qué hace (patrón State/Strategy, como los estados de la criatura).
/// Tiene una subida y bajada suaves: Intensity va de 0 a 1 y vuelve a 0.
/// </summary>
public abstract class EnvironmentEvent
{
    private const float FadeSeconds = 8f;

    public abstract string Id { get; }
    public abstract string DisplayName { get; }
    public abstract string StartMessage { get; }
    public abstract string EndMessage { get; }
    protected abstract Vector2 DurationRange { get; }

    public float Duration { get; private set; }
    public float Elapsed { get; private set; }
    public float Remaining => Mathf.Max(Duration - Elapsed, 0f);
    public bool IsFinished => Elapsed >= Duration;

    /// <summary>0 a 1 con subida y bajada de unos segundos.</summary>
    public float Intensity =>
        Mathf.Clamp01(Mathf.Min(Elapsed / FadeSeconds, (Duration - Elapsed) / FadeSeconds));

    public virtual bool CanStart(EnvironmentContext context) => true;

    public void Begin(EnvironmentContext context)
    {
        Duration = Random.Range(DurationRange.x, DurationRange.y);
        Elapsed = 0f;
        OnBegin(context);
    }

    public void Tick(EnvironmentContext context, float deltaTime)
    {
        Elapsed += deltaTime;
        OnTick(context, deltaTime);
    }

    public void End(EnvironmentContext context) => OnEnd(context);

    protected virtual void OnBegin(EnvironmentContext context) { }
    protected abstract void OnTick(EnvironmentContext context, float deltaTime);
    protected abstract void OnEnd(EnvironmentContext context);
}
