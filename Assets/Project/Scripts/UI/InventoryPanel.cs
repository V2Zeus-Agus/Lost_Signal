using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Inventario interactuable (Tab o I). Muestra los slots de la mochila con el ícono de cada recurso,
/// permite arrastrar para mover/combinar/intercambiar, dividir pilas y descartar.
/// Patrones: Observer (escucha ItemContainer.OnSlotsChanged) y Command (cada acción es un ICommand).
/// La UI se arma por código: no hace falta tocar el Canvas del HUD.
/// </summary>
public class InventoryPanel : MonoBehaviour
{
    [SerializeField] private Inventory inventory;
    [SerializeField, Min(1)] private int columns = 4;

    private const float SlotSize = 100f;
    private const float Gap = 12f;
    private const float Margin = 30f;
    private const float DetailWidth = 220f;

    private readonly List<InventorySlotView> slotViews = new List<InventorySlotView>();
    private Canvas canvas;
    private RectTransform window;
    private TMP_Text titleText;
    private Image capacityFill;
    private TMP_Text capacityText;
    private Image detailIcon;
    private TMP_Text detailText;
    private Button discardOneButton;
    private Button discardAllButton;
    private Button splitButton;
    private Image dragGhost;

    private ItemContainer container;
    private int selected = -1;
    private int dragging = -1;
    private bool dirty = true;

    public bool IsOpen => canvas != null && canvas.gameObject.activeSelf;

    private void Awake()
    {
        if (inventory == null) inventory = FindFirstObjectByType<Inventory>();
    }

    private void Start()
    {
        if (inventory == null)
        {
            Debug.LogWarning("[InventoryPanel] No hay Inventory en la escena.", this);
            enabled = false;
            return;
        }

        container = inventory.Container;
        container.OnSlotsChanged += MarkDirty;
        inventory.OnCapacityChanged += MarkDirty;
        if (GameManager.Instance != null) GameManager.Instance.OnStateChanged += HandleStateChanged;

        RuntimeUI.EnsureEventSystem();
        BuildUI();
        canvas.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (container != null) container.OnSlotsChanged -= MarkDirty;
        if (inventory != null) inventory.OnCapacityChanged -= MarkDirty;
        if (GameManager.Instance != null) GameManager.Instance.OnStateChanged -= HandleStateChanged;
        UIModal.Close(this);
    }

    private void MarkDirty() => dirty = true;

    private void HandleStateChanged(GameState state)
    {
        if (state != GameState.Playing) Close();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        bool toggle = keyboard.tabKey.wasPressedThisFrame || keyboard.iKey.wasPressedThisFrame;
        if (IsOpen)
        {
            // Si otro panel (p. ej. la consola) tiene el teclado, las letras no cierran el inventario.
            if (keyboard.escapeKey.wasPressedThisFrame || (toggle && !UIModal.IsOpenByOther(this))) Close();
        }
        else if (toggle && !UIModal.IsOpen && GameManager.Instance != null && GameManager.Instance.IsPlaying)
        {
            Open();
        }

        if (IsOpen && dirty) Refresh();
    }

    public void Open()
    {
        canvas.gameObject.SetActive(true);
        dirty = true;
        UIModal.Open(this);
        Refresh();
    }

    public void Close()
    {
        if (!IsOpen) return;
        EndDrag();
        RuntimeUI.ClearSelection(); // que Enter/Espacio no "clickeen" un botón con el panel cerrado
        canvas.gameObject.SetActive(false);
        UIModal.Close(this);
    }

    // ---------- Acciones (las llaman los slots y los botones) ----------

    public void Select(int index)
    {
        selected = container.IsValidSlot(index) && !container.Slots[index].IsEmpty ? index : -1;
        dirty = true;
    }

    public void DiscardOne(int index) => new DiscardFromSlotCommand(container, index, 1).Execute();

    private void DiscardSelectedOne()
    {
        if (selected >= 0) DiscardOne(selected);
    }

    private void DiscardSelectedStack()
    {
        if (selected < 0) return;
        new DiscardFromSlotCommand(container, selected, container.Slots[selected].amount).Execute();
    }

    private void SplitSelected()
    {
        if (selected >= 0) new SplitStackCommand(container, selected).Execute();
    }

    public void BeginDrag(int index, Vector2 screenPosition)
    {
        if (!container.IsValidSlot(index) || container.Slots[index].IsEmpty) return;
        dragging = index;
        RuntimeUI.SetItemIcon(dragGhost, container.Slots[index].item);
        dragGhost.color = new Color(dragGhost.color.r, dragGhost.color.g, dragGhost.color.b, 0.85f);
        dragGhost.transform.position = screenPosition;
    }

