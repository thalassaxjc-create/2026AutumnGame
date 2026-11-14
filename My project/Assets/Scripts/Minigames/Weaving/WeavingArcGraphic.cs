using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 命中时绘制短促的白色弧线：F 从左向右，J 从右向左，结点同时交叉。
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class WeavingArcGraphic : MaskableGraphic
{
    const float Duration = 0.34f;

    float elapsed = Duration;
    int direction = 1;
    bool knot;

    public void PlaySweep(int sweepDirection)
    {
        direction = sweepDirection >= 0 ? 1 : -1;
        knot = false;
        elapsed = 0f;
        SetVerticesDirty();
    }

    public void PlayKnot()
    {
        knot = true;
        elapsed = 0f;
        SetVerticesDirty();
    }

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    void Update()
    {
        if (elapsed >= Duration)
            return;

        elapsed = Mathf.Min(Duration, elapsed + Time.unscaledDeltaTime);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (elapsed >= Duration)
            return;

        float normalized = Mathf.Clamp01(elapsed / Duration);
        float head = 1f - Mathf.Pow(1f - normalized, 3f);
        Rect rect = GetPixelAdjustedRect();

        if (knot)
        {
            DrawSweep(vh, rect, head, 1);
            DrawSweep(vh, rect, head, -1);

            if (normalized > 0.48f)
            {
                float ringT = Mathf.InverseLerp(0.48f, 1f, normalized);
                float radius = Mathf.Lerp(12f, 34f, ringT);
                float alpha = 1f - ringT;
                DrawCircle(vh, rect.center, radius, 4f, new Color(1f, 0.98f, 0.88f, alpha));
                AddDiamond(vh, rect.center, Mathf.Lerp(13f, 5f, ringT),
                    new Color(1f, 0.96f, 0.75f, alpha));
            }
        }
        else
        {
            DrawSweep(vh, rect, head, direction);
        }
    }

    static void DrawSweep(VertexHelper vh, Rect rect, float head, int direction)
    {
        float tail = Mathf.Max(0f, head - 0.52f);
        const int segments = 26;
        Vector2 previous = Evaluate(rect, tail, direction);

        for (int i = 1; i <= segments; i++)
        {
            float t = Mathf.Lerp(tail, head, i / (float)segments);
            Vector2 current = Evaluate(rect, t, direction);
            float along = Mathf.InverseLerp(tail, Mathf.Max(tail + 0.001f, head), t);
            float alpha = Mathf.Lerp(0.05f, 0.95f, along);
            float width = Mathf.Lerp(2f, 7f, along);
            AddLine(vh, previous, current, width, new Color(1f, 0.99f, 0.92f, alpha));
            previous = current;
        }
    }

    static Vector2 Evaluate(Rect rect, float t, int direction)
    {
        if (direction < 0)
            t = 1f - t;

        Vector2 start = new Vector2(rect.xMin + rect.width * 0.08f, rect.yMin + rect.height * 0.24f);
        Vector2 control = new Vector2(rect.center.x, rect.yMax - rect.height * 0.08f);
        Vector2 end = new Vector2(rect.xMax - rect.width * 0.08f, rect.yMin + rect.height * 0.24f);
        float oneMinus = 1f - t;
        return oneMinus * oneMinus * start + 2f * oneMinus * t * control + t * t * end;
    }

    static void DrawCircle(VertexHelper vh, Vector2 center, float radius, float width, Color color)
    {
        const int segments = 24;
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

    static void AddDiamond(VertexHelper vh, Vector2 center, float radius, Color color)
    {
        int index = vh.currentVertCount;
        AddVert(vh, center + Vector2.left * radius, color);
        AddVert(vh, center + Vector2.up * radius, color);
        AddVert(vh, center + Vector2.right * radius, color);
        AddVert(vh, center + Vector2.down * radius, color);
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
