using UnityEngine;

/// <summary>
/// Calcula dónde y cómo quedaría una pieza según hacia dónde mira el jugador,
/// y si se puede construir ahí (suelo, inclinación, espacio libre, adentro de un hábitat...).
/// </summary>
public static class BuildPlacement
{
    private const float SnapSearchRadius = 10f;
    private const float SnapMaxDistance = 4.5f;
    private const float FoundationDepth = 2.4f;

    private static readonly Collider[] overlapBuffer = new Collider[32];

    public static bool TryGetPose(BuildableDefinition definition, Transform cam, Collider ground, float range, int rotationSteps,
        out Vector3 position, out Quaternion rotation, out bool snapped, out string problem)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        snapped = false;
        problem = null;

        if (!Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore))
            return false;

        // Frente de la pieza mirando al jugador.
        float facePlayerYaw = cam.eulerAngles.y + 180f;

        if (definition.kind == BuildableKind.Habitat)
        {
            if (TrySnapHabitat(hit.point, rotationSteps, out position, out rotation))
            {
                snapped = true;
                problem = CheckGroundUnder(ground, position, rotation, true);
            }
            else
            {
                rotation = Quaternion.Euler(0f, Mathf.Round(facePlayerYaw / 15f) * 15f + 90f * rotationSteps, 0f);
                position = hit.point;

                // Si apunta muy cerca, el módulo se corre hacia adelante para no quedar encima del jugador.
                Vector3 flatForward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
                float minDistance = Habitat.Size * 0.75f + 0.8f;
                Vector3 fromPlayer = position - cam.position;
                fromPlayer.y = 0f;
                if (fromPlayer.magnitude < minDistance)
                    position = new Vector3(cam.position.x, position.y, cam.position.z) + flatForward * minDistance;
                if (ground != null && GroundRange(ground, position, rotation, out float min, out float max))
                {
                    position.y = max; // el piso nunca queda enterrado; los cimientos tapan el resto
                    if (max - min > FoundationDepth - 0.2f) problem = "Muy inclinado para un hábitat";
                }
            }
        }
        else
        {
            position = hit.point;
            rotation = Quaternion.Euler(0f, facePlayerYaw + 45f * rotationSteps, 0f);

            // Igual que el hábitat: si apunta a sus pies, la pieza se corre hacia adelante (sobre el suelo).
            Bounds footprint = BuildPieceFactory.Footprint(definition.kind);
            float minDistance = Mathf.Max(footprint.extents.x, footprint.extents.z) + 0.9f;
            Vector3 flat = position - cam.position;
            flat.y = 0f;
            if (flat.magnitude < minDistance && !definition.mustBeInsideHabitat)
            {
                Vector3 forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
                Vector3 pushed = new Vector3(cam.position.x, 0f, cam.position.z) + forward * minDistance;
                if (Physics.Raycast(pushed + Vector3.up * (cam.position.y + 20f), Vector3.down, out RaycastHit ground2, 200f, ~0, QueryTriggerInteraction.Ignore))
                {
                    position = ground2.point;
                    hit = ground2;
                }
            }
            if (hit.normal.y < 0.8f) problem = "Muy inclinado";
            if (definition.mustBeInsideHabitat && Habitat.FindContaining(position + Vector3.up * 0.2f) == null)
                problem = "Tiene que ir adentro de un hábitat";
        }

        if (problem == null && IsBlocked(definition.kind, position, rotation, ground))
            problem = "Lugar ocupado";

        return true;
    }

    /// <summary>Busca el lado libre de un hábitat cercano más próximo a donde apunta el jugador.</summary>
    private static bool TrySnapHabitat(Vector3 aim, int rotationSteps, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        float best = SnapMaxDistance;
        bool found = false;

        foreach (Habitat h in Habitat.All)
        {
            if (h == null || FlatDistance(h.transform.position, aim) > SnapSearchRadius) continue;
            for (int side = 0; side < 4; side++)
            {
                Vector3 candidate = h.transform.position + h.transform.rotation * Habitat.SideDirections[side] * Habitat.Size;
                if (IsHabitatAt(candidate)) continue;
                float d = FlatDistance(candidate, aim);
                if (d >= best) continue;
                best = d;
                position = candidate;
                rotation = h.transform.rotation * Quaternion.Euler(0f, 90f * rotationSteps, 0f);
                found = true;
            }
        }
        return found;
    }

    private static bool IsHabitatAt(Vector3 position)
    {
        foreach (Habitat h in Habitat.All)
            if (h != null && FlatDistance(h.transform.position, position) < 1f) return true;
        return false;
    }

    private static string CheckGroundUnder(Collider ground, Vector3 position, Quaternion rotation, bool fixedHeight)
    {
        if (ground == null || !GroundRange(ground, position, rotation, out float min, out float max)) return null;
        if (max > position.y + 0.25f) return "El terreno tapa el piso: probá otro lado";
        if (min < position.y - FoundationDepth) return "Queda colgando: el terreno baja mucho";
        return null;
    }

    /// <summary>Alturas mínima y máxima del suelo bajo las esquinas y el centro del hábitat.</summary>
    private static bool GroundRange(Collider ground, Vector3 position, Quaternion rotation, out float min, out float max)
    {
        min = float.MaxValue;
        max = float.MinValue;
        float h = Habitat.Size * 0.5f - 0.2f;
        Vector3[] points =
        {
            Vector3.zero, new Vector3(h, 0f, h), new Vector3(-h, 0f, h), new Vector3(h, 0f, -h), new Vector3(-h, 0f, -h)
        };
        bool any = false;
        foreach (Vector3 local in points)
        {
            Vector3 p = position + rotation * local;
            if (!ground.Raycast(new Ray(new Vector3(p.x, position.y + 200f, p.z), Vector3.down), out RaycastHit hit, 600f)) continue;
            min = Mathf.Min(min, hit.point.y);
            max = Mathf.Max(max, hit.point.y);
            any = true;
        }
        return any;
    }

    private static bool IsBlocked(BuildableKind kind, Vector3 position, Quaternion rotation, Collider ground)
    {
        Bounds footprint = BuildPieceFactory.Footprint(kind);
        Vector3 center = position + rotation * footprint.center;
        int count = Physics.OverlapBoxNonAlloc(center, footprint.extents * 0.96f, overlapBuffer, rotation, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider col = overlapBuffer[i];
            if (col == null || col == ground) continue;
            return true;
        }
        return false;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
