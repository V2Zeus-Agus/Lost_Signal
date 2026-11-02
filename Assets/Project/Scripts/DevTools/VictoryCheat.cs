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
        if (keyboard == null || !keyboard.f8Key.wasPressedThisFrame) return;

        GiveRepairMaterials();
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
