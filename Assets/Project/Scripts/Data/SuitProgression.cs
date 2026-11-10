using UnityEngine;

/// <summary>
/// Reglas del traje por niveles (una sola fuente de verdad para frío, tormentas y meteoritos).
/// </summary>
public static class SuitProgression
{
    public const int MaxLevel = 3;
    public const float BaseColdLimit = -60f;
    public const float ColdLimitPerLevel = 20f;

    /// <summary>Temperatura (°C) desde la que el frío congela el visor y daña.</summary>
    public static float ColdLimit(int level) => BaseColdLimit - ColdLimitPerLevel * Mathf.Clamp(level, 0, MaxLevel);

    /// <summary>Consumo extra de O2 dentro de una tormenta de polvo (0,6 = +60%).</summary>
    public static float DustStormExtraDrain(int level) => 0.6f * (1f - 0.25f * Mathf.Clamp(level, 0, MaxLevel));

    /// <summary>Multiplicador del daño de los meteoritos.</summary>
    public static float MeteorDamageMultiplier(int level) => 1f - 0.2f * Mathf.Clamp(level, 0, MaxLevel);

    public static string Describe(int level) =>
        $"aguanta {ColdLimit(level):0} °C · tormenta +{DustStormExtraDrain(level) * 100f:0}% O2 · meteoritos {MeteorDamageMultiplier(level) * 100f:0}%";
}
