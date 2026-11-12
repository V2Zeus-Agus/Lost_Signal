using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Pantalla de la tienda de mejoras: una tarjeta por mejora con nivel, efecto actual,
/// próximo nivel, costo (tengo/necesito) y botón de compra. Observer: se refresca sola
/// cuando cambia la mochila o una mejora.
/// </summary>
public class UpgradeShopPanel : MonoBehaviour
{
    private const float Width = 900f;
    private const float CardHeight = 128f;
    private const float Pad = 24f;

    private static UpgradeShopPanel instance;

    private class Card
    {
        public IUpgradeOffer offer;
        public TMP_Text title;
        public TMP_Text current;
        public TMP_Text next;
        public RectTransform costRow;
        public Button buy;
        public TMP_Text buyLabel;
        public Button pin;
        public TMP_Text pinLabel;
    }

    private readonly List<Card> cards = new List<Card>();
    private Canvas canvas;
    private RectTransform window;
    private UpgradeShop shop;
    private UpgradeShop builtFor;
    private Inventory inventory;
    private int openedFrame;
    private bool dirty;

    public static bool IsOpen => instance != null && instance.canvas != null && instance.canvas.gameObject.activeSelf;

    public static void Show(UpgradeShop shop, Inventory inventory)
    {
        if (shop == null || inventory == null) return;
        if (instance == null)
        {
            instance = new GameObject("UpgradeShopPanel").AddComponent<UpgradeShopPanel>();
            RuntimeUI.EnsureEventSystem();
        }
        instance.Open(shop, inventory);
    }

    private void OnDestroy()
    {
        Unsubscribe();
        UIModal.Close(this);
        if (instance == this) instance = null;
    }

    private void Open(UpgradeShop newShop, Inventory newInventory)
    {
        Unsubscribe();
        shop = newShop;
        inventory = newInventory;
        inventory.Container.OnSlotsChanged += MarkDirty;
        if (shop.Upgrades != null) shop.Upgrades.OnUpgradeChanged += HandleUpgradeChanged;
        if (GameManager.Instance != null) GameManager.Instance.OnStateChanged += HandleStateChanged;
        PinnedUpgrades.OnChanged += MarkDirty;

        Build();
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
        if (shop != null && shop.Upgrades != null) shop.Upgrades.OnUpgradeChanged -= HandleUpgradeChanged;
        if (GameManager.Instance != null) GameManager.Instance.OnStateChanged -= HandleStateChanged;
        PinnedUpgrades.OnChanged -= MarkDirty;
    }

    private void MarkDirty() => dirty = true;
    private void HandleUpgradeChanged(string id, int level) => dirty = true;

    private void HandleStateChanged(GameState state)
    {
        if (state != GameState.Playing) Close();
    }

