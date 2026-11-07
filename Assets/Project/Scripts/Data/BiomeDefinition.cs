using UnityEngine;

/// <summary>
/// Ambiente de una zona del planeta. BiomeMap mezcla estos valores según dónde esté el jugador.
/// La temperatura queda lista para la Fase 1 (traje y entorno).
/// </summary>
[CreateAssetMenu(fileName = "NewBiome", menuName = "Lost Signal/Biome")]
public class BiomeDefinition : ScriptableObject
{
    public string displayName = "Bioma";

    [Header("Atmósfera")]
    public Color fogColor = new Color(0.55f, 0.32f, 0.22f);
    [Min(0f)] public float fogDensity = 0.0045f;

    [Header("Luz")]
    public Color sunColor = new Color(1f, 0.9f, 0.8f);
    [Min(0f)] public float sunIntensity = 1.3f;

    [Header("Polvo en suspensión (0 = nada, 1 = tormenta)")]
    [Range(0f, 1f)] public float dust;
    public Color dustColor = new Color(0.85f, 0.6f, 0.4f, 0.35f);

    [Header("Entorno")]
    public float temperatureCelsius = -60f;
}
