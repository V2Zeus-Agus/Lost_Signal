using System;

/// <summary>
/// Trucos de prueba que se prenden desde la consola F8 (solo builds de desarrollo).
/// Materiales infinitos: comprar mejoras y construir no pide ni gasta recursos.
/// </summary>
public static class DevCheats
{
    private static bool infiniteMaterials;

    public static event Action OnChanged;

    public static bool InfiniteMaterials
    {
        get => infiniteMaterials;
        set
        {
            if (infiniteMaterials == value) return;
            infiniteMaterials = value;
            OnChanged?.Invoke();
        }
    }
}
