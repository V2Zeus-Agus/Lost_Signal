using UnityEngine;

/// <summary>
/// Dónde va una puerta: siempre en la pared de un hábitat, centrada y mirando hacia afuera.
/// La usan el modo construcción (para encajarla) y el manager (para saber qué pared cambiar).
/// </summary>
public static class DoorPlacement
{
    private const float MaxAimDistance = 4f;

    /// <summary>Pose de la puerta en la pared libre más cercana a donde apunta el jugador.</summary>
    public static bool TrySnap(Vector3 aim, out Vector3 position, out Quaternion rotation, out string problem)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        problem = "Apuntá a una pared de un hábitat";
        float best = MaxAimDistance;
        bool found = false;

        foreach (Habitat h in Habitat.All)
        {
            if (h == null) continue;
            for (int side = 0; side < 4; side++)
            {
                Vector3 center = WallCenter(h, side);
                float d = Vector2.Distance(new Vector2(center.x, center.z), new Vector2(aim.x, aim.z));
                if (d >= best) continue;
                best = d;
                found = true;
                position = center;
                rotation = Quaternion.LookRotation(h.transform.rotation * Habitat.SideDirections[side]);
                problem = h.IsSideOpen(side) ? "Esa pared está unida a otro módulo"
                        : h.HasDoor(side) ? "Esa pared ya tiene puerta"
                        : null;
            }
        }
        return found;
    }

    /// <summary>¿La puerta en esa pose corresponde a un lado de este hábitat?</summary>
    public static bool TryGetSide(Habitat habitat, Vector3 position, Vector3 forward, out int side)
    {
        side = habitat.SideFacing(forward);
        if (side < 0) return false;
        Vector3 center = WallCenter(habitat, side);
        return Vector2.Distance(new Vector2(center.x, center.z), new Vector2(position.x, position.z)) < 0.6f;
    }

    /// <summary>Centro de la pared, a la altura del piso del hábitat (base del hábitat).</summary>
    private static Vector3 WallCenter(Habitat h, int side) =>
        h.transform.position + h.transform.rotation * Habitat.SideDirections[side] * (Habitat.Size * 0.5f);
}
