using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Panel de OBJETIVOS (arriba a la derecha): reparar/despegar la nave con sus materiales,
/// construir la base, la primera fábrica y mejorar el equipo. Cada objetivo es un IQuest.
/// Se refresca cada medio segundo o cuando cambia la mochila.
/// </summary>
public class QuestHUD : MonoBehaviour
{
    private class Block
    {
        public IQuest quest;
        public RectTransform root;
        public TMP_Text title;
        public TMP_Text hint;
        public HUDBar bar;
        public HUDLineList lines;
    }

    private const string CollapsedPrefKey = "LostSignal.QuestsCollapsed";

    /// <summary>Objetivos ocultos con [J] (también los del tutorial). Se recuerda entre partidas.</summary>
    // Se lee la primera vez que se usa: PlayerPrefs no se puede leer en inicializadores estáticos.
    private static int collapsedState = -1;
    public static bool Collapsed
    {
        get
        {
            if (collapsedState < 0) collapsedState = PlayerPrefs.GetInt(CollapsedPrefKey, 0);
            return collapsedState == 1;
        }
    }
    public static event System.Action OnCollapsedChanged;

    public static void ToggleCollapsed()
    {
        collapsedState = Collapsed ? 0 : 1;
        PlayerPrefs.SetInt(CollapsedPrefKey, collapsedState);
        OnCollapsedChanged?.Invoke();
    }

    private readonly List<Block> blocks = new List<Block>();
    private TMP_Text toggleHint;
    private readonly List<QuestLine> buffer = new List<QuestLine>();
    private RectTransform panel;
    private Inventory inventory;
    private ShipRepair ship;
    private float timer;
    private bool dirty = true;

    public void Init(ShipRepair ship, Inventory playerInventory, string shipTitle)
    {
        inventory = playerInventory;
        panel = HUDTheme.Panel("Quests", "Objetivos", HUDTheme.Accent);
        HUDRoot.Instance.Add(HUDRoot.Instance.Right, panel, 0);
        // "[J] ocultar" a la derecha del título del panel.
        toggleHint = RuntimeUI.Text("ToggleHint", panel.GetChild(0), "", 14, TextAlignmentOptions.TopRight);
        toggleHint.color = HUDTheme.Muted;
        toggleHint.textWrappingMode = TextWrappingModes.NoWrap;
        RuntimeUI.Stretch(toggleHint.rectTransform);
        OnCollapsedChanged += MarkDirty;

        this.ship = ship;
        if (inventory != null) inventory.OnResourceChanged += HandleInventoryChanged;
        if (ship != null) ship.OnProgressChanged += HandleProgress;

        AddQuest(new RepairShipQuest(ship, inventory, shipTitle), true);
        // En el tutorial la guía ya lista los pasos: acá solo queda la nave (lo del tuto).
        if (FindFirstObjectByType<TutorialGuide>() != null) return;
        AddQuest(new BuildBaseQuest(), false);
        AddQuest(new BuildFactoryQuest(), false);
        AddQuest(new UpgradeGearQuest(FindFirstObjectByType<UpgradeShop>(), inventory), false);

    }

    private void OnDestroy()
    {
        if (inventory != null) inventory.OnResourceChanged -= HandleInventoryChanged;
        if (ship != null) ship.OnProgressChanged -= HandleProgress;
        OnCollapsedChanged -= MarkDirty;
    }

    private void MarkDirty() => dirty = true;

    private void HandleInventoryChanged(ItemDefinition item, int amount) => dirty = true;
    private void HandleProgress(float progress) => dirty = true;

    private void AddQuest(IQuest quest, bool withBar)
    {
        var block = new Block { quest = quest };
        block.root = HUDTheme.Group(panel, 3f);
        block.title = HUDTheme.Label(block.root, "", 20, HUDTheme.Text);
        block.title.fontStyle = FontStyles.Bold;
        block.hint = HUDTheme.Label(block.root, "", 15, HUDTheme.Muted);
        if (withBar) block.bar = new HUDBar(block.root, 8f, HUDTheme.Accent);
        block.lines = new HUDLineList(block.root, 24f, 17f);
        blocks.Add(block);
    }

    private void Update()
    {
        if (panel == null) return;
        Keyboard keyboard = Keyboard.current;
        bool playing = GameManager.Instance != null && GameManager.Instance.IsPlaying;
        if (playing && !UIModal.IsOpen && keyboard != null && keyboard.jKey.wasPressedThisFrame) ToggleCollapsed();
        timer += Time.deltaTime;
        if (!dirty && timer < 0.5f) return;
        timer = 0f;
        dirty = false;
        Redraw();
    }

    private void Redraw()
    {
        toggleHint.text = Collapsed ? $"<color={HUDTheme.Hex(HUDTheme.Accent)}>[J]</color> mostrar" : "[J] ocultar";
        bool first = true;
        foreach (Block block in blocks)
        {
            IQuest quest = block.quest;
            bool visible = quest.IsVisible && !Collapsed;
            if (block.root.gameObject.activeSelf != visible) block.root.gameObject.SetActive(visible);
            if (!visible) continue;

            string marker = quest.IsComplete ? HUDTheme.Hex(HUDTheme.Good) : HUDTheme.Hex(first ? HUDTheme.Accent : HUDTheme.Teal);
            block.title.text = $"<color={marker}>■</color> {quest.Title}";
            block.title.fontSize = first ? 21 : 18;
            block.hint.text = quest.Hint;

            if (block.bar != null)
            {
                float p = Mathf.Max(quest.Progress, 0f);
                block.bar.Set(p, quest.IsComplete ? HUDTheme.Good : HUDTheme.Accent);
            }

            buffer.Clear();
            quest.FillLines(buffer);
            for (int i = 0; i < buffer.Count; i++)
            {
                QuestLine line = buffer[i];
                HUDLine view = block.lines.Get(i);
                if (line.isTask) view.SetCheck(line.done);
                else view.SetItem(line.item);
                view.Left.text = line.label;
                view.Left.color = line.done ? HUDTheme.Muted : HUDTheme.Text;
                view.Right.text = line.value ?? "";
            }
            block.lines.Trim(buffer.Count);
            first = false;
        }
    }
}
