using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Consola de desarrollador (F8). Un botón por recurso + un campo de cantidad, y algunas
/// opciones de prueba (materiales de la nave, O2, vida, teletransportes).
/// Cada botón arma un ICommand (GiveItemCommand, TeleportCommand...) y lo ejecuta: patrón Command.
/// Solo existe en el editor y en builds de desarrollo.
/// </summary>
public class DevConsole : MonoBehaviour
{
    [SerializeField] private ItemDatabase itemDatabase;
    [SerializeField] private Inventory inventory;
    [SerializeField, Min(1)] private int defaultAmount = 10;

    private const float Width = 820f;
    private const float Pad = 26f;

    private Canvas canvas;
    private TMP_InputField amountField;
    private TMP_Text logText;
    private TMP_Text limitLabel;
    private Health playerHealth;
    private PlayerOxygen playerOxygen;
    private ShipRepair shipRepair;

    public bool IsOpen => canvas != null && canvas.gameObject.activeSelf;

    private void Awake()
    {
        if (!Debug.isDebugBuild)
        {
            Destroy(gameObject);
            return;
        }
        if (inventory == null) inventory = FindFirstObjectByType<Inventory>();
    }

    private void Start()
    {
        if (inventory == null)
        {
            enabled = false;
            return;
        }
        playerHealth = inventory.GetComponent<Health>();
        playerOxygen = inventory.GetComponent<PlayerOxygen>();
        shipRepair = FindFirstObjectByType<ShipRepair>();
        if (GameManager.Instance != null) GameManager.Instance.OnStateChanged += HandleStateChanged;

        RuntimeUI.EnsureEventSystem();
        BuildUI();
        canvas.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnStateChanged -= HandleStateChanged;
        UIModal.Close(this);
    }

    private void HandleStateChanged(GameState state)
    {
        if (state != GameState.Playing) Close();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (IsOpen)
        {
            if (keyboard.f8Key.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame) Close();
        }
        else if (keyboard.f8Key.wasPressedThisFrame && GameManager.Instance != null && GameManager.Instance.IsPlaying)
        {
            Open();
        }
    }

    private void Open()
    {
        canvas.gameObject.SetActive(true);
        UIModal.Open(this);
        RefreshLimitLabel();
    }

    private void Close()
    {
        if (!IsOpen) return;
        RuntimeUI.ClearSelection();
        canvas.gameObject.SetActive(false);
        UIModal.Close(this);
    }

    // ---------- Acciones ----------

    private int Amount
    {
        get
        {
            if (int.TryParse(amountField.text, out int value) && value > 0) return Mathf.Min(value, 999);
            amountField.text = defaultAmount.ToString();
            return defaultAmount;
        }
    }

    private void Give(ItemDefinition item)
    {
        int requested = Amount;
        var command = new GiveItemCommand(inventory, item, requested);
        command.Execute();

        string result = $"+{command.Added} {item.displayName}";
        if (command.Added < requested)
            result += $"  <color=#ff8866>(no entraron {requested - command.Added}: mochila {inventory.TotalItems}/{inventory.MaxItems})</color>";
        Log(result);
    }

    private void Run(ICommand command, string message)
    {
        command.Execute();
        Log(message);
    }

    private void ToggleLimit()
    {
        inventory.UnlimitedOverride = !inventory.UnlimitedOverride;
        RefreshLimitLabel();
        Log(inventory.UnlimitedOverride ? "Mochila sin tope (solo debug)" : $"Tope de mochila restaurado: {inventory.MaxItems} items");
    }

    private void RefreshLimitLabel()
    {
        if (limitLabel != null)
            limitLabel.text = inventory.UnlimitedOverride ? "Tope mochila: OFF" : "Tope mochila: ON";
    }

    // Usa el mismo Interact del cofre, como si el jugador fuera hasta cada uno.
    private void RecoverChests()
    {
        DeathChest[] chests = FindObjectsByType<DeathChest>(FindObjectsSortMode.None);
        foreach (DeathChest chest in chests) chest.Interact(inventory);
        Log(chests.Length == 0 ? "No hay cofres de muerte" : $"Recuperado lo que entró de {chests.Length} cofre(s)");
    }

    // Para probar sin mouse (p. ej. control remoto): mueve la cámara como si fuera el mouse.
    private void LookDelta(Vector2 delta)
    {
        PlayerController controller = inventory.GetComponent<PlayerController>();
        if (controller == null) return;
        float sensitivity = Mathf.Max(controller.LookSensitivity, 0.001f);
        controller.Look(delta * (0.1f / sensitivity));
    }

    private void OpenShop()
    {
        UpgradeShop shop = FindFirstObjectByType<UpgradeShop>();
        if (shop == null) { Log("No hay tienda en esta escena"); return; }
        Close();
        shop.Interact(inventory);
    }

