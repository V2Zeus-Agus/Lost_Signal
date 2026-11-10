using UnityEngine;

/// <summary>Tormenta de polvo en las Mesetas del norte: poca visibilidad y más consumo de O2.</summary>
public class DustStormEvent : EnvironmentEvent
{
    public override string Id => "dust_storm";
    public override string DisplayName => "Tormenta de polvo";
    public override string StartMessage => "ALERTA: Tormenta de polvo en las Mesetas del norte: más consumo de O2";
    public override string EndMessage => "La tormenta de polvo se calmó";
    protected override Vector2 DurationRange => new Vector2(70f, 110f);

    protected override void OnTick(EnvironmentContext context, float deltaTime)
    {
        if (context.biomes == null) return;
        context.biomes.StormIntensity = Intensity;
        float exposure = context.PlayerSheltered ? 0f : context.biomes.NorthWeight;
        EnvironmentEffects.OxygenDrainBonus = SuitProgression.DustStormExtraDrain(context.SuitLevel) * Intensity * exposure;
    }

    protected override void OnEnd(EnvironmentContext context)
    {
        if (context.biomes != null) context.biomes.StormIntensity = 0f;
        EnvironmentEffects.OxygenDrainBonus = 0f;
    }
}

/// <summary>Ola de frío en la Cuenca del sur: baja la temperatura unos 35 °C.</summary>
public class ColdWaveEvent : EnvironmentEvent
{
    public const float Degrees = -35f;

    public override string Id => "cold_wave";
    public override string DisplayName => "Ola de frío";
    public override string StartMessage => $"ALERTA: Ola de frío en la Cuenca del sur: {Degrees:0} °C";
    public override string EndMessage => "La ola de frío pasó";
    protected override Vector2 DurationRange => new Vector2(80f, 120f);

    protected override void OnTick(EnvironmentContext context, float deltaTime)
    {
        if (context.biomes != null) context.biomes.ColdWaveOffset = Degrees * Intensity;
    }

    protected override void OnEnd(EnvironmentContext context)
    {
        if (context.biomes != null) context.biomes.ColdWaveOffset = 0f;
    }
}
