using UnityEngine;
using UnityEngine.UI;

/// <summary>剑门、钢片、钳子与张力条共用 UI 网格，不生成贴图或独立材质。</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class BambooFinishingGraphic : MaskableGraphic
{
    public BambooCraftProgress Progress { get; set; }
    static readonly Color Steel = new Color(0.68f, 0.74f, 0.72f);
    static readonly Color SteelDark = new Color(0.23f, 0.3f, 0.29f);
    static readonly Color Bamboo = new Color(0.96f, 0.83f, 0.48f);
    static readonly Color Red = new Color(0.83f, 0.22f, 0.14f);
    static readonly Color Green = new Color(0.48f, 0.74f, 0.42f);

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (Progress == null) return;
        if (Progress.IsGateStage) DrawGate(vh, rectTransform.rect);
        else DrawPulling(vh, rectTransform.rect);
    }

    void DrawGate(VertexHelper vh, Rect r)
    {
        float height = Progress.GateHeight;
        float lower = height - BambooCraftProgress.GateHalfOpening;
        float upper = height + BambooCraftProgress.GateHalfOpening;
        float gateX = BambooCraftProgress.GateX;
        Quad(vh, Area(r, gateX - 0.04f, lower, 0.08f, upper - lower), new Color(0.52f, 0.78f, 0.46f, 0.15f));
        Quad(vh, Area(r, gateX - 0.035f, 0, 0.07f, lower), SteelDark);
        Quad(vh, Area(r, gateX - 0.035f, upper, 0.07f, 1 - upper), SteelDark);
        Quad(vh, Area(r, gateX - 0.025f, 0, 0.05f, lower), Steel);
        Quad(vh, Area(r, gateX - 0.025f, upper, 0.05f, 1 - upper), Steel);
        Color edge = Progress.GateDamaged ? Red : new Color(0.94f, 0.96f, 0.9f);
        Quad(vh, Area(r, gateX - 0.035f, lower - 0.018f, 0.07f, 0.018f), edge);
        Quad(vh, Area(r, gateX - 0.035f, upper, 0.07f, 0.018f), edge);
        for (int i = 0; i < 32; i++)
            Quad(vh, Area(r, i / 32f, height - 0.0025f, 0.018f, 0.005f), new Color(0.78f, 0.85f, 0.59f, 0.45f));

        Vector2 center = Point(r, Progress.PieceX, Progress.PieceY);
        float angle = Progress.PieceTilt * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        Vector2 normal = new Vector2(-direction.y, direction.x);
        float halfLength = r.width * BambooCraftProgress.PieceHalfLength;
        float halfHeight = r.height * BambooCraftProgress.PieceHalfHeight;
        Vector2 a = center - direction * halfLength;
        Vector2 b = center + direction * halfLength;
        Line(vh, a, b, halfHeight * 2, Bamboo);
        Line(vh, a + normal * halfHeight * 0.3f, b + normal * halfHeight * 0.3f, 1.3f, new Color(0.7f, 0.54f, 0.22f));
        if (Progress.GateSourceDefective)
            for (int i = -1; i <= 1; i++)
                Line(vh, center + direction * (i * 18 - 5) - normal * halfHeight,
                    center + direction * (i * 18 + 5) + normal * halfHeight, 2, new Color(0.46f, 0.25f, 0.12f));
        if (Progress.GateDamaged)
        {
            Vector2 notch = center + direction * halfLength * 0.5f;
            Triangle(vh, notch - direction * 12 + normal * halfHeight,
                notch + direction * 12 + normal * halfHeight, notch - normal * halfHeight * 0.65f,
                new Color(0.57f, 0.39f, 0.24f));
            Line(vh, notch - direction * 11 + normal * halfHeight, notch - normal * halfHeight * 0.65f, 2, Red);
        }
        Color level = Mathf.Abs(Progress.PieceTilt) <= BambooCraftProgress.SafeGateTilt ? Green : new Color(1f, 0.7f, 0.3f);
        Line(vh, center + new Vector2(-20, -30), center + new Vector2(20, -30), 3, level);
    }

    void DrawPulling(VertexHelper vh, Rect r)
    {
        Vector2 ring = Point(r, 0.5f, 0.65f);
        float thickness = Mathf.Lerp(16, 3, Progress.DrawProgress);
        Vector2 grip = Point(r, 0.74f + Progress.DrawProgress * 0.13f, 0.65f);
        Line(vh, Point(r, 0.05f, 0.65f), ring, 17, Bamboo);
        Line(vh, ring, grip, thickness, Bamboo);
        for (int i = 0; i < 4; i++)
            Line(vh, Point(r, 0.05f, 0.65f) + Vector2.up * (i * 4 - 6), ring + Vector2.up * (i * 4 - 6), 1,
                new Color(0.62f, 0.49f, 0.24f));
        Ring(vh, ring, 29, 18, Steel);
        Ring(vh, ring, 31, 29, SteelDark);
        // Steel jaws pinch the emerging strand; handles move with the pull distance.
        Line(vh, grip + new Vector2(-8, 10), grip + new Vector2(24, 0), 9, Steel);
        Line(vh, grip + new Vector2(-8, -10), grip + new Vector2(24, 0), 9, Steel);
        Line(vh, grip + new Vector2(24, 0), grip + new Vector2(60, 25), 12, SteelDark);
        Line(vh, grip + new Vector2(24, 0), grip + new Vector2(60, -25), 12, SteelDark);

        Rect bar = Area(r, 0.08f, 0.12f, 0.84f, 0.09f);
        Quad(vh, new Rect(bar.x - 3, bar.y - 3, bar.width + 6, bar.height + 6), SteelDark);
        Quad(vh, bar, new Color(0.86f, 0.81f, 0.64f));
        Quad(vh, new Rect(bar.x, bar.y, bar.width * Progress.Tension, bar.height),
            Progress.DrawNeedsRelease ? Red : Green);
        if (Progress.Stage == BambooCraftProgress.Step.Drawing && (Progress.DangerWarning || Progress.DangerActive))
        {
            Color danger = Progress.DangerWarning ? new Color(1f, 0.67f, 0.22f, 0.65f) : new Color(0.88f, 0.13f, 0.12f, 0.86f);
            Rect zone = new Rect(bar.x + bar.width * Progress.DangerMin, bar.y, bar.width * 0.16f, bar.height);
            Quad(vh, zone, danger);
            for (int i = 1; i < 5; i++)
                Line(vh, new Vector2(zone.x + zone.width * i / 5f, zone.y),
                    new Vector2(zone.x + zone.width * i / 5f - 9, zone.yMax), 2, new Color(1, 1, 1, 0.4f));
        }
        float marker = bar.x + bar.width * Progress.Tension;
        Quad(vh, new Rect(marker - 2, bar.y - 5, 4, bar.height + 10), Color.white);
    }

    static Rect Area(Rect r, float x, float y, float w, float h) => new Rect(r.x + x * r.width, r.y + y * r.height, w * r.width, h * r.height);
    static Vector2 Point(Rect r, float x, float y) => new Vector2(r.x + x * r.width, r.y + y * r.height);
    static void Quad(VertexHelper vh, Rect r, Color color)
    {
        int i = vh.currentVertCount;
        vh.AddVert(new Vector2(r.xMin, r.yMin), color, Vector2.zero);
        vh.AddVert(new Vector2(r.xMin, r.yMax), color, Vector2.zero);
        vh.AddVert(new Vector2(r.xMax, r.yMax), color, Vector2.zero);
        vh.AddVert(new Vector2(r.xMax, r.yMin), color, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i + 2, i + 3, i);
    }
    static void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        int i = vh.currentVertCount;
        vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero); vh.AddVert(c, color, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2);
    }
    static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
    {
        Vector2 d = (b - a).normalized;
        Vector2 n = new Vector2(-d.y, d.x) * width * 0.5f;
        int i = vh.currentVertCount;
        vh.AddVert(a - n, color, Vector2.zero); vh.AddVert(a + n, color, Vector2.zero);
        vh.AddVert(b + n, color, Vector2.zero); vh.AddVert(b - n, color, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i + 2, i + 3, i);
    }
    static void Ring(VertexHelper vh, Vector2 center, float outer, float inner, Color color)
    {
        for (int i = 0; i < 32; i++)
        {
            float a = i * Mathf.PI * 2 / 32;
            float b = (i + 1) * Mathf.PI * 2 / 32;
            Vector2 da = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 db = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
            int v = vh.currentVertCount;
            vh.AddVert(center + da * inner, color, Vector2.zero); vh.AddVert(center + da * outer, color, Vector2.zero);
            vh.AddVert(center + db * outer, color, Vector2.zero); vh.AddVert(center + db * inner, color, Vector2.zero);
            vh.AddTriangle(v, v + 1, v + 2); vh.AddTriangle(v + 2, v + 3, v);
        }
    }
}
