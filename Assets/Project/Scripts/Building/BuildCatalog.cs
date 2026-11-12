using System.Collections.Generic;
using UnityEngine;

/// <summary>Lista de construibles y materiales de la construcción.</summary>
[CreateAssetMenu(fileName = "BuildCatalog", menuName = "Lost Signal/Build Catalog")]
public class BuildCatalog : ScriptableObject
{
    public List<BuildableDefinition> buildables = new List<BuildableDefinition>();

    [Header("Materiales")]
    public Material hull;
    public Material trim;
    public Material glass;
    public Material floor;
    public Material glow;
    public Material hologramValid;
    public Material hologramInvalid;

    [Header("Decoración del hábitat (opcional)")]
    [Tooltip("Modelo que se pone sobre el techo de cada módulo (p. ej. paneles solares de KayKit).")]
    public GameObject habitatRoofDecor;

    public BuildableDefinition Get(string id) => buildables.Find(b => b != null && b.Id == id);

    // Fallbacks por si falta algún material.
    public Material Hull => hull != null ? hull : hull = RuntimeMaterials.Lit(new Color(0.78f, 0.8f, 0.84f));
    public Material Trim => trim != null ? trim : trim = RuntimeMaterials.Lit(new Color(1f, 0.5f, 0.15f));
    public Material Glass => glass != null ? glass : glass = RuntimeMaterials.Lit(new Color(0.45f, 0.8f, 1f), 1.2f);
    public Material Floor => floor != null ? floor : floor = RuntimeMaterials.Lit(new Color(0.25f, 0.27f, 0.3f));
    public Material Glow => glow != null ? glow : glow = RuntimeMaterials.Lit(new Color(0.3f, 0.95f, 1f), 3f);
    public Material HologramValid => hologramValid != null ? hologramValid : hologramValid = RuntimeMaterials.Lit(new Color(0.3f, 1f, 0.5f), 1.5f);
    public Material HologramInvalid => hologramInvalid != null ? hologramInvalid : hologramInvalid = RuntimeMaterials.Lit(new Color(1f, 0.25f, 0.2f), 1.5f);
}
