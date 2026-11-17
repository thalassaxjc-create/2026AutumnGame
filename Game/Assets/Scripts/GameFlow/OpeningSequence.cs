using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>第一天序章 CG：乡路插画由黑白渐变为彩色，并显示返乡字幕。</summary>
[DisallowMultipleComponent]
public sealed class OpeningSequence : MonoBehaviour
{
    WorkshopOrder order;
    GameObject view;
    CanvasGroup group;
    OpeningIllustrationGraphic illustration;
    PlayerController player;
    bool playerWasEnabled;
    float elapsed;
    bool finished;

    public bool IsPlaying => !finished && view != null;

    public void Begin(WorkshopOrder story)
    {
        if (view != null || story == null || story.CurrentPhase != WorkshopOrder.Phase.Opening)
            return;
        order = story;
        BuildView();
        player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            playerWasEnabled = player.enabled;
            player.enabled = false;
        }
        GameFlowModalState.Acquire(this);
    }

    void Update()
    {
        if (finished || view == null)
            return;
        elapsed += Time.unscaledDeltaTime;
        illustration.SetColorAmount(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.1f, 3.6f, elapsed)));
        group.alpha = elapsed < 5.2f ? 1f : Mathf.Clamp01(1f - (elapsed - 5.2f) / .65f);

        Keyboard keyboard = Keyboard.current;
        bool skip = keyboard != null && (keyboard.spaceKey.wasPressedThisFrame ||
            keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame ||
            keyboard.escapeKey.wasPressedThisFrame);
        if (skip || elapsed >= 5.85f)
            CompleteImmediately();
    }

    public void CompleteImmediately()
    {
        if (finished)
            return;
        finished = true;
        order?.CompleteOpening();
        if (player != null && playerWasEnabled)
            player.enabled = true;
        GameFlowModalState.Release(this);
        if (view != null)
            Destroy(view);
        view = null;
    }

    void BuildView()
    {
        view = new GameObject("OpeningCG", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(CanvasGroup));
        view.transform.SetParent(transform, false);
        Canvas canvas = view.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler scaler = view.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;
        group = view.GetComponent<CanvasGroup>();
        group.blocksRaycasts = true;

        var art = new GameObject("CountryRoadIllustration", typeof(RectTransform),
            typeof(OpeningIllustrationGraphic));
        art.transform.SetParent(view.transform, false);
        RectTransform artRect = art.GetComponent<RectTransform>();
        artRect.anchorMin = Vector2.zero;
        artRect.anchorMax = Vector2.one;
        artRect.offsetMin = artRect.offsetMax = Vector2.zero;
        illustration = art.GetComponent<OpeningIllustrationGraphic>();

        var panel = new GameObject("SubtitlePanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(view.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(.12f, .08f);
        panelRect.anchorMax = new Vector2(.88f, .25f);
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(.03f, .025f, .02f, .78f);

        var textObject = new GameObject("Subtitle", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(panel.transform, false);
        Text text = textObject.GetComponent<Text>();
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(45f, 20f);
        text.rectTransform.offsetMax = new Vector2(-45f, -20f);
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 38;
        text.fontStyle = FontStyle.Bold;
        text.color = new Color32(255, 244, 218, 255);
        text.text = "“不知道去哪，不知道喜欢什么，那就先回家吧。”\n<size=24>空格 / Enter 跳过</size>";
        text.raycastTarget = false;
        InteractionPromptUI.EnsureChineseFont(text);
    }

    void OnDisable()
    {
        if (!finished)
            CompleteImmediately();
    }
}

public sealed class OpeningIllustrationGraphic : MaskableGraphic
{
    float colorAmount;

    public void SetColorAmount(float value)
    {
        value = Mathf.Clamp01(value);
        if (Mathf.Abs(value - colorAmount) < .002f)
            return;
        colorAmount = value;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        Quad(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMax), Tone(new Color32(154, 205, 224, 255)));
        Triangle(vh, new Vector2(r.xMin, r.yMin + r.height * .42f), new Vector2(r.xMin + r.width * .42f, r.yMax * .73f),
            new Vector2(r.center.x, r.yMin + r.height * .35f), Tone(new Color32(87, 132, 82, 255)));
        Triangle(vh, new Vector2(r.xMax, r.yMin + r.height * .40f), new Vector2(r.xMin + r.width * .62f, r.yMax * .68f),
            new Vector2(r.center.x, r.yMin + r.height * .34f), Tone(new Color32(105, 151, 91, 255)));
        Quad(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin + r.height * .39f), Tone(new Color32(188, 178, 105, 255)));

        // 由远及近的乡间小路。
        Polygon(vh, new[] { new Vector2(r.center.x - 85f, r.yMin + r.height * .42f),
            new Vector2(r.center.x + 70f, r.yMin + r.height * .42f), new Vector2(r.center.x + 420f, r.yMin),
            new Vector2(r.center.x - 360f, r.yMin) }, Tone(new Color32(220, 195, 142, 255)));

        Vector2 person = new Vector2(r.center.x + 110f, r.yMin + r.height * .33f);
        Circle(vh, person + Vector2.up * 145f, 34f, Tone(new Color32(226, 185, 150, 255)));
        Quad(vh, person + new Vector2(-30f, 25f), person + new Vector2(30f, 125f), Tone(new Color32(54, 81, 97, 255)));
        Quad(vh, person + new Vector2(-28f, -65f), person + new Vector2(-5f, 28f), Tone(new Color32(45, 49, 55, 255)));
        Quad(vh, person + new Vector2(8f, -65f), person + new Vector2(31f, 28f), Tone(new Color32(45, 49, 55, 255)));
        Quad(vh, person + new Vector2(52f, -35f), person + new Vector2(120f, 55f), Tone(new Color32(151, 79, 54, 255)));
        Quad(vh, person + new Vector2(80f, 48f), person + new Vector2(88f, 116f), Tone(new Color32(60, 54, 48, 255)));
    }

    Color32 Tone(Color color)
    {
        float gray = color.r * .299f + color.g * .587f + color.b * .114f;
        return Color.Lerp(new Color(gray, gray, gray, color.a), color, colorAmount);
    }

    static void Quad(VertexHelper vh, Vector2 min, Vector2 max, Color32 color) => Polygon(vh,
        new[] { new Vector2(min.x, min.y), new Vector2(min.x, max.y), new Vector2(max.x, max.y), new Vector2(max.x, min.y) }, color);

    static void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color32 color) => Polygon(vh, new[] { a, b, c }, color);

    static void Circle(VertexHelper vh, Vector2 center, float radius, Color32 color)
    {
        const int segments = 16;
        int start = vh.currentVertCount;
        vh.AddVert(center, color, Vector2.zero);
        for (int i = 0; i <= segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
        }
        for (int i = 0; i < segments; i++)
            vh.AddTriangle(start, start + i + 1, start + i + 2);
    }

    static void Polygon(VertexHelper vh, Vector2[] points, Color32 color)
    {
        int start = vh.currentVertCount;
        for (int i = 0; i < points.Length; i++)
            vh.AddVert(points[i], color, Vector2.zero);
        for (int i = 1; i < points.Length - 1; i++)
            vh.AddTriangle(start, start + i, start + i + 1);
    }
}