    private void OpenBuildMenu()
    {
        BuildModeController builder = FindFirstObjectByType<BuildModeController>();
        if (builder == null) { Log("No se puede construir en esta escena"); return; }
        Close();
        builder.OpenMenu();
    }

    private void ForceEvent(string id)
    {
        if (EnvironmentEventManager.Instance == null) return;
        EnvironmentEventManager.Instance.Force(id);
        Log("Evento forzado: " + id);
        Close();
    }

    private void EmptyBackpack()
    {
        inventory.Container.Clear();
        Log("Mochila vaciada");
    }

    private void Log(string message)
    {
        logText.text = message;
        Debug.Log("[DevConsole] " + message);
    }

    private List<ItemDefinition> CollectItems()
    {
        var items = new List<ItemDefinition>();
        if (itemDatabase != null)
        {
            foreach (ItemDefinition item in itemDatabase.Items)
                if (item != null && !items.Contains(item)) items.Add(item);
        }
        if (items.Count > 0) return items;

        // Sin base de datos asignada: junta los items que aparecen en la escena.
        if (shipRepair != null)
            foreach (RepairRequirement r in shipRepair.Requirements)
                if (r.resourceType != null && !items.Contains(r.resourceType)) items.Add(r.resourceType);
        foreach (ResourceNode node in FindObjectsByType<ResourceNode>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (node.Item != null && !items.Contains(node.Item)) items.Add(node.Item);
        return items;
    }

    // ---------- UI ----------

    private void BuildUI()
    {
        canvas = RuntimeUI.CreateCanvas("DevConsoleCanvas", transform, 40);
        List<ItemDefinition> items = CollectItems();
        bool hasBiomes = BiomeMap.Instance != null;

        Image window = RuntimeUI.Image("Window", canvas.transform, new Color(0.05f, 0.03f, 0.03f, 0.95f));
        RuntimeUI.Center(window.rectTransform, Width, 600f); // el alto final se ajusta al contenido
        Transform w = window.transform;

        TMP_Text title = RuntimeUI.Text("Title", w, "<b>CONSOLA DE DESARROLLADOR</b>  <size=18><color=#9aa>F8 / Esc para cerrar</color></size>", 30);
        RuntimeUI.Place(title.rectTransform, Pad, 20f, Width - Pad * 2f, 44f);

        // Cantidad
        float y = 80f;
        TMP_Text amountLabel = RuntimeUI.Text("AmountLabel", w, "Cantidad:", 24);
        RuntimeUI.Place(amountLabel.rectTransform, Pad, y + 8f, 120f, 36f);

        GameObject fieldGo = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        fieldGo.transform.SetParent(w, false);
        amountField = fieldGo.GetComponent<TMP_InputField>();
        amountField.contentType = TMP_InputField.ContentType.IntegerNumber;
        amountField.characterLimit = 3;
        amountField.text = defaultAmount.ToString();
        amountField.pointSize = 24;
        RuntimeUI.Place((RectTransform)fieldGo.transform, Pad + 120f, y, 110f, 44f);

        float qx = Pad + 250f;
        foreach (int quick in new[] { 1, 5, 10, 25, 50 })
        {
            int value = quick;
            Button b = RuntimeUI.Button($"Quick{value}", w, value.ToString(), () => amountField.text = value.ToString(), 20);
            RuntimeUI.Place((RectTransform)b.transform, qx, y + 2f, 60f, 40f);
            qx += 68f;
        }

        // Un botón por recurso
        y += 70f;
        TMP_Text giveLabel = RuntimeUI.Text("GiveLabel", w, "<color=#ffb070>Dar recurso</color>", 22);
        RuntimeUI.Place(giveLabel.rectTransform, Pad, y, 300f, 30f);
        y += 34f;
        int count = Mathf.Max(items.Count, 1);
        float gap = 10f;
        float bw = (Width - Pad * 2f - gap * (count - 1)) / count;
        for (int i = 0; i < items.Count; i++)
        {
            ItemDefinition item = items[i];
            Button b = RuntimeUI.Button($"Give_{item.Id}", w, "", () => Give(item), 20);
            RectTransform rt = (RectTransform)b.transform;
            RuntimeUI.Place(rt, Pad + i * (bw + gap), y, bw, 110f);

            Image icon = RuntimeUI.Image("Icon", b.transform, Color.white, false);
            RuntimeUI.SetItemIcon(icon, item);
            RuntimeUI.Place(icon.rectTransform, (bw - 64f) * 0.5f, 8f, 64f, 64f);

            TMP_Text label = b.GetComponentInChildren<TMP_Text>();
            label.text = $"<color=#{ColorUtility.ToHtmlStringRGB(item.uiColor)}>{item.displayName}</color>";
            label.alignment = TextAlignmentOptions.Bottom;
        }

        // Opciones
        y += 130f;
        TMP_Text optLabel = RuntimeUI.Text("OptLabel", w, "<color=#ffb070>Opciones</color>", 22);
        RuntimeUI.Place(optLabel.rectTransform, Pad, y, 300f, 30f);
        y += 34f;
        var options = new List<(string label, UnityEngine.Events.UnityAction action)>
        {
            ("Materiales de la nave", () => Run(new GiveRepairMaterialsCommand(inventory, shipRepair), "Materiales de la nave cargados (lo que entró)")),
            ("Reparar nave (F7)", () => { Close(); VictoryCheat.InstantRepair(); }),
            ("Vaciar mochila", EmptyBackpack),
            ("Rellenar O2", () => { if (playerOxygen != null) playerOxygen.Refill(); Log("Oxígeno al máximo"); }),
            ("Curar", () => { if (playerHealth != null) playerHealth.RestoreFull(); Log("Vida al máximo"); }),
            ("Tope mochila", ToggleLimit),
            ("Recuperar cofres", RecoverChests),
            ("Abrir tienda", OpenShop),
            ("Construir (B)", OpenBuildMenu),
            ("Mirar abajo", () => LookDelta(new Vector2(0f, -150f))),
            ("Mirar arriba", () => LookDelta(new Vector2(0f, 150f))),
            ("Girar 90°", () => LookDelta(new Vector2(900f, 0f))),
            ("Morir (probar cofre)", () => { Close(); if (playerHealth != null) playerHealth.TakeDamage(99999f); }),
        };
        PlaceButtonRow(w, options, ref y);

        if (hasBiomes)
        {
            var teleports = new List<(string, UnityEngine.Events.UnityAction)>
            {
                ("Ir al norte (F9)", () => VictoryCheat.Teleport(new Vector3(0f, 0f, 450f), "Mesetas de Polvo (norte)")),
                ("Ir al sur (F10)", () => VictoryCheat.Teleport(new Vector3(0f, 0f, -450f), "Cuenca Fría (sur)")),
                ("Ir a la base (F11)", () => VictoryCheat.Teleport(new Vector3(5f, 0f, -11.5f), "Base")),
            };
            if (BaseBuildingManager.Instance != null)
                teleports.Add(("Ir a mi base", () =>
                {
                    if (!BaseBuildingManager.Instance.MovePlayerToCheckpoint()) Log("Todavía no tenés una base con aire");
                }));
            if (EnvironmentEventManager.Instance != null)
            {
                teleports.Add(("Tormenta de polvo", () => ForceEvent("dust_storm")));
                teleports.Add(("Ola de frío", () => ForceEvent("cold_wave")));
                teleports.Add(("Lluvia de meteoritos", () => ForceEvent("meteor_shower")));
                teleports.Add(("Terminar evento", () => { EnvironmentEventManager.Instance.Stop(); Log("Evento terminado"); }));
            }
            foreach (SalvageWreck wreck in FindObjectsByType<SalvageWreck>(FindObjectsSortMode.InstanceID))
            {
                SalvageWreck target = wreck;
                Vector3 p = target.transform.position + target.transform.right * 9f;
                teleports.Add(($"Ir a {target.name}", () => VictoryCheat.Teleport(new Vector3(p.x, 0f, p.z), target.name)));
            }
            PlaceButtonRow(w, teleports, ref y);
        }

        logText = RuntimeUI.Text("Log", w, "<color=#9aa>Elegí cantidad y tocá un recurso. Respeta el tope de la mochila salvo que lo apagues.</color>", 20);
        RuntimeUI.Place(logText.rectTransform, Pad, y + 4f, Width - Pad * 2f, 50f);
        window.rectTransform.sizeDelta = new Vector2(Width, y + 70f);
    }

    private void PlaceButtonRow(Transform parent, List<(string label, UnityEngine.Events.UnityAction action)> buttons, ref float y)
    {
        const float gap = 10f;
        const int perRow = 3;
        float bw = (Width - Pad * 2f - gap * (perRow - 1)) / perRow;
        for (int i = 0; i < buttons.Count; i++)
        {
            Button b = RuntimeUI.Button(buttons[i].label, parent, buttons[i].label, buttons[i].action, 20);
            RuntimeUI.Place((RectTransform)b.transform, Pad + (i % perRow) * (bw + gap), y + (i / perRow) * 50f, bw, 42f);
            if (buttons[i].label == "Tope mochila") limitLabel = b.GetComponentInChildren<TMP_Text>();
        }
        y += Mathf.CeilToInt(buttons.Count / (float)perRow) * 50f + 10f;
    }
}
