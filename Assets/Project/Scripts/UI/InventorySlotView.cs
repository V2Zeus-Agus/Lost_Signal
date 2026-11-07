using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Un slot visual del inventario. Solo muestra datos y avisa al panel qué pasó
/// (click, arrastre, soltar); el panel decide qué comando ejecutar.
/// </summary>
public class InventorySlotView : MonoBehaviour,
    IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    private InventoryPanel panel;
    private Image background;
    private Image icon;
    private TMP_Text amountText;
    private Image frame;

    public int Index { get; private set; }

    public void Init(InventoryPanel owner, int index, Image bg, Image iconImage, TMP_Text amount, Image selectionFrame)
    {
        panel = owner;
        Index = index;
        background = bg;
        icon = iconImage;
        amountText = amount;
        frame = selectionFrame;
    }

    public void Show(ItemStack stack, bool selected)
    {
        bool empty = stack == null || stack.IsEmpty;
        RuntimeUI.SetItemIcon(icon, empty ? null : stack.item);
        amountText.text = empty ? string.Empty : stack.amount.ToString();
        frame.enabled = selected;
        background.color = empty
            ? RuntimeUI.SlotColor
            : Color.Lerp(RuntimeUI.SlotColor, stack.item.uiColor * new Color(1f, 1f, 1f, 0.25f), 0.5f);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.dragging) return;
        if (eventData.button == PointerEventData.InputButton.Left) panel.Select(Index);
        else if (eventData.button == PointerEventData.InputButton.Right) panel.DiscardOne(Index);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        panel.BeginDrag(Index, eventData.position);
    }

    public void OnDrag(PointerEventData eventData) => panel.UpdateDrag(eventData.position);

    public void OnEndDrag(PointerEventData eventData) => panel.EndDrag();

    public void OnDrop(PointerEventData eventData)
    {
        InventorySlotView source = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<InventorySlotView>() : null;
        if (source != null) panel.DropOn(source.Index, Index);
    }
}
