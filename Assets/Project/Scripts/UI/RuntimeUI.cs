using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Helpers para armar UI por código (inventario, consola de desarrollador).
/// Evita depender de prefabs de UI y mantiene el mismo estilo en todos los paneles.
/// </summary>
public static class RuntimeUI
{
    public static readonly Color PanelColor = new Color(0.03f, 0.04f, 0.08f, 0.92f);
    public static readonly Color SlotColor = new Color(1f, 1f, 1f, 0.07f);
    public static readonly Color ButtonColor = new Color(0.16f, 0.2f, 0.3f, 1f);
    public static readonly Color AccentColor = new Color(1f, 0.55f, 0.2f, 1f);

    public static Canvas CreateCanvas(string name, Transform parent, int sortingOrder)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(parent, false);
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    /// <summary>Sin EventSystem los botones no reciben clicks. Crea uno si la escena no tiene.</summary>
    public static void EnsureEventSystem()
    {
        if (EventSystem.current != null || Object.FindFirstObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        Object.DontDestroyOnLoad(go);
    }

    public static void ClearSelection()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static Image Image(string name, Transform parent, Color color, bool raycast = true)
    {
        RectTransform rt = Rect(name, parent);
        Image image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    public static TextMeshProUGUI Text(string name, Transform parent, string value, float size,
        TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
    {
        RectTransform rt = Rect(name, parent);
        TextMeshProUGUI text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.richText = true;
        text.raycastTarget = false;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    public static Button Button(string name, Transform parent, string label, UnityAction onClick, float fontSize = 22f)
    {
        Image bg = Image(name, parent, ButtonColor);
        Button button = bg.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        button.colors = colors;
        if (onClick != null) button.onClick.AddListener(onClick);

        TextMeshProUGUI text = Text("Label", bg.transform, label, fontSize, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, 6f);
        return button;
    }

    public static void Stretch(RectTransform rt, float padding = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padding, padding);
        rt.offsetMax = new Vector2(-padding, -padding);
    }

    /// <summary>Posiciona con ancla arriba-izquierda del padre (x hacia la derecha, y hacia abajo).</summary>
    public static void Place(RectTransform rt, float x, float y, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(width, height);
    }

    public static void Center(RectTransform rt, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(width, height);
    }

    /// <summary>Muestra el ícono del item; si todavía no tiene, un cuadrado de su color.</summary>
    public static void SetItemIcon(Image image, ItemDefinition item)
    {
        if (item == null)
        {
            image.enabled = false;
            return;
        }
        image.enabled = true;
        image.sprite = item.icon;
        image.preserveAspect = true;
        image.color = item.icon != null ? Color.white : item.uiColor;
    }
}
