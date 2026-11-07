using UnityEngine;
using UnityEngine.InputSystem;

public class VictoryCheat : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<VictoryCheat>() != null) return;

        GameObject cheat = new GameObject("VictoryCheat");
        cheat.AddComponent<VictoryCheat>();
        DontDestroyOnLoad(cheat);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.f8Key.wasPressedThisFrame) GiveRepairMaterials();
        if (keyboard.f7Key.wasPressedThisFrame) InstantRepair();

        // Teletransporte para probar biomas (solo si el mapa es grande)
        if (keyboard.f9Key.wasPressedThisFrame) Teleport(new Vector3(0f, 0f, 450f), "Mesetas de Polvo (norte)");
        if (keyboard.f10Key.wasPressedThisFrame) Teleport(new Vector3(0f, 0f, -450f), "Cuenca Fría (sur)");
        if (keyboard.f11Key.wasPressedThisFrame) Teleport(new Vector3(5f, 0f, -11.5f), "Base");
    }

    private void Teleport(Vector3 flat, string label)
    {
        if (BiomeMap.Instance == null) return; // solo en escenas con biomas (Mars)
        Inventory inventory = FindFirstObjectByType<Inventory>();
        if (inventory == null) return;

        Vector3 target = flat + Vector3.up * 2f;
        if (Physics.Raycast(new Vector3(flat.x, 1000f, flat.z), Vector3.down, out RaycastHit hit, 2000f, ~0, QueryTriggerInteraction.Ignore))
            target = hit.point + Vector3.up * 1.2f;

        CharacterController controller = inventory.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        inventory.transform.position = target;
        if (controller != null) controller.enabled = true;
        NotificationQueue.Enqueue($"[DEBUG] Teletransporte: {label}");
    }

    // F7: carga los materiales y los deposita en la nave sin tener que ir hasta ella.
    private void InstantRepair()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;

        ShipRepair shipRepair = FindFirstObjectByType<ShipRepair>();
        Inventory inventory = FindFirstObjectByType<Inventory>();
        if (shipRepair == null || inventory == null || shipRepair.IsRepaired) return;

        GiveRepairMaterials();
        shipRepair.Deposit(inventory);
    }

    private void GiveRepairMaterials()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;

        ShipRepair shipRepair = FindFirstObjectByType<ShipRepair>();
        Inventory inventory = FindFirstObjectByType<Inventory>();
        if (shipRepair == null || inventory == null || shipRepair.IsRepaired) return;

        foreach (RepairRequirement requirement in shipRepair.Requirements)
        {
            int missing = requirement.amount
                          - shipRepair.GetDelivered(requirement.resourceType)
                          - inventory.GetAmount(requirement.resourceType);

            if (missing > 0) inventory.Add(requirement.resourceType, missing);
        }

        NotificationQueue.Enqueue("[DEBUG] Materiales cargados: andá a la nave y presioná E");
    }
}
