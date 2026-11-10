using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Menú de construcción (B): una tarjeta por pieza con su costo, y la opción Desarmar.</summary>
public class BuildMenuPanel : MonoBehaviour
{
    private const float Width = 980f;
    private const float CardWidth = 455f;
    private const float CardHeight = 150f;
    private const float Pad = 24f;

    private static BuildMenuPanel instance;

    private class Card
    {
        public BuildableDefinition definition;
        public RectTransform costRow;
        public TMP_Text buttonLabel;
        public Button button;
        public TMP_Text description;
    }

    private readonly List<Card> cards = new List<Card>();
    private Canvas canvas;
    private BuildCatalog builtFor;
    private Inventory inventory;
    private BaseBuildingManager manager;
    private Action<BuildableDefinition> onSelect;
    private Action onRemove;
    private Action onClose;
    private int openedFrame;
    private bool dirty;

    private static bool IsOpen => instance != null && instance.canvas != null && instance.canvas.gameObject.activeSelf;

    public static void Show(BaseBuildingManager manager, Inventory inventory, Action<BuildableDefinition> onSelect, Action onRemove, Action onClose)
    {
        if (manager == null || manager.Catalog == null) return;
        if (instance == null)
        {
            instance = new GameObject("BuildMenuPanel").AddComponent<BuildMenuPanel>();
            RuntimeUI.EnsureEventSystem();
        }
        instance.manager = manager;
        instance.Open(manager.Catalog, inventory, onSelect, onRemove, onClose);
    }

    /// <summary>Cierra sin avisar (lo usa el controlador al cambiar de estado).</summary>
    public static void HideSilently()
    {
        if (instance != null) instance.Hide();
    }

    private void Open(BuildCatalog catalog, Inventory inv, Action<BuildableDefinition> select, Action remove, Action close)
    {
        Hide();
        inventory = inv;
        onSelect = select;
        onRemove = remove;
        onClose = close;
        Build(catalog);
        canvas.gameObject.SetActive(true);
        openedFrame = Time.frameCount;
        UIModal.Open(this);
        if (inventory != null) inventory.Container.OnSlotsChanged += MarkDirty;
        if (GameManager.Instance != null) GameManager.Instance.OnStateChanged += HandleStateChanged;
        Refresh();
    }

    private void Hide()
    {
        if (!IsOpen) return;
        RuntimeUI.ClearSelection();
        canvas.gameObject.SetActive(false);
        UIModal.Close(this);
        if (inventory != null) inventory.Container.OnSlotsChanged -= MarkDirty;
        if (GameManager.Instance != null) GameManager.Instance.OnStateChanged -= HandleStateChanged;
    }

    private void CloseAndNotify()
    {
        Hide();
        onClose?.Invoke();
    }

    private void OnDestroy()
    {
        Hide();
        if (instance == this) instance = null;
    }

    private void MarkDirty() => dirty = true;

    private void HandleStateChanged(GameState state)
    {
        if (state != GameState.Playing) CloseAndNotify();
    }

    private void Update()
    {
        if (!IsOpen) return;
        Keyboard k = Keyboard.current;
        if (k != null && Time.frameCount != openedFrame && (k.escapeKey.wasPressedThisFrame || k.bKey.wasPressedThisFrame))
        {
            UIModal.ConsumeEscape();
            CloseAndNotify();
            return;
        }
        if (dirty) Refresh();
    }

    private void Refresh()
    {
        dirty = false;
        foreach (Card card in cards)
        {
            CostChips.Fill(card.costRow, card.definition.cost, inventory, 28f, 18f);
            bool unlocked = manager == null || manager.IsUnlocked(card.definition);
            bool affordable = CostText.CanAfford(inventory, card.definition.cost);
            card.button.interactable = unlocked;
            card.buttonLabel.text = !unlocked ? "Bloqueado" : affordable ? "Construir" : "Faltan recursos";
            card.description.text = unlocked
                ? $"<color=#aab>{card.definition.description}</color>"
                : "<color=#ffb070>Se desbloquea con una base con aire: hábitat + generador de O2.</color>";
        }
    }