    public void UpdateDrag(Vector2 screenPosition)
    {
        if (dragging >= 0) dragGhost.transform.position = screenPosition;
    }

    public void EndDrag()
    {
        dragging = -1;
        if (dragGhost != null) dragGhost.enabled = false;
    }

    public void DropOn(int from, int to)
    {
        if (dragging < 0 || from != dragging) return;
        new MoveSlotCommand(container, from, to).Execute();
        selected = container.Slots[to].IsEmpty ? -1 : to;
        dirty = true;
    }

    // ---------- Vista ----------

    private void Refresh()
    {
        dirty = false;
        if (selected >= 0 && container.Slots[selected].IsEmpty) selected = -1;

        for (int i = 0; i < slotViews.Count; i++)
            slotViews[i].Show(i < container.SlotCount ? container.Slots[i] : null, i == selected);

        int total = container.TotalCount;
        int max = container.MaxTotalItems;
        bool full = container.HasTotalLimit && total >= max;
        titleText.text = "<b>MOCHILA</b>";
        capacityText.text = container.HasTotalLimit ? $"{total}/{max} items" : $"{total} items";
        capacityText.color = full ? new Color(1f, 0.4f, 0.35f) : Color.white;
        capacityFill.fillAmount = container.HasTotalLimit && max > 0 ? Mathf.Clamp01((float)total / max) : 0f;
        capacityFill.color = full ? new Color(1f, 0.35f, 0.3f) : RuntimeUI.AccentColor;

        bool hasSelection = selected >= 0;
        discardOneButton.interactable = hasSelection;
        discardAllButton.interactable = hasSelection;
        splitButton.interactable = hasSelection && container.Slots[selected].amount > 1;

        if (!hasSelection)
        {
            RuntimeUI.SetItemIcon(detailIcon, null);
            detailText.text = "<color=#9aa>Elegí un slot para ver el detalle.\n\nArrastrá para mover, combinar o intercambiar pilas.</color>";
            return;
        }

        ItemStack stack = container.Slots[selected];
        RuntimeUI.SetItemIcon(detailIcon, stack.item);
        detailText.text =
            $"<b><color=#{ColorUtility.ToHtmlStringRGB(stack.item.uiColor)}>{stack.item.displayName}</color></b>\n" +
            $"<size=18><color=#9aa>{CategoryName(stack.item.category)}</color></size>\n\n" +
            $"En este slot: {stack.amount}/{stack.item.maxStack}\n" +
            $"Total en mochila: {container.GetAmount(stack.item)}";
    }

    private static string CategoryName(ItemCategory category)
    {
        switch (category)
        {
            case ItemCategory.RawResource: return "Recurso en bruto";
            case ItemCategory.Refined: return "Material refinado";
            case ItemCategory.Component: return "Componente";
            case ItemCategory.Fuel: return "Combustible";
            case ItemCategory.Consumable: return "Consumible";
            default: return "Herramienta";
        }
    }

