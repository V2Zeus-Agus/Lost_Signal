using UnityEngine;

/// <summary>
/// Curva de mejoras del tanque de O2 (nivel 0 a 10):
///   Niveles 1-4: + segundos base (8 s) cada uno, costo base.
///   Niveles 5-9: +25 s cada uno; el costo pasa a una base de 10 y se multiplica x1.3 por nivel.
///   Nivel 10 (máximo): +60 s.
/// La usan PlayerOxygen (capacidad) y OxygenShop (costo), así no se desincronizan.
/// </summary>
public static class OxygenTankProgression
{
    public const int MaxLevel = 10;
    public const int ScalingStartLevel = 5;
    public const float CostMultiplier = 1.3f;
    public const float MidTierSeconds = 25f;
    public const float FinalLevelSeconds = 60f;
    public const int DefaultScaledBaseCost = 10;

    /// <summary>Segundos que agrega un nivel puntual.</summary>
    public static float SecondsForLevel(int level, float earlySeconds)
    {
        if (level <= 0) return 0f;
        if (level >= MaxLevel) return FinalLevelSeconds;
        if (level >= ScalingStartLevel) return MidTierSeconds;
        return earlySeconds;
    }

    /// <summary>Segundos extra totales con el tanque en ese nivel.</summary>
    public static float TotalBonus(int level, float earlySeconds)
    {
        float total = 0f;
        for (int l = 1; l <= Mathf.Min(level, MaxLevel); l++) total += SecondsForLevel(l, earlySeconds);
        return total;
    }

    /// <summary>Costo de comprar ese nivel (1..10).</summary>
    /// <param name="earlyCost">Costo fijo de los niveles 1-4.</param>
    /// <param name="scaledBaseCost">Base desde el nivel 5, a la que se le aplica x1.3 por nivel.</param>
    public static int CostForLevel(int level, int earlyCost, int scaledBaseCost = DefaultScaledBaseCost)
    {
        if (level < ScalingStartLevel) return earlyCost;
        int steps = level - ScalingStartLevel + 1; // nivel 5 = base x1.3, nivel 6 = base x1.3², ...
        return Mathf.CeilToInt(scaledBaseCost * Mathf.Pow(CostMultiplier, steps));
    }
}
