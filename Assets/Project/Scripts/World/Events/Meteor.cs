using System.Collections;
using UnityEngine;

/// <summary>
/// Un meteorito: marca roja en el suelo, cae, explota (daño en área si el jugador no está
/// en un refugio) y deja un fragmento con recursos.
/// </summary>
public class Meteor : MonoBehaviour
{
    private const float WarningSeconds = 2.5f;
    private const float FallSeconds = 0.9f;
    private const float DamageRadius = 5f;
    private const float MaxDamage = 35f;
    private const float MinDamage = 10f;

    private static Material rockMaterial, glowMaterial, markerMaterial;

    private Vector3 target;
    private EnvironmentContext context;

    public static void Launch(Vector3 target, EnvironmentContext context)
    {
        var go = new GameObject("Meteor");
        Meteor meteor = go.AddComponent<Meteor>();
        meteor.target = target;
        meteor.context = context;
        meteor.StartCoroutine(meteor.Run());
    }

    private IEnumerator Run()
    {
        EnsureMaterials();

        // 1) Aviso en el suelo
        GameObject marker = Primitive(PrimitiveType.Cylinder, "Marker", target + Vector3.up * 0.05f, new Vector3(DamageRadius * 2f, 0.02f, DamageRadius * 2f), markerMaterial);
        for (float t = 0f; t < WarningSeconds; t += Time.deltaTime)
        {
            float pulse = 0.6f + 0.4f * Mathf.Sin(t * 14f);
            marker.transform.localScale = new Vector3(DamageRadius * 2f * pulse, 0.02f, DamageRadius * 2f * pulse);
            yield return null;
        }

        // 2) Caída
        Vector3 start = target + new Vector3(Random.Range(-40f, 40f), 140f, Random.Range(-40f, 40f));
        GameObject rock = Primitive(PrimitiveType.Sphere, "Rock", start, Vector3.one * 1.6f, glowMaterial);
        Light trail = rock.AddComponent<Light>();
        trail.type = LightType.Point;
        trail.color = new Color(1f, 0.5f, 0.15f);
        trail.range = 25f;
        trail.intensity = 8f;
        for (float t = 0f; t < FallSeconds; t += Time.deltaTime)
        {
            rock.transform.position = Vector3.Lerp(start, target, t / FallSeconds);
            yield return null;
        }
        Destroy(rock);
        Destroy(marker);

        // 3) Impacto
        Impact();
        GameObject flash = Primitive(PrimitiveType.Sphere, "Flash", target, Vector3.one, glowMaterial);
        Light light = flash.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.6f, 0.25f);
        light.range = 30f;
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            float k = t / 0.5f;
            flash.transform.localScale = Vector3.one * Mathf.Lerp(1f, DamageRadius * 2.2f, k);
            light.intensity = Mathf.Lerp(12f, 0f, k);
            yield return null;
        }
        Destroy(flash);

        SpawnFragment();
        Destroy(gameObject);
    }

    private void Impact()
    {
        if (context == null || context.player == null || context.playerHealth == null || context.playerHealth.IsDead) return;
        if (context.PlayerSheltered) return;

        float distance = Vector3.Distance(context.player.position, target);
        if (distance > DamageRadius) return;

        float damage = Mathf.Lerp(MaxDamage, MinDamage, distance / DamageRadius) * SuitProgression.MeteorDamageMultiplier(context.SuitLevel);
        if (damage >= context.playerHealth.Current && GameManager.Instance != null)
            GameManager.Instance.SetDefeatCause("TE CAYÓ UN METEORITO", "Cuando veas las marcas rojas, alejate o metete en un hábitat.");
        context.playerHealth.TakeDamage(damage);
        NotificationQueue.Enqueue($"¡Te golpeó un meteorito! (-{damage:0} de vida)");
    }

    private void SpawnFragment()
    {
        if (context?.meteorLoot == null || context.meteorLoot.Length == 0) return;
        ItemDefinition item = context.meteorLoot[Random.Range(0, context.meteorLoot.Length)];
        if (item == null) return;

        var go = new GameObject("MeteorFragment");
        go.transform.position = target;
        MeteorFragment fragment = go.AddComponent<MeteorFragment>();
        fragment.Init(item, Random.Range(1, 4), rockMaterial, glowMaterial);
    }

    private static GameObject Primitive(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.position = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    private static void EnsureMaterials()
    {
        if (rockMaterial != null) return;
        rockMaterial = RuntimeMaterials.Lit(new Color(0.18f, 0.13f, 0.12f));
        glowMaterial = RuntimeMaterials.Lit(new Color(1f, 0.45f, 0.1f), 4f);
        markerMaterial = RuntimeMaterials.Lit(new Color(1f, 0.1f, 0.05f), 2.5f);
    }
}

/// <summary>Roca de meteorito con unos pocos recursos escasos. Desaparece a los 4 minutos.</summary>
public class MeteorFragment : MonoBehaviour, IInteractable
{
    private ItemDefinition item;
    private int amount;

    public void Init(ItemDefinition loot, int count, Material rock, Material glow)
    {
        item = loot;
        amount = count;

        var box = gameObject.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.5f, 0f);
        box.size = new Vector3(1.6f, 1f, 1.6f);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(transform, false);
        body.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        body.transform.localScale = new Vector3(1.5f, 0.9f, 1.3f);
        body.GetComponent<Renderer>().sharedMaterial = rock;

        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(core.GetComponent<Collider>());
        core.transform.SetParent(transform, false);
        core.transform.localPosition = new Vector3(0f, 0.75f, 0f);
        core.transform.localRotation = Quaternion.Euler(30f, 45f, 20f);
        core.transform.localScale = Vector3.one * 0.45f;
        core.GetComponent<Renderer>().sharedMaterial = glow;

        Destroy(gameObject, 240f);
    }

    public string GetPrompt() => item != null ? $"[E] Romper fragmento de meteorito ({amount} {item.displayName})" : "";

    public void Interact(Inventory inventory)
    {
        if (item == null) return;
        int added = inventory.Add(item, amount);
        if (added <= 0)
        {
            NotificationQueue.Enqueue("Mochila llena");
            return;
        }
        NotificationQueue.Enqueue($"+{added} {item.displayName}");
        amount -= added;
        if (amount <= 0) Destroy(gameObject);
    }
}
