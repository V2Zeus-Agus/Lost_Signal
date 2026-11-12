using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pasa al estilo del HUD los paneles hechos a mano en la escena (opciones, victoria, derrota,
/// intro): botones y sliders redondeados con la paleta, sin tocar su orden ni sus eventos.
/// </summary>
public static class ScreenStyler
{
    public static void Apply(GameObject root)
    {
        if (root == null) return;
        AddBackdrop(root);

        foreach (Button button in root.GetComponentsInChildren<Button>(true))
        {
            if (button.targetGraphic is Image image)
            {
                image.sprite = HUDTheme.Rounded;
                image.type = Image.Type.Sliced;
                image.color = IsPrimary(button) ? HUDTheme.Accent : RuntimeUI.ButtonColor;
            }
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true)) text.color = Color.white;
        }

        foreach (Slider slider in root.GetComponentsInChildren<Slider>(true))
        {
            Transform background = slider.transform.Find("Background");
            if (background != null && background.TryGetComponent(out Image bg)) Round(bg, HUDTheme.Track);
            if (slider.fillRect != null && slider.fillRect.TryGetComponent(out Image fill)) Round(fill, HUDTheme.Accent);
            if (slider.handleRect != null && slider.handleRect.TryGetComponent(out Image handle)) Round(handle, Color.white);
        }

        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            if (text.GetComponentInParent<Button>() == null && text.color.a > 0.5f && IsGray(text.color))
                text.color = HUDTheme.Text;
    }

    /// <summary>
    /// Oscurece la pantalla y pone una tarjeta redondeada detrás del contenido del panel, igual
    /// que la pausa. Se calcula con los rects de los hijos, así sirve para cualquier panel.
    /// </summary>
    private static void AddBackdrop(GameObject root)
    {
        if (!(root.transform is RectTransform rt) || root.transform.Find("StyledCard") != null) return;

        // Fondo oscuro (solo si el panel ocupa la pantalla y no tiene uno propio).
        if (!root.TryGetComponent(out Image dim))
        {
            dim = root.AddComponent<Image>();
            dim.color = new Color(0.01f, 0.015f, 0.03f, 0.72f);
        }
        else if (dim.color.a < 0.5f) dim.color = new Color(0.01f, 0.015f, 0.03f, 0.72f);

        bool any = false;
        var bounds = new Bounds();
        var corners = new Vector3[4];
        foreach (RectTransform child in rt)
        {
            if (!child.gameObject.activeSelf) continue;
            child.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector3 local = rt.InverseTransformPoint(corner);
                if (!any) { bounds = new Bounds(local, Vector3.zero); any = true; }
                else bounds.Encapsulate(local);
            }
        }
        if (!any) return;

        Image card = HUDTheme.RoundedImage("StyledCard", root.transform, new Color(0.05f, 0.065f, 0.1f, 0.96f));
        card.transform.SetAsFirstSibling();
        RectTransform c = card.rectTransform;
        c.anchorMin = c.anchorMax = rt.pivot;
        c.pivot = new Vector2(0.5f, 0.5f);
        c.anchoredPosition = bounds.center; // ancla en el pivot del panel = origen del espacio local
        c.sizeDelta = (Vector2)bounds.size + new Vector2(80f, 64f);

        Image strip = HUDTheme.RoundedImage("Accent", c, HUDTheme.Accent);
        strip.rectTransform.anchorMin = new Vector2(0f, 1f);
        strip.rectTransform.anchorMax = new Vector2(1f, 1f);
        strip.rectTransform.pivot = new Vector2(0.5f, 1f);
        strip.rectTransform.offsetMin = new Vector2(40f, -6f);
        strip.rectTransform.offsetMax = new Vector2(-40f, 0f);
    }

    private static void Round(Image image, Color color)
    {
        image.sprite = HUDTheme.Rounded;
        image.type = Image.Type.Sliced;
        image.color = color;
    }

    /// <summary>El botón principal de cada pantalla (seguir / volver) va en naranja.</summary>
    private static bool IsPrimary(Button button)
    {
        string n = button.name.ToLowerInvariant();
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        string t = label != null ? label.text.ToLowerInvariant() : "";
        return n.Contains("continue") || n.Contains("resume") || n.Contains("back") ||
               t.Contains("continuar") || t.Contains("reanudar") || t.Contains("volver") || t.Contains("empezar") || t.Contains("jugar");
    }

    private static bool IsGray(Color c) => Mathf.Abs(c.r - c.g) < 0.08f && Mathf.Abs(c.g - c.b) < 0.08f;
}
