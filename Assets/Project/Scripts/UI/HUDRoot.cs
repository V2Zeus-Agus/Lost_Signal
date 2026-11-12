using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lienzo único del HUD con columnas: izquierda (signos vitales + mejoras fijadas),
/// derecha (objetivos + ambiente), abajo a la izquierda (mochila), abajo a la derecha (arma)
/// y centro (avisos y prompt). Cada widget se agrega a su columna con un orden y se apilan solos.
/// Singleton perezoso: lo crea el primer widget que lo pide.
/// </summary>
public class HUDRoot : MonoBehaviour
{
    public const float LeftWidth = 380f;
    public const float RightWidth = 430f;

    private static HUDRoot instance;

    public static HUDRoot Instance
    {
        get
        {
            if (instance == null) instance = new GameObject("HUDRoot").AddComponent<HUDRoot>();
            return instance;
        }
    }

    public RectTransform Left { get; private set; }
    public RectTransform Right { get; private set; }
    public RectTransform BottomLeft { get; private set; }
    public RectTransform BottomRight { get; private set; }
    public RectTransform Center { get; private set; }

    private Canvas canvas;

    private void Awake()
    {
        instance = this;
        canvas = RuntimeUI.CreateCanvas("HUDCanvas", transform, 0);
        Destroy(canvas.GetComponent<GraphicRaycaster>()); // el HUD no recibe clicks

        Left = Column("Left", new Vector2(0f, 1f), new Vector2(24f, -24f), LeftWidth, TextAnchor.UpperLeft);
        Right = Column("Right", new Vector2(1f, 1f), new Vector2(-24f, -24f), RightWidth, TextAnchor.UpperRight);
        BottomLeft = Column("BottomLeft", new Vector2(0f, 0f), new Vector2(24f, 24f), 320f, TextAnchor.LowerLeft);
        BottomRight = Column("BottomRight", new Vector2(1f, 0f), new Vector2(-24f, 24f), 300f, TextAnchor.LowerRight);
        Center = RuntimeUI.Rect("Center", canvas.transform);
        RuntimeUI.Stretch(Center);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void LateUpdate()
    {
        bool playing = GameManager.Instance != null && GameManager.Instance.IsPlaying;
        canvas.enabled = playing && !UIModal.IsOpen;
    }

    private RectTransform Column(string name, Vector2 corner, Vector2 offset, float width, TextAnchor alignment)
    {
        RectTransform column = RuntimeUI.Rect(name, canvas.transform);
        column.anchorMin = column.anchorMax = column.pivot = corner;
        column.anchoredPosition = offset;
        column.sizeDelta = new Vector2(width, 100f);

        VerticalLayoutGroup layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = alignment;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = column.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return column;
    }

    /// <summary>Agrega un panel a una columna; los de menor orden quedan arriba.</summary>
    public void Add(RectTransform column, RectTransform panel, int order)
    {
        panel.SetParent(column, false);
        panel.gameObject.AddComponent<HUDOrder>().order = order;

        int index = 0;
        for (int i = 0; i < column.childCount; i++)
        {
            Transform child = column.GetChild(i);
            if (child == panel) continue;
            HUDOrder other = child.GetComponent<HUDOrder>();
            if (other != null && other.order <= order) index++;
        }
        panel.SetSiblingIndex(index);
    }
}

/// <summary>Orden de un panel dentro de su columna del HUD.</summary>
public class HUDOrder : MonoBehaviour
{
    public int order;
}
