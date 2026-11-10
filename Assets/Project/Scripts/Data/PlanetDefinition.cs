using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Todo lo que hace distinto a un planeta. Los sistemas (oxígeno, clima, spawns)
/// leen sus valores de acá a través de PlanetContext, nunca de números fijos.
/// </summary>
[CreateAssetMenu(fileName = "NewPlanet", menuName = "Lost Signal/Planet")]
public class PlanetDefinition : ScriptableObject
{
    [Header("Identidad")]
    public string id;
    public string displayName = "Planeta";
    [TextArea(2, 5)] public string description;
    [Tooltip("Nombre de la escena de este planeta (para el mapa estelar).")]
    public string sceneName;

    [Header("Entorno (el enemigo principal)")]
    [Tooltip("Multiplica el consumo de O2 fuera de zonas seguras. 2 = el oxígeno dura la mitad.")]
    [Min(0.1f)] public float oxygenDrainMultiplier = 1f;
    [Tooltip("Presión en unidades de juego. El traje base soporta 1. (Se usa desde la Fase 1.)")]
    [Min(0f)] public float pressure = 1f;
    [Tooltip("Temperatura promedio en °C. (Se usa desde la Fase 1.)")]
    public float temperatureCelsius = -60f;
    [Tooltip("Presión real para mostrar en el HUD (kPa). Marte: 0,6. Tierra: 101.")]
    [Min(0f)] public float pressureKPa = 101f;
    [Min(0.1f)] public float gravityMultiplier = 1f;

    [Header("Recursos")]
    [Tooltip("Recursos que se pueden encontrar en este planeta.")]
    public List<ItemDefinition> availableResources = new List<ItemDefinition>();
    [Tooltip("Recursos que solo existen en este planeta.")]
    public List<ItemDefinition> exclusiveResources = new List<ItemDefinition>();

    [Header("Viaje")]
    [Tooltip("Combustible necesario para lanzar la nave hacia este planeta.")]
    [Min(0)] public int launchFuelCost = 50;

    public string Id => string.IsNullOrEmpty(id) ? name.ToLowerInvariant() : id;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(id)) id = name.ToLowerInvariant();
    }
#endif
}