    private void Build(BuildCatalog catalog)
    {
        if (canvas != null && builtFor == catalog) return;
        if (canvas != null) Destroy(canvas.gameObject);
        cards.Clear();
        builtFor = catalog;

        canvas = RuntimeUI.CreateCanvas("BuildMenuCanvas", transform, 30);
        Image dim = RuntimeUI.Image("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.4f));
        RuntimeUI.Stretch(dim.rectTransform);

        int count = catalog.buildables.Count + 1; // + Desarmar
        int rows = Mathf.CeilToInt(count / 2f);
        float height = 86f + rows * (CardHeight + 12f) + 40f;
        Image window = RuntimeUI.Image("Window", canvas.transform, RuntimeUI.PanelColor);
        RuntimeUI.Center(window.rectTransform, Width, height);

        TMP_Text title = RuntimeUI.Text("Title", window.transform, "<b>CONSTRUIR</b>", 34);
        RuntimeUI.Place(title.rectTransform, Pad, 20f, 400f, 44f);
        TMP_Text hint = RuntimeUI.Text("Hint", window.transform, "<color=#9aa>B o Esc para cerrar</color>", 18, TextAlignmentOptions.Right);
        RuntimeUI.Place(hint.rectTransform, Width - Pad - 300f, 30f, 300f, 30f);

        int index = 0;
        foreach (BuildableDefinition definition in catalog.buildables)
        {
            if (definition == null) continue;
            var card = new Card { definition = definition };
            Transform t = CardRoot(window.transform, index++);

            TMP_Text name = RuntimeUI.Text("Name", t, $"<b>{definition.displayName}</b>", 24);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.enableAutoSizing = true; // "Fábrica de Combustible" no entra a 24
            name.fontSizeMin = 16f;
            name.fontSizeMax = 24f;
            RuntimeUI.Place(name.rectTransform, 14f, 12f, CardWidth - 200f, 30f);
            TMP_Text desc = RuntimeUI.Text("Desc", t, $"<color=#aab>{definition.description}</color>", 16);
            RuntimeUI.Place(desc.rectTransform, 14f, 52f, CardWidth - 28f, 52f);
            card.description = desc;
            card.costRow = RuntimeUI.Rect("Cost", t);
            RuntimeUI.Place(card.costRow, 14f, 112f, CardWidth - 28f, 30f);

            BuildableDefinition captured = definition;
            Button button = RuntimeUI.Button("Build", t, "Construir", () => Select(captured), 18);
            RuntimeUI.Place((RectTransform)button.transform, CardWidth - 180f, 10f, 166f, 38f);
            button.GetComponent<Image>().color = new Color(0.85f, 0.45f, 0.15f, 1f);
            card.buttonLabel = button.GetComponentInChildren<TMP_Text>();
            card.button = button;
            cards.Add(card);
        }

        Transform removeCard = CardRoot(window.transform, index);
        TMP_Text removeName = RuntimeUI.Text("Name", removeCard, "<b>Desarmar</b>", 24);
        RuntimeUI.Place(removeName.rectTransform, 14f, 12f, CardWidth - 200f, 30f);
        TMP_Text removeDesc = RuntimeUI.Text("Desc", removeCard, "<color=#aab>Apuntá a algo construido y hacé clic. Devuelve todo lo que costó.</color>", 16);
        RuntimeUI.Place(removeDesc.rectTransform, 14f, 52f, CardWidth - 28f, 52f);
        Button removeButton = RuntimeUI.Button("Remove", removeCard, "Desarmar", Remove, 18);
        RuntimeUI.Place((RectTransform)removeButton.transform, CardWidth - 180f, 10f, 166f, 38f);
        removeButton.GetComponent<Image>().color = new Color(0.45f, 0.15f, 0.15f, 1f);
    }

    private static Transform CardRoot(Transform window, int index)
    {
        Image bg = RuntimeUI.Image("Card" + index, window, new Color(1f, 1f, 1f, 0.05f), false);
        RuntimeUI.Place(bg.rectTransform, Pad + (index % 2) * (CardWidth + 22f), 80f + (index / 2) * (CardHeight + 12f), CardWidth, CardHeight);
        return bg.transform;
    }

    private void Select(BuildableDefinition definition)
    {
        Hide();
        onSelect?.Invoke(definition);
    }

    private void Remove()
    {
        Hide();
        onRemove?.Invoke();
    }
}