    private void BuildUI()
    {
        canvas = RuntimeUI.CreateCanvas("InventoryPanelCanvas", transform, 20);

        int slotCount = container.SlotCount;
        int rows = Mathf.CeilToInt(slotCount / (float)columns);
        float gridWidth = columns * SlotSize + (columns - 1) * Gap;
        float gridHeight = rows * SlotSize + (rows - 1) * Gap;
        float width = Margin * 2f + gridWidth + Gap * 2f + DetailWidth;
        float height = Mathf.Max(80f + gridHeight + 110f, 530f);

        // Fondo oscuro detrás de la ventana (bloquea clicks al juego).
        Image dim = RuntimeUI.Image("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.35f));
        RuntimeUI.Stretch(dim.rectTransform);

        Image windowImage = RuntimeUI.Image("Window", canvas.transform, RuntimeUI.PanelColor);
        window = windowImage.rectTransform;
        RuntimeUI.Center(window, width, height);

        titleText = RuntimeUI.Text("Title", window, "MOCHILA", 34);
        RuntimeUI.Place(titleText.rectTransform, Margin, 22f, 300f, 44f);

        capacityText = RuntimeUI.Text("Capacity", window, "", 24, TextAlignmentOptions.Right);
        RuntimeUI.Place(capacityText.rectTransform, Margin + gridWidth - 200f, 28f, 200f, 32f);

        // Slots
        for (int i = 0; i < slotCount; i++)
        {
            int col = i % columns;
            int row = i / columns;
            Image bg = RuntimeUI.Image($"Slot{i}", window, RuntimeUI.SlotColor);
            RuntimeUI.Place(bg.rectTransform, Margin + col * (SlotSize + Gap), 80f + row * (SlotSize + Gap), SlotSize, SlotSize);

            Image frame = RuntimeUI.Image("Selected", bg.transform, new Color(1f, 0.55f, 0.2f, 0.35f), false);
            RuntimeUI.Stretch(frame.rectTransform);
            frame.gameObject.AddComponent<Outline>().effectColor = RuntimeUI.AccentColor;
            frame.enabled = false;

            Image icon = RuntimeUI.Image("Icon", bg.transform, Color.white, false);
            RuntimeUI.Stretch(icon.rectTransform, 12f);

            TMP_Text amount = RuntimeUI.Text("Amount", bg.transform, "", 24, TextAlignmentOptions.BottomRight);
            amount.fontStyle = FontStyles.Bold;
            amount.outlineWidth = 0.25f;
            RuntimeUI.Stretch(amount.rectTransform, 6f);

            InventorySlotView view = bg.gameObject.AddComponent<InventorySlotView>();
            view.Init(this, i, bg, icon, amount, frame);
            slotViews.Add(view);
        }

        // Barra de capacidad
        float barY = 80f + gridHeight + 22f;
        Image barBg = RuntimeUI.Image("CapacityBar", window, new Color(1f, 1f, 1f, 0.1f), false);
        RuntimeUI.Place(barBg.rectTransform, Margin, barY, gridWidth, 14f);
        capacityFill = RuntimeUI.Image("Fill", barBg.transform, RuntimeUI.AccentColor, false);
        RuntimeUI.Stretch(capacityFill.rectTransform);
        capacityFill.sprite = WhiteSprite();
        capacityFill.type = Image.Type.Filled;
        capacityFill.fillMethod = Image.FillMethod.Horizontal;

        TMP_Text hint = RuntimeUI.Text("Hint", window,
            "<color=#9aa>Arrastrar: mover / combinar / intercambiar · Click derecho: descartar 1 · Tab, I o Esc: cerrar</color>", 18);
        RuntimeUI.Place(hint.rectTransform, Margin, barY + 30f, gridWidth, 50f);

        // Detalle
        float dx = Margin + gridWidth + Gap * 2f;
        float detailHeight = height - 80f - Margin;
        Image detailBg = RuntimeUI.Image("Detail", window, new Color(1f, 1f, 1f, 0.04f), false);
        RuntimeUI.Place(detailBg.rectTransform, dx, 80f, DetailWidth, detailHeight);

        detailIcon = RuntimeUI.Image("DetailIcon", detailBg.transform, Color.white, false);
        RuntimeUI.Place(detailIcon.rectTransform, (DetailWidth - 110f) * 0.5f, 12f, 110f, 110f);

        detailText = RuntimeUI.Text("DetailText", detailBg.transform, "", 20);
        RuntimeUI.Place(detailText.rectTransform, 12f, 130f, DetailWidth - 24f, 130f);

        float by = detailHeight - 12f - 3f * 46f + 6f;
        splitButton = RuntimeUI.Button("Split", detailBg.transform, "Dividir pila", SplitSelected, 20);
        RuntimeUI.Place((RectTransform)splitButton.transform, 12f, by, DetailWidth - 24f, 40f);
        discardOneButton = RuntimeUI.Button("DiscardOne", detailBg.transform, "Descartar 1", DiscardSelectedOne, 20);
        RuntimeUI.Place((RectTransform)discardOneButton.transform, 12f, by + 46f, DetailWidth - 24f, 40f);
        discardAllButton = RuntimeUI.Button("DiscardAll", detailBg.transform, "Descartar pila", DiscardSelectedStack, 20);
        RuntimeUI.Place((RectTransform)discardAllButton.transform, 12f, by + 92f, DetailWidth - 24f, 40f);
        discardAllButton.GetComponent<Image>().color = new Color(0.45f, 0.15f, 0.15f, 1f);

        // Ícono que sigue al mouse mientras se arrastra
        dragGhost = RuntimeUI.Image("DragGhost", canvas.transform, Color.white, false);
        dragGhost.rectTransform.sizeDelta = new Vector2(SlotSize * 0.8f, SlotSize * 0.8f);
        dragGhost.enabled = false;
    }

    private static Sprite whiteSprite;

    // Image.Type.Filled necesita un sprite para recortar.
    private static Sprite WhiteSprite()
    {
        if (whiteSprite == null)
            whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        return whiteSprite;
    }
}
