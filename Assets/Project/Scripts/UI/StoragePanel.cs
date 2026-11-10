using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Pantalla de un cofre: mochila a la izquierda, cofre a la derecha. Clic en una pila la pasa
/// al otro lado (lo que entre). Usa TransferStackCommand (Command) y escucha los cambios (Observer).
/// </summary>
public class StoragePanel : MonoBehaviour
{
    private const float Slot = 84f;
    private const float Gap = 10f;
    private const float Pad = 26f;

    private static StoragePanel instance;

    private class SlotView
    {
        public Image bg;
        public Image icon;
        public TMP_Text amount;
    }

    private readonly List<SlotView> backpackSlots = new List<SlotView>();
    private readonly List<SlotView> storageSlots = new List<SlotView>();
    private Canvas canvas;
    private TMP_Text backpackTitle;
    private TMP_Text storageTitle;
    private Inventory inventory;
    private StorageContainer storage;
    private int openedFrame;
    private bool dirty;

    private bool IsOpen => canvas != null && canvas.gameObject.activeSelf;

    public static void Show(StorageContainer storage, Inventory inventory)
    {
        if (storage == null || inventory == null) return;
        if (instance == null)
        {
            instance = new GameObject("StoragePanel").AddComponent<StoragePanel>();
            RuntimeUI.EnsureEventSystem();
        }
        instance.Open(storage, inventory);
    }

    private void Open(StorageContainer newStorage, Inventory newInventory)
    {
        Unsubscribe();
        storage = newStorage;
        inventory = newInventory;
        if (canvas == null) Build();
        inventory.Container.OnSlotsChanged += MarkDirty;
        storage.Container.OnSlotsChanged += MarkDirty;
        if (GameManager.Instance != null) GameManager.Instance.OnStateChanged += HandleStateChanged;
        canvas.gameObject.SetActive(true);
        openedFrame = Time.frameCount;
        UIModal.Open(this);
        Refresh();
    }

    private void Close()
    {
        if (!IsOpen) return;
        RuntimeUI.ClearSelection();
        canvas.gameObject.SetActive(false);
        UIModal.Close(this);
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (inventory != null) inventory.Container.OnSlotsChanged -= MarkDirty;
        if (storage != null) storage.Container.OnSlotsChanged -= MarkDirty;
        if (GameManager.Instance != null) GameManager.Instance.OnStateChanged -= HandleStateChanged;
    }

    private void OnDestroy()
    {
        Unsubscribe();
        UIModal.Close(this);
        if (instance == this) instance = null;
    }

    private void MarkDirty() => dirty = true;

    private void HandleStateChanged(GameState state)
    {
        if (state != GameState.Playing) Close();
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (storage == null) { Close(); return; }
        Keyboard k = Keyboard.current;
        if (k != null && Time.frameCount != openedFrame && (k.escapeKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame || k.tabKey.wasPressedThisFrame))
        {
            Close();
            return;
        }
        if (dirty) Refresh();
    }

    private void Transfer(bool fromBackpack, int slot)
    {
        ItemContainer from = fromBackpack ? inventory.Container : storage.Container;
        ItemContainer to = fromBackpack ? storage.Container : inventory.Container;
        new TransferStackCommand(from, to, slot).Execute();
        Refresh();
    }

    private void Refresh()
    {
        dirty = false;
        Fill(backpackSlots, inventory.Container);
        Fill(storageSlots, storage.Container);
        backpackTitle.text = $"<b>MOCHILA</b>  <size=20><color=#9aa>{inventory.TotalItems}/{inventory.MaxItems} items</color></size>";
        storageTitle.text = $"<b>COFRE</b>  <size=20><color=#9aa>{storage.Container.TotalCount} items</color></size>";
    }

    private static void Fill(List<SlotView> views, ItemContainer container)
    {
        for (int i = 0; i < views.Count; i++)
        {
            ItemStack stack = i < container.SlotCount ? container.Slots[i] : null;
            bool empty = stack == null || stack.IsEmpty;
            RuntimeUI.SetItemIcon(views[i].icon, empty ? null : stack.item);
            views[i].amount.text = empty ? "" : stack.amount.ToString();
        }
    }

    private void Build()
    {
        canvas = RuntimeUI.CreateCanvas("StorageCanvas", transform, 30);
        Image dim = RuntimeUI.Image("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.4f));
        RuntimeUI.Stretch(dim.rectTransform);

        const int backCols = 4, storeCols = 5;
        float backWidth = backCols * Slot + (backCols - 1) * Gap;
        float storeWidth = storeCols * Slot + (storeCols - 1) * Gap;
        float width = Pad * 3f + backWidth + storeWidth;
        float height = 80f + 4 * Slot + 3 * Gap + 70f;

        Image window = RuntimeUI.Image("Window", canvas.transform, RuntimeUI.PanelColor);
        RuntimeUI.Center(window.rectTransform, width, height);

        backpackTitle = RuntimeUI.Text("BackpackTitle", window.transform, "", 28);
        RuntimeUI.Place(backpackTitle.rectTransform, Pad, 20f, backWidth, 40f);
        storageTitle = RuntimeUI.Text("StorageTitle", window.transform, "", 28);
        RuntimeUI.Place(storageTitle.rectTransform, Pad * 2f + backWidth, 20f, storeWidth, 40f);

        BuildGrid(window.transform, backpackSlots, inventory.Container.SlotCount, backCols, Pad, true);
        BuildGrid(window.transform, storageSlots, StorageContainer.SlotCount, storeCols, Pad * 2f + backWidth, false);

        TMP_Text hint = RuntimeUI.Text("Hint", window.transform,
            "<color=#9aa>Clic en una pila para pasarla al otro lado · Esc, E o Tab para cerrar</color>", 18);
        RuntimeUI.Place(hint.rectTransform, Pad, height - 46f, width - Pad * 2f, 30f);
    }

    private void BuildGrid(Transform parent, List<SlotView> views, int count, int cols, float x0, bool backpack)
    {
        for (int i = 0; i < count; i++)
        {
            var view = new SlotView();
            view.bg = RuntimeUI.Image("Slot" + i, parent, RuntimeUI.SlotColor);
            RuntimeUI.Place(view.bg.rectTransform, x0 + (i % cols) * (Slot + Gap), 72f + (i / cols) * (Slot + Gap), Slot, Slot);
            Button button = view.bg.gameObject.AddComponent<Button>();
            int index = i;
            button.onClick.AddListener(() => Transfer(backpack, index));

            view.icon = RuntimeUI.Image("Icon", view.bg.transform, Color.white, false);
            RuntimeUI.Stretch(view.icon.rectTransform, 10f);
            view.amount = RuntimeUI.Text("Amount", view.bg.transform, "", 22, TextAlignmentOptions.BottomRight);
            view.amount.fontStyle = FontStyles.Bold;
            RuntimeUI.Stretch(view.amount.rectTransform, 5f);
            views.Add(view);
        }
    }
}
