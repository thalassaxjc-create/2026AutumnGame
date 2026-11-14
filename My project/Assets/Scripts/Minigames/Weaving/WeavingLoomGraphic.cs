using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 不依赖图片资源的抽象编帘工作台背景。
/// 绘制木架、帘面、吊线与木梭，并随编织进度和按键产生微小运动。
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class WeavingLoomGraphic : MaskableGraphic
{
    float progress;
    float swingRemaining;
    int swingDirection;
    float knotPulse;

    public void SetProgress(float value)
    {
        value = Mathf.Clamp01(value);
        if (Mathf.Approximately(progress, value))
            return;

        progress = value;
        SetVerticesDirty();
    }

    public void PlayShuttle(int direction)
    {
        swingDirection = direction >= 0 ? 1 : -1;
        swingRemaining = 0.32f;
        SetVerticesDirty();
    }

    public void PlayKnot()
    {
        knotPulse = 0.42f;
        swingRemaining = 0.36f;
        SetVerticesDirty();
    }

    public void ResetVisual()
    {
        progress = 0f;
        swingRemaining = 0f;
        knotPulse = 0f;
        SetVerticesDirty();
    }

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    void Update()
    {
        bool dirty = false;
        if (swingRemaining > 0f)
        {
            swingRemaining = Mathf.Max(0f, swingRemaining - Time.unscaledDeltaTime);
            dirty = true;
        }

        if (knotPulse > 0f)
        {
            knotPulse = Mathf.Max(0f, knotPulse - Time.unscaledDeltaTime);
            dirty = true;
        }

        if (dirty)
            SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        float w = r.width;
        float h = r.height;

        Color top = new Color32(44, 34, 27, 255);
        Color bottom = new Color32(17, 13, 11, 255);
        AddGradientQuad(vh, r, bottom, top);

        // 暗角，让中央的帘面和音游区域更突出。
        AddQuad(vh, new Rect(r.xMin, r.yMin, w * 0.08f, h), new Color(0f, 0f, 0f, 0.28f));
        AddQuad(vh, new Rect(r.xMax - w * 0.08f, r.yMin, w * 0.08f, h), new Color(0f, 0f, 0f, 0.28f));

        Color woodShadow = new Color32(44, 26, 17, 225);
        Color woodDark = new Color32(102, 62, 37, 255);
        Color wood = new Color32(154, 99, 56, 255);
        Color woodLight = new Color32(190, 132, 76, 255);
        Color thread = new Color32(222, 213, 187, 225);
        Color bamboo = new Color32(205, 173, 107, 255);
        Color bambooLight = new Color32(236, 211, 145, 255);

        float left = r.xMin + w * 0.055f;
        float right = r.xMin + w * 0.785f;
        float lowerRailY = r.yMin + h * 0.30f;
        float upperRailY = r.yMin + h * 0.68f;

        // 木架投影和主体。
        AddQuad(vh, new Rect(left + 13f, lowerRailY - 17f, right - left + 30f, 42f), new Color(0f, 0f, 0f, 0.30f));
        AddQuad(vh, new Rect(left + 13f, upperRailY - 17f, right - left + 30f, 46f), new Color(0f, 0f, 0f, 0.30f));
        AddQuad(vh, new Rect(left, lowerRailY, right - left, 28f), woodDark);
        AddQuad(vh, new Rect(left, lowerRailY + 5f, right - left, 17f), wood);
        AddQuad(vh, new Rect(left, upperRailY, right - left, 34f), woodDark);
        AddQuad(vh, new Rect(left, upperRailY + 7f, right - left, 18f), woodLight);

        AddQuad(vh, new Rect(left - 24f, lowerRailY - 72f, 28f, upperRailY - lowerRailY + 126f), woodDark);
        AddQuad(vh, new Rect(right - 4f, lowerRailY - 72f, 28f, upperRailY - lowerRailY + 126f), woodDark);
        AddQuad(vh, new Rect(left - 17f, lowerRailY - 67f, 13f, upperRailY - lowerRailY + 116f), wood);
        AddQuad(vh, new Rect(right + 3f, lowerRailY - 67f, 13f, upperRailY - lowerRailY + 116f), wood);

        // 帘面底稿与已编织区域。
        Rect sheet = new Rect(left + 26f, lowerRailY + 34f, right - left - 52f, upperRailY - lowerRailY - 20f);
        AddQuad(vh, sheet, new Color(0.10f, 0.075f, 0.05f, 0.76f));
        AddOutline(vh, sheet, 2f, new Color(0.85f, 0.72f, 0.46f, 0.32f));

        float wovenWidth = sheet.width * progress;
        if (wovenWidth > 0.5f)
        {
            Rect woven = new Rect(sheet.xMin, sheet.yMin, wovenWidth, sheet.height);
            AddGradientQuad(vh, woven,
                new Color32(157, 123, 71, 245),
                new Color32(218, 187, 119, 250));
        }

        // 100 根竹丝一一对应进度。
        int placed = Mathf.RoundToInt(progress * 100f);
        for (int i = 0; i < 100; i++)
        {
            float x = Mathf.Lerp(sheet.xMin + 2f, sheet.xMax - 2f, (i + 0.5f) / 100f);
            Color lineColor = i < placed
                ? (i % 2 == 0 ? bambooLight : bamboo)
                : new Color(0.78f, 0.67f, 0.44f, 0.10f);
            AddLine(vh, new Vector2(x, sheet.yMin + 2f), new Vector2(x, sheet.yMax - 2f), i < placed ? 1.7f : 0.8f, lineColor);
        }

        // 横向绑线，让画面一眼能看出“竹帘”。
        for (int i = 0; i < 9; i++)
        {
            float y = Mathf.Lerp(sheet.yMin + 9f, sheet.yMax - 9f, i / 8f);
            AddLine(vh, new Vector2(sheet.xMin, y), new Vector2(sheet.xMax, y), 1.4f, thread);
        }

        // 帘面右端正在施工的位置。
        float workingX = sheet.xMin + wovenWidth;
        if (progress < 1f)
        {
            float pulse = 0.55f + Mathf.Sin(Time.unscaledTime * 5f) * 0.15f;
            AddLine(vh, new Vector2(workingX, sheet.yMin - 8f), new Vector2(workingX, sheet.yMax + 8f), 3f,
                new Color(1f, 0.92f, 0.68f, pulse));
        }

        // 工作台下方成排悬挂的木梭。
        float swingT = swingRemaining <= 0f ? 0f : Mathf.Sin((0.32f - swingRemaining) / 0.32f * Mathf.PI);
        for (int i = 0; i < 18; i++)
        {
            float t = (i + 0.5f) / 18f;
            float x = Mathf.Lerp(sheet.xMin + 8f, sheet.xMax - 8f, t);
            float parity = i % 2 == 0 ? 1f : -1f;
            float sway = swingT * swingDirection * parity * 12f;
            float stringBottom = lowerRailY - 98f - Mathf.Abs(sway) * 0.6f;
            AddLine(vh, new Vector2(x, sheet.yMin), new Vector2(x + sway * 0.35f, stringBottom + 24f), 1.2f, thread);

            Vector2 shuttle = new Vector2(x + sway, stringBottom);
            AddDiamond(vh, shuttle + new Vector2(3f, -4f), 17f, 27f, new Color(0f, 0f, 0f, 0.25f));
            AddDiamond(vh, shuttle, 17f, 27f, i % 3 == 0 ? woodLight : wood);
            AddQuad(vh, new Rect(shuttle.x - 4f, shuttle.y - 19f, 8f, 38f), woodDark);
        }

        // 打结时在帘面中心出现非常短的亮点。
        if (knotPulse > 0f)
        {
            float t = knotPulse / 0.42f;
            float size = Mathf.Lerp(35f, 8f, 1f - t);
            AddDiamond(vh, sheet.center, size, size, new Color(1f, 0.96f, 0.82f, t));
        }

        // 左下角的小型工艺说明牌。
        AddQuad(vh, new Rect(left, r.yMin + h * 0.095f, 310f, 62f), new Color(0.05f, 0.035f, 0.025f, 0.74f));
        AddLine(vh, new Vector2(left + 18f, r.yMin + h * 0.118f), new Vector2(left + 280f, r.yMin + h * 0.118f), 2f,
            new Color(0.82f, 0.64f, 0.35f, 0.62f));
    }

    static void AddOutline(VertexHelper vh, Rect rect, float thickness, Color color)
    {
        AddQuad(vh, new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
        AddQuad(vh, new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
        AddQuad(vh, new Rect(rect.xMin, rect.yMin, thickness, rect.height), color);
        AddQuad(vh, new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), color);
    }

    static void AddGradientQuad(VertexHelper vh, Rect rect, Color bottom, Color top)
    {
        int index = vh.currentVertCount;
        AddVert(vh, new Vector2(rect.xMin, rect.yMin), bottom);
        AddVert(vh, new Vector2(rect.xMin, rect.yMax), top);
        AddVert(vh, new Vector2(rect.xMax, rect.yMax), top);
        AddVert(vh, new Vector2(rect.xMax, rect.yMin), bottom);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }

    static void AddQuad(VertexHelper vh, Rect rect, Color color)
    {
        int index = vh.currentVertCount;
        AddVert(vh, new Vector2(rect.xMin, rect.yMin), color);
        AddVert(vh, new Vector2(rect.xMin, rect.yMax), color);
        AddVert(vh, new Vector2(rect.xMax, rect.yMax), color);
        AddVert(vh, new Vector2(rect.xMax, rect.yMin), color);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }

    static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
    {
        Vector2 direction = b - a;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (width * 0.5f);
        int index = vh.currentVertCount;
        AddVert(vh, a - normal, color);
        AddVert(vh, a + normal, color);
        AddVert(vh, b + normal, color);
        AddVert(vh, b - normal, color);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }

    static void AddDiamond(VertexHelper vh, Vector2 center, float halfWidth, float halfHeight, Color color)
    {
        int index = vh.currentVertCount;
        AddVert(vh, center + Vector2.left * halfWidth, color);
        AddVert(vh, center + Vector2.up * halfHeight, color);
        AddVert(vh, center + Vector2.right * halfWidth, color);
        AddVert(vh, center + Vector2.down * halfHeight, color);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }

    static void AddVert(VertexHelper vh, Vector2 position, Color color)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        vertex.uv0 = Vector2.zero;
        vh.AddVert(vertex);
    }
}
