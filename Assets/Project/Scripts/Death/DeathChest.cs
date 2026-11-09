using UnityEngine;

/// <summary>
/// Cofre que queda donde murió el jugador con sus recursos escasos (poco común en adelante).
/// Se recupera interactuando: pasa a la mochila todo lo que entre; lo demás sigue en el cofre.
/// </summary>
public class DeathChest : MonoBehaviour, IInteractable
{
    public const int SlotCount = 12;

    private DeathChestManager manager;

    public ItemContainer Container { get; } = new ItemContainer(SlotCount);

    public void Init(DeathChestManager owner) => manager = owner;

    public string GetPrompt()
    {
        if (Container.TotalCount == 0) return "Cofre vacío";
        var sb = new System.Text.StringBuilder("[E] Recuperar recursos · ");
        bool first = true;
        foreach (ItemStack stack in Container.Slots)
        {
            if (stack.IsEmpty) continue;
            if (!first) sb.Append(", ");
            sb.Append(stack.amount).Append(' ').Append(stack.item.displayName);
            first = false;
        }
        return sb.ToString();
    }

    public void Interact(Inventory inventory)
    {
        int moved = 0;
        for (int i = 0; i < Container.SlotCount; i++)
        {
            ItemStack stack = Container.Slots[i];
            if (stack.IsEmpty) continue;
            ItemDefinition item = stack.item;
            int added = inventory.Add(item, stack.amount);
            if (added <= 0) continue;
            Container.RemoveFromSlot(i, added);
            moved += added;
            NotificationQueue.Enqueue($"+{added} {item.displayName}");
        }

        if (Container.TotalCount == 0)
        {
            NotificationQueue.Enqueue("Recuperaste todo lo del cofre");
            if (manager != null) manager.Remove(this);
            else Destroy(gameObject);
        }
        else if (moved == 0)
        {
            NotificationQueue.Enqueue("Mochila llena: vaciala y volvé por el resto");
        }
    }

    // ---------- Visual (primitivas, sin prefab) ----------

    public void BuildVisual()
    {
        var box = gameObject.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.4f, 0f);
        box.size = new Vector3(1.3f, 0.8f, 0.9f);

        Material body = MakeMaterial(new Color(0.18f, 0.2f, 0.26f), 0f);
        Material glow = MakeMaterial(new Color(1f, 0.5f, 0.12f), 3f);

        Part(PrimitiveType.Cube, "Body", new Vector3(0f, 0.32f, 0f), new Vector3(1.2f, 0.64f, 0.8f), body);
        Part(PrimitiveType.Cube, "Lid", new Vector3(0f, 0.72f, 0f), new Vector3(1.26f, 0.16f, 0.86f), body);
        Part(PrimitiveType.Cube, "Stripe", new Vector3(0f, 0.45f, 0f), new Vector3(1.22f, 0.08f, 0.82f), glow);
        // Haz de luz alto para encontrarlo de lejos. Arranca a 4 m para no tapar la cámara de cerca.
        Part(PrimitiveType.Cylinder, "Beam", new Vector3(0f, 22f, 0f), new Vector3(0.25f, 18f, 0.25f), glow);

        var lightGo = new GameObject("Light");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 2f, 0f);
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.55f, 0.2f);
        light.range = 14f;
        light.intensity = 6f;
    }

    private void Part(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(transform, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static Material MakeMaterial(Color color, float emission)
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