    private void Update()
    {
        if (!IsOpen) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && Time.frameCount != openedFrame &&
            (keyboard.escapeKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame))
        {
            Close();
            return;
        }
        if (dirty) Refresh();
    }

    private void Buy(Card card)
    {
        shop.Buy(card.offer, inventory);
        Refresh();
    }

    private void TogglePin(Card card) =>
        PinnedUpgrades.Toggle(PinnedUpgrades.UpgradeKey(card.offer.UpgradeId), card.offer.Title);

    // ---------- Vista ----------

    private void Refresh()
    {
        dirty = false;
        foreach (Card card in cards)
        {
            IUpgradeOffer o = card.offer;
            card.title.text = $"<b>{o.Title}</b>  <size=20><color=#9aa>Nivel {o.Level}/{o.MaxLevel}</color></size>";
            card.current.text = o.CurrentEffect;
            if (o.IsMaxed)
            {
                card.next.text = "<color=#5f5>Nivel máximo</color>";
                CostChips.Fill(card.costRow, new List<ItemAmount>(), inventory, 26f, 18f);
                card.buy.interactable = false;
                card.buyLabel.text = "Máximo";
            }
            else
            {
                card.next.text = $"<color=#ffb070>Próximo:</color> {o.NextEffect}";
                CostChips.Fill(card.costRow, o.NextCost, inventory, 26f, 18f);
                bool affordable = CostText.CanAfford(inventory, o.NextCost);
                card.buy.interactable = affordable;
                card.buyLabel.text = affordable ? "Mejorar" : "Faltan recursos";
            }

            bool pinned = PinnedUpgrades.IsPinned(PinnedUpgrades.UpgradeKey(o.UpgradeId));
            card.pin.interactable = !o.IsMaxed;
            card.pinLabel.text = pinned ? "Fijada en HUD" : "Fijar en HUD";
            card.pin.GetComponent<Image>().color = pinned ? new Color(0.15f, 0.55f, 0.65f, 1f) : RuntimeUI.ButtonColor;
        }
    }

    private void Build()
    {
        if (canvas != null)
        {
            // Se rearma solo si es otra tienda (cada una puede vender mejoras distintas).
            if (builtFor == shop) return;
            Destroy(canvas.gameObject);
            cards.Clear();
        }

        builtFor = shop;
        canvas = RuntimeUI.CreateCanvas("UpgradeShopCanvas", transform, 30);
        Image dim = RuntimeUI.Image("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.45f));
        RuntimeUI.Stretch(dim.rectTransform);

        float height = 92f + shop.Offers.Count * (CardHeight + 12f) + 50f;
        Image windowImage = RuntimeUI.Image("Window", canvas.transform, RuntimeUI.PanelColor);
        window = windowImage.rectTransform;
        RuntimeUI.Center(window, Width, height);

        TMP_Text title = RuntimeUI.Text("Title", window, $"<b>{shop.ShopName.ToUpperInvariant()}</b>", 34);
        RuntimeUI.Place(title.rectTransform, Pad, 20f, Width - Pad * 2f, 44f);
        TMP_Text close = RuntimeUI.Text("Close", window, "<color=#9aa>Esc o E para cerrar</color>", 18, TextAlignmentOptions.Right);
        RuntimeUI.Place(close.rectTransform, Width - 260f - Pad, 30f, 260f, 30f);

        float y = 80f;
        foreach (IUpgradeOffer offer in shop.Offers)
        {
            var card = new Card { offer = offer };
            Image bg = RuntimeUI.Image("Card_" + offer.UpgradeId, window, new Color(1f, 1f, 1f, 0.05f), false);
            RuntimeUI.Place(bg.rectTransform, Pad, y, Width - Pad * 2f, CardHeight);
            Transform t = bg.transform;

            card.title = RuntimeUI.Text("Title", t, "", 26);
            RuntimeUI.Place(card.title.rectTransform, 16f, 10f, 560f, 34f);
            card.current = RuntimeUI.Text("Current", t, "", 17);
            card.current.color = new Color(0.75f, 0.8f, 0.85f);
            card.current.textWrappingMode = TextWrappingModes.NoWrap;
            card.current.overflowMode = TextOverflowModes.Ellipsis;
            RuntimeUI.Place(card.current.rectTransform, 16f, 44f, 620f, 24f);
            card.next = RuntimeUI.Text("Next", t, "", 17);
            card.next.textWrappingMode = TextWrappingModes.NoWrap;
            card.next.overflowMode = TextOverflowModes.Ellipsis;
            RuntimeUI.Place(card.next.rectTransform, 16f, 70f, 620f, 24f);

            card.costRow = RuntimeUI.Rect("Cost", t);
            RuntimeUI.Place(card.costRow, 16f, 98f, 560f, 26f);

            Card captured = card;
            card.buy = RuntimeUI.Button("Buy", t, "Mejorar", () => Buy(captured), 22);
            RuntimeUI.Place((RectTransform)card.buy.transform, Width - Pad * 2f - 210f, 14f, 190f, 52f);
            card.buy.GetComponent<Image>().color = new Color(0.85f, 0.45f, 0.15f, 1f);
            card.buyLabel = card.buy.GetComponentInChildren<TMP_Text>();

            card.pin = RuntimeUI.Button("Pin", t, "Fijar en HUD", () => TogglePin(captured), 17);
            RuntimeUI.Place((RectTransform)card.pin.transform, Width - Pad * 2f - 210f, 74f, 190f, 40f);
            card.pinLabel = card.pin.GetComponentInChildren<TMP_Text>();

            cards.Add(card);
            y += CardHeight + 12f;
        }

    }
}
