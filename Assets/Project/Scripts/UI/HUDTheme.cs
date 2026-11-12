using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Estilo único del HUD (paleta tipo KayKit Space Base: casco claro, naranja y celeste) y
/// ayudantes para armar paneles redondeados que se acomodan solos con LayoutGroups.
/// </summary>
public static class HUDTheme
{
    public static readonly Color PanelColor = new Color(0.05f, 0.065f, 0.1f, 0.84f);
    public static readonly Color Card = new Color(1f, 1f, 1f, 0.055f);
    public static readonly Color Track = new Color(1f, 1f, 1f, 0.1f);
    public static readonly Color Text = new Color(0.93f, 0.95f, 0.98f);
    public static readonly Color Muted = new Color(0.6f, 0.66f, 0.74f);
    public static readonly Color Accent = new Color(1f, 0.56f, 0.2f);
    public static readonly Color Teal = new Color(0.33f, 0.85f, 0.96f);
    public static readonly Color Good = new Color(0.47f, 0.92f, 0.52f);
    public static readonly Color Warn = new Color(1f, 0.8f, 0.3f);
    public static readonly Color Bad = new Color(1f, 0.38f, 0.32f);
    public static readonly Color Health = new Color(0.94f, 0.32f, 0.32f);
    public static readonly Color Stamina = new Color(1f, 0.78f, 0.27f);
    public static readonly Color Oxygen = new Color(0.33f, 0.8f, 1f);

    public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

    private static Sprite rounded;

    /// <summary>Sprite 9-slice redondeado generado por código (no hace falta importar texturas).</summary>
    public static Sprite Rounded
    {
        get
        {
            if (rounded == null) rounded = MakeRounded(64, 14);
            return rounded;
        }
    }

    private static Sprite MakeRounded(int size, int radius)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "HUDRounded",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
            float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
            float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
            byte alpha = (byte)(Mathf.Clamp01(radius - distance + 0.5f) * 255f);
            pixels[y * size + x] = new Color32(255, 255, 255, alpha);
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    public static Image RoundedImage(string name, Transform parent, Color color, bool raycast = false)
    {
        Image image = RuntimeUI.Image(name, parent, color, raycast);
        image.sprite = Rounded;
        image.type = Image.Type.Sliced;
        return image;
    }

    /// <summary>Panel redondeado con título; los hijos se apilan solos (VerticalLayoutGroup).</summary>
    public static RectTransform Panel(string name, string title, Color accent)
    {
        Image bg = RoundedImage(name, null, PanelColor);
        RectTransform root = bg.rectTransform;
        VerticalLayoutGroup layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 12, 14);
        layout.spacing = 6f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        if (!string.IsNullOrEmpty(title))
        {
            RectTransform header = Row(root, 22f);
            Image bar = RoundedImage("Accent", header, accent);
            RuntimeUI.Place(bar.rectTransform, 0f, 3f, 4f, 16f);
            bar.type = Image.Type.Simple;
            TMP_Text label = RuntimeUI.Text("Title", header, title.ToUpperInvariant(), 15);
            label.fontStyle = FontStyles.Bold;
            label.characterSpacing = 6f;
            label.color = Muted;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            RuntimeUI.Place(label.rectTransform, 12f, 0f, 400f, 22f);
        }
        return root;
    }

    /// <summary>Fila de alto fijo dentro de un panel con layout.</summary>
    public static RectTransform Row(Transform parent, float height)
    {
        RectTransform row = RuntimeUI.Rect("Row", parent);
        LayoutElement element = row.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = element.minHeight = height;
        return row;
    }

    /// <summary>Texto que crece hacia abajo según su contenido.</summary>
    public static TMP_Text Label(Transform parent, string value, float size, Color color)
    {
        TMP_Text text = RuntimeUI.Text("Label", parent, value, size);
        text.color = color;
        return text;
    }

    /// <summary>Contenedor vertical sin fondo (para agrupar filas dentro de un panel).</summary>
    public static RectTransform Group(Transform parent, float spacing = 4f)
    {
        RectTransform group = RuntimeUI.Rect("Group", parent);
        VerticalLayoutGroup layout = group.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return group;
    }

    /// <summary>Etiqueta a la izquierda y valor a la derecha en la misma fila.</summary>
    public static void SplitRow(Transform parent, float height, float size, out TMP_Text left, out TMP_Text right)
    {
        RectTransform row = Row(parent, height);
        left = RuntimeUI.Text("Left", row, "", size);
        left.textWrappingMode = TextWrappingModes.NoWrap;
        left.color = Text;
        RuntimeUI.Stretch(left.rectTransform);
        right = RuntimeUI.Text("Right", row, "", size, TextAlignmentOptions.TopRight);
        right.textWrappingMode = TextWrappingModes.NoWrap;
        right.color = Text;
        RuntimeUI.Stretch(right.rectTransform);
    }
}

