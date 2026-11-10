using System.Collections.Generic;
using UnityEngine;

/// <summary>Un lugar con aire y calor (hábitat presurizado). La nave usa su propio radio.</summary>
public interface IShelter
{
    string ShelterName { get; }
    bool Contains(Vector3 worldPosition);
}

/// <summary>
/// Registro global de refugios. PlayerOxygen, el frío y los meteoritos preguntan acá
/// si el jugador está a salvo, sin conocer los hábitats directamente.
/// </summary>
public static class ShelterRegistry
{
    private static readonly List<IShelter> shelters = new List<IShelter>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => shelters.Clear();

    public static void Register(IShelter shelter)
    {
        if (shelter != null && !shelters.Contains(shelter)) shelters.Add(shelter);
    }

    public static void Unregister(IShelter shelter) => shelters.Remove(shelter);

    public static IShelter Find(Vector3 position)
    {
        foreach (IShelter shelter in shelters)
            if (shelter != null && shelter.Contains(position)) return shelter;
        return null;
    }

    public static bool IsSheltered(Vector3 position) => Find(position) != null;
}

/// <summary>
/// Efectos de entorno activos que modifican al jugador (los escribe EnvironmentEventManager).
/// </summary>
public static class EnvironmentEffects
{
    /// <summary>Consumo de O2 extra fuera de refugios: 0,6 = +60%.</summary>
    public static float OxygenDrainBonus { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => OxygenDrainBonus = 0f;
}
