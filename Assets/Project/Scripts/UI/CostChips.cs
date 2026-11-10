using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fila de "fichas" de costo: ícono del recurso + "tengo/necesito" (verde si alcanza, rojo si no).
/// La usan la tienda de mejoras y el menú de construcción.
/// </summary>
public static class CostChips
{
    public static readonly Color Enough = new Color(0.55f, 1f, 0.55f);
    public static readonly Color Missing = new Color(1f, 0.45f, 0.4f);

    public static void Fill(RectTransform row, IReadOnlyList<ItemAmount> cost, Inventory inventory, float iconSize = 34f, float fontSize = 20f)
    {
        for (int i = row.childCount - 1; i >= 0; i--) Object.Destroy(row.GetChild(i).gameObject);

        float x = 0f;
        foreach (ItemAmount c in cost)
        {
            if (c.item == null) continue;
            int have = inventory != null ? inventory.GetAmount(c.item) : 0;

            Image icon = RuntimeUI.Image("Icon", row, Color.white, false);
            RuntimeUI.SetItemIcon(icon, c.item);
            RuntimeUI.Place(icon.rectTransform, x, 0f, iconSize, iconSize);
            x += iconSize + 4f;

            TMP_Text text = RuntimeUI.Text("Amount", row, $"{have}/{c.amount}", fontSize, TextAlignmentOptions.Left);
            text.color = have >= c.amount ? Enough : Missing;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            RuntimeUI.Place(text.rectTransform, x, 4f, 80f, iconSize);
            x += Mathf.Max(text.GetPreferredValues().x, 30f) + 16f;
        }
    }
}
