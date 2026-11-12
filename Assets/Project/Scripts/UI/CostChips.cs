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

    /// <summary>
    /// Llena la fila con fichas. Si se pasa <paramref name="maxWidth"/>, las fichas que no entran
    /// bajan a otra línea. Devuelve el alto ocupado.
    /// </summary>
    public static float Fill(RectTransform row, IReadOnlyList<ItemAmount> cost, Inventory inventory, float iconSize = 34f, float fontSize = 20f, float maxWidth = 0f)
    {
        for (int i = row.childCount - 1; i >= 0; i--) Object.Destroy(row.GetChild(i).gameObject);

        float x = 0f, y = 0f;
        bool any = false;
        foreach (ItemAmount c in cost)
        {
            if (c.item == null) continue;
            int have = inventory != null ? inventory.GetAmount(c.item) : 0;
            string label = $"{have}/{c.amount}";

            Image icon = RuntimeUI.Image("Icon", row, Color.white, false);
            TMP_Text text = RuntimeUI.Text("Amount", row, label, fontSize, TextAlignmentOptions.Left);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            // Ancho estimado por cantidad de caracteres: GetPreferredValues falla (NullReference) si el
            // texto se crea con el lienzo del HUD apagado (p. ej. fijando con la tienda abierta).
            float textWidth = Mathf.Max(label.Length * fontSize * 0.58f, 30f);
            float chipWidth = iconSize + 4f + textWidth;

            if (maxWidth > 0f && any && x + chipWidth > maxWidth)
            {
                x = 0f;
                y += iconSize + 6f;
            }

            RuntimeUI.SetItemIcon(icon, c.item);
            RuntimeUI.Place(icon.rectTransform, x, y, iconSize, iconSize);
            text.color = have >= c.amount || DevCheats.InfiniteMaterials ? Enough : Missing;
            RuntimeUI.Place(text.rectTransform, x + iconSize + 4f, y + (iconSize - fontSize * 1.2f) * 0.5f, textWidth + 4f, iconSize);
            x += chipWidth + 16f;
            any = true;
        }
        return any ? y + iconSize : 0f;
    }
}
