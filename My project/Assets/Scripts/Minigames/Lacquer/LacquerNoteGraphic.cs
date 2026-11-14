using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 程序化绘制漆点与长漆条。长漆条始终从局部坐标左端拖向右端，
/// 实际方向由 RectTransform 的旋转决定。
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class LacquerNoteGraphic : MaskableGraphic
{
    public enum NoteShape
    {
        Tap,
        Stroke
    }

    NoteShape shape;
    Color lacquerColor = Color.red;
    float approach;
    float dragProgress;
    bool dragInside = true;

    public void Configure(NoteShape noteShape, Color color)
    {
        shape = noteShape;
        lacquerColor = color;
        dragProgress = 0f;
        dragInside = true;
        SetVerticesDirty();
    }

    public void SetApproach(float value)
    {
        value = Mathf.Clamp01(value);
        if (Mathf.Abs(approach - value) < 0.002f)
            return;

        approach = value;
        SetVerticesDirty();
    }

    public void SetDragProgress(float value, bool inside)
    {
        value = Mathf.Clamp01(value);
        if (Mathf.Abs(dragProgress - value) < 0.002f && dragInside == inside)
            return;

        dragProgress = value;
        dragInside = inside;
        SetVerticesDirty();
    }

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();

        if (shape == NoteShape.Tap)
            DrawTap(vh, r);
        else
            DrawStroke(vh, r);
    }

    void DrawTap(VertexHelper vh, Rect r)
    {
        Vector2 center = r.center;
        float baseRadius = Mathf.Min(r.width, r.height) * 0.25f;
        float ringRadius = Mathf.Lerp(baseRadius * 1.95f, baseRadius * 1.13f, approach);
        float ringAlpha = Mathf.Lerp(0.22f, 0.92f, approach);

        DrawRing(vh, center, ringRadius, 5f, new Color(1f, 0.94f, 0.82f, ringAlpha), 36);
        AddCircle(vh, center + new Vector2(4f, -5f), baseRadius + 5f, new Color(0f, 0f, 0f, 0.42f), 32);
        AddCircle(vh, center, baseRadius + 3f, Darken(lacquerColor, 0.48f), 32);
        AddCircle(vh, center, baseRadius, lacquerColor, 32);
        AddCircle(vh, center + new Vector2(-baseRadius * 0.28f, baseRadius * 0.32f), baseRadius * 0.22f,
            new Color(1f, 0.92f, 0.79f, 0.48f), 20);

        float coreRadius = Mathf.Lerp(baseRadius * 0.14f, baseRadius * 0.27f, approach);
        AddCircle(vh, center, coreRadius, new Color(1f, 0.96f, 0.86f, 0.72f), 20);
    }

    void DrawStroke(VertexHelper vh, Rect r)
    {
        float radius = Mathf.Min(35f, r.height * 0.27f);
        Vector2 start = new Vector2(r.xMin + radius + 12f, r.center.y);
        Vector2 end = new Vector2(r.xMax - radius - 12f, r.center.y);
        Vector2 filledEnd = Vector2.Lerp(start, end, dragProgress);
        Color dark = Darken(lacquerColor, 0.42f);
        Color fill = dragInside ? lacquerColor : new Color32(202, 104, 74, 255);

        float ringRadius = Mathf.Lerp(radius * 1.75f, radius * 1.12f, approach);
        DrawRing(vh, start, ringRadius, 4f, new Color(1f, 0.94f, 0.82f, Mathf.Lerp(0.18f, 0.90f, approach)), 30);

        AddLine(vh, start + new Vector2(4f, -5f), end + new Vector2(4f, -5f), radius * 1.65f,
            new Color(0f, 0f, 0f, 0.42f));
        AddLine(vh, start, end, radius * 1.52f, dark);
        AddLine(vh, start, end, radius * 1.12f, new Color(lacquerColor.r, lacquerColor.g, lacquerColor.b, 0.34f));

        if (dragProgress > 0.001f)
        {
            AddLine(vh, start, filledEnd, radius * 1.10f, fill);
            AddCircle(vh, filledEnd, radius * 0.55f, fill, 24);
            AddCircle(vh, filledEnd + new Vector2(-5f, 7f), radius * 0.13f,
                new Color(1f, 0.92f, 0.79f, 0.52f), 16);
        }

        AddCircle(vh, start, radius, dark, 28);
        AddCircle(vh, start, radius * 0.82f, lacquerColor, 28);
        AddCircle(vh, end, radius * 0.72f, dark, 28);
        DrawRing(vh, end, radius * 0.52f, 3f, new Color(1f, 0.91f, 0.76f, 0.72f), 24);

        // 多个小箭头提示必须从左向右拖动。
        for (int i = 1; i <= 3; i++)
        {
            float t = i / 4f;
            Vector2 center = Vector2.Lerp(start, end, t);
            DrawChevron(vh, center, radius * 0.35f, new Color(1f, 0.94f, 0.84f, 0.72f));
        }
    }

    static Color Darken(Color color, float multiplier)
    {
        return new Color(color.r * multiplier, color.g * multiplier, color.b * multiplier, color.a);
    }

    static void DrawChevron(VertexHelper vh, Vector2 center, float size, Color color)
    {
        Vector2 backTop = center + new Vector2(-size * 0.55f, size);
        Vector2 tip = center + new Vector2(size * 0.65f, 0f);
        Vector2 backBottom = center + new Vector2(-size * 0.55f, -size);
        AddLine(vh, backTop, tip, 3f, color);
        AddLine(vh, tip, backBottom, 3f, color);
    }

    static void DrawRing(VertexHelper vh, Vector2 center, float radius, float width, Color color, int segments)
    {
        Vector2 previous = center + Vector2.right * radius;
        for (int i = 1; i <= segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            Vector2 current = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            AddLine(vh, previous, current, width, color);
            previous = current;
        }
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

    static void AddCircle(VertexHelper vh, Vector2 center, float radius, Color color, int segments)
    {
        int centerIndex = vh.currentVertCount;
        AddVert(vh, center, color);
        for (int i = 0; i <= segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            AddVert(vh, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color);
        }

        for (int i = 0; i < segments; i++)
            vh.AddTriangle(centerIndex, centerIndex + i + 1, centerIndex + i + 2);
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
