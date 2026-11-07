using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Atajos de prueba. Solo existen en el editor y en builds de desarrollo (Debug.isDebugBuild):
/// en una build final para Steam no se crean.
///  F7  reparar la nave al instante
///  F8  consola de desarrollador (ver DevConsole)
///  F9/F10/F11  teletransporte norte / sur / base (solo en Mars)
/// </summary>
public class VictoryCheat : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!Debug.isDebugBuild) return;
        if (FindFirstObjectByType<VictoryCheat>() != null) return;

        GameObject cheat = new GameObject("VictoryCheat");
        cheat.AddComponent<VictoryCheat>();
        DontDestroyOnLoad(cheat);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || UIModal.IsOpen) return;

        if (keyboard.f7Key.wasPressedThisFrame) InstantRepair();

        if (keyboard.f9Key.wasPressedThisFrame) Teleport(new Vector3(0f, 0f, 450f), "Mesetas de Polvo (norte)");
        if (keyboard.f10Key.wasPressedThisFrame) Teleport(new Vector3(0f, 0f, -450f), "Cuenca Fría (sur)");
        if (keyboard.f11Key.wasPressedThisFrame) Teleport(new Vector3(5f, 0f, -11.5f), "Base");
    }

    public static void Teleport(Vector3 flat, string label)
    {
        if (BiomeMap.Instance == null) return; // solo en escenas con biomas (Mars)
        Inventory inventory = FindFirstObjectByType<Inventory>();
        if (inventory == null) return;

        new TeleportCommand(inventory.transform, flat).Execute();
        NotificationQueue.Enqueue($"[DEBUG] Teletransporte: {label}");
    }

    // F7: carga los materiales y los deposita en la nave sin tener que ir hasta ella.
    public static void InstantRepair()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;

        ShipRepair shipRepair = FindFirstObjectByType<ShipRepair>();
        Inventory inventory = FindFirstObjectByType<Inventory>();
        if (shipRepair == null || inventory == null || shipRepair.IsRepaired) return;

        // La mochila tiene tope: se carga y deposita de a tandas hasta completar.
        for (int i = 0; i < 50 && !shipRepair.IsRepaired; i++)
        {
            float before = shipRepair.Progress;
            new GiveRepairMaterialsCommand(inventory, shipRepair).Execute();
            shipRepair.Deposit(inventory);
            if (Mathf.Approximately(before, shipRepair.Progress)) break;
        }
        NotificationQueue.Clear(); // evita 15 carteles seguidos de "Nave reparada al X%"
        NotificationQueue.Enqueue(shipRepair.IsRepaired ? "[DEBUG] Nave reparada" : "[DEBUG] No se pudo completar: vaciá la mochila");
    }
}
