using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Módulo de hábitat (6 x 6 m). Si se pega otro al lado, la pared compartida desaparece
/// y el interior se agranda. Con un generador de O2 adentro (en cualquier módulo conectado)
/// queda presurizado: recarga O2 y protege del frío y de los meteoritos (IShelter).
/// </summary>
public class Habitat : MonoBehaviour, IShelter
{
    public const float Size = 6f;
    public const float FloorTop = 0.3f;
    public const float WallHeight = 3.2f;

    /// <summary>Lados en espacio local: 0 = frente (+Z, puerta), 1 = +X, 2 = -Z, 3 = -X.</summary>
    public static readonly Vector3[] SideDirections = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };

    private static readonly List<Habitat> all = new List<Habitat>();
    public static IReadOnlyList<Habitat> All => all;

    private readonly GameObject[] sides = new GameObject[4];
    private Light interiorLight;

    public bool IsPressurized { get; private set; }
    public string ShelterName => "hábitat presurizado";

    public void Init(GameObject[] sideObjects, Light light)
    {
        for (int i = 0; i < 4; i++) sides[i] = sideObjects[i];
        interiorLight = light;
        SetPressurized(false);
    }

    private void OnEnable()
    {
        if (!all.Contains(this)) all.Add(this);
    }

    private void OnDisable()
    {
        all.Remove(this);
        ShelterRegistry.Unregister(this);
    }

    public void SetSideOpen(int side, bool open)
    {
        if (sides[side] != null) sides[side].SetActive(!open);
    }

    public void SetPressurized(bool pressurized)
    {
        IsPressurized = pressurized;
        if (pressurized) ShelterRegistry.Register(this);
        else ShelterRegistry.Unregister(this);

        if (interiorLight != null)
        {
            interiorLight.color = pressurized ? new Color(1f, 0.93f, 0.8f) : new Color(1f, 0.3f, 0.25f);
            interiorLight.intensity = pressurized ? 2.2f : 0.7f;
        }
    }

    public bool Contains(Vector3 worldPosition)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        float half = Size * 0.5f;
        return Mathf.Abs(local.x) <= half && Mathf.Abs(local.z) <= half && local.y >= -0.5f && local.y <= FloorTop + WallHeight + 0.5f;
    }

    /// <summary>Hábitat (con o sin aire) que contiene ese punto.</summary>
    public static Habitat FindContaining(Vector3 worldPosition)
    {
        foreach (Habitat h in all)
            if (h != null && h.Contains(worldPosition)) return h;
        return null;
    }

    /// <summary>Qué lado de este hábitat mira hacia una dirección del mundo (-1 si ninguno).</summary>
    public int SideFacing(Vector3 worldDirection)
    {
        Vector3 local = transform.InverseTransformDirection(worldDirection);
        local.y = 0f;
        local.Normalize();
        for (int i = 0; i < 4; i++)
            if (Vector3.Dot(local, SideDirections[i]) > 0.95f) return i;
        return -1;
    }
}
