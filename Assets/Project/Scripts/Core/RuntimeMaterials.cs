using UnityEngine;

/// <summary>Materiales creados por código (URP Lit, con emisión opcional).</summary>
public static class RuntimeMaterials
{
    public static Material Lit(Color color, float emission = 0f)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var material = new Material(shader) { color = color };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (emission > 0f)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * emission);
        }
        return material;
    }
}