/// <summary>Barra redondeada (pista + relleno). El relleno se escala por anclas.</summary>
public class HUDBar
{
    public readonly RectTransform Root;
    private readonly Image fill;

    public HUDBar(Transform parent, float height, Color color)
    {
        Root = HUDTheme.Row(parent, height);
        Image track = HUDTheme.RoundedImage("Track", Root, HUDTheme.Track);
        RuntimeUI.Stretch(track.rectTransform);
        fill = HUDTheme.RoundedImage("Fill", Root, color);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
    }

    public void Set(float normalized) => Set(normalized, fill.color);

    public void Set(float normalized, Color color)
    {
        normalized = Mathf.Clamp01(normalized);
        fill.rectTransform.anchorMax = new Vector2(normalized, 1f);
        fill.enabled = normalized > 0.001f;
        fill.color = color;
    }
}

/// <summary>Fila con ícono de item + texto izquierda + texto derecha.</summary>
public class HUDLine
{
    public readonly RectTransform Root;
    public readonly Image Icon;
    public readonly TMP_Text Left;
    public readonly TMP_Text Right;

    public HUDLine(Transform parent, float height, float fontSize)
    {
        Root = HUDTheme.Row(parent, height);
        Icon = RuntimeUI.Image("Icon", Root, Color.white, false);
        RuntimeUI.Place(Icon.rectTransform, 0f, (height - 18f) * 0.5f, 18f, 18f);
        Left = RuntimeUI.Text("Left", Root, "", fontSize);
        Left.textWrappingMode = TextWrappingModes.NoWrap;
        Left.overflowMode = TextOverflowModes.Ellipsis;
        Left.color = HUDTheme.Text;
        Left.rectTransform.anchorMin = Vector2.zero;
        Left.rectTransform.anchorMax = Vector2.one;
        Left.rectTransform.offsetMin = new Vector2(26f, 0f);
        Left.rectTransform.offsetMax = new Vector2(-110f, 0f);
        Right = RuntimeUI.Text("Right", Root, "", fontSize, TextAlignmentOptions.TopRight);
        Right.textWrappingMode = TextWrappingModes.NoWrap;
        Right.color = HUDTheme.Text;
        RuntimeUI.Stretch(Right.rectTransform);
    }

    /// <summary>Ícono del item (o un cuadradito de color si no tiene ícono).</summary>
    public void SetItem(ItemDefinition item) => RuntimeUI.SetItemIcon(Icon, item);

    /// <summary>Casilla de tarea: verde si está hecha, gris si no.</summary>
    public void SetCheck(bool done)
    {
        Icon.enabled = true;
        Icon.sprite = HUDTheme.Rounded;
        Icon.type = Image.Type.Sliced;
        Icon.pixelsPerUnitMultiplier = 3f;
        Icon.color = done ? HUDTheme.Good : new Color(1f, 1f, 1f, 0.18f);
    }

    public void SetActive(bool active)
    {
        if (Root.gameObject.activeSelf != active) Root.gameObject.SetActive(active);
    }
}

/// <summary>Reutiliza filas en vez de destruirlas y crearlas en cada refresco.</summary>
public class HUDLineList
{
    private readonly Transform parent;
    private readonly float height;
    private readonly float fontSize;
    private readonly List<HUDLine> lines = new List<HUDLine>();

    public HUDLineList(Transform parent, float height, float fontSize)
    {
        this.parent = parent;
        this.height = height;
        this.fontSize = fontSize;
    }

    public HUDLine Get(int index)
    {
        while (lines.Count <= index) lines.Add(new HUDLine(parent, height, fontSize));
        lines[index].SetActive(true);
        return lines[index];
    }

    /// <summary>Oculta las filas que sobran desde <paramref name="used"/>.</summary>
    public void Trim(int used)
    {
        for (int i = used; i < lines.Count; i++) lines[i].SetActive(false);
    }
}
