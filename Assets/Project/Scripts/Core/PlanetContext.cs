using UnityEngine;

/// <summary>
/// Dice en qué planeta estamos. Va en cada escena de planeta.
/// Cualquier sistema consulta PlanetContext.Current en vez de usar valores fijos.
/// </summary>
[DefaultExecutionOrder(-100)]
public class PlanetContext : MonoBehaviour
{
    [SerializeField] private PlanetDefinition planet;

    public static PlanetDefinition Current { get; private set; }

    public static string CurrentId => Current != null ? Current.Id : "default";
    public static float OxygenDrainMultiplier => Current != null ? Current.oxygenDrainMultiplier : 1f;

    private void Awake()
    {
        if (planet == null)
            Debug.LogWarning("[PlanetContext] No hay PlanetDefinition asignado. Se usan valores por defecto.", this);
        Current = planet;
    }

    private void OnDestroy()
    {
        if (Current == planet) Current = null;
    }
}
