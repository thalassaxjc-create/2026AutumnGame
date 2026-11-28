using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class PaperInspectionGraphic : MaskableGraphic
{
    static readonly Color32 Desk = new(51, 36, 27, 255);
    static readonly Color32 Shade = new(155, 137, 110, 255);
    static readonly Color32 Light = new(253, 236, 193, 255);
    static readonly Color32 Fiber = new(117, 90, 54, 35);
    PaperInspectionRound.Sample sample;
    float lamp;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
        useLegacyMeshGeneration = false;
    }

    internal void LayPaper(PaperInspectionRound.Sample sheet)
    {
        sample = sheet;
        lamp = -.17f;
        SetVerticesDirty();
    }

    internal void MoveLamp(Vector2 pointer)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, pointer, null, out Vector2 point))
            return;
        Rect paper = PaperRect();
        if (!paper.Contains(point))
            return;
        float x = (point.x - paper.xMin) / paper.width;
        if (Mathf.Abs(x - lamp) < .001f)
            return;
        lamp = x;
        SetVerticesDirty();
    }

    Rect PaperRect()
    {
        Rect board = rectTransform.rect;
        return new Rect(board.xMin + board.width * .09f, board.yMin + board.height * .12f,
            board.width * .82f, board.height * .76f);
    }

    float Illumination(Vector2 point)
    {
        float distance = Mathf.Abs(point.x - lamp - (point.y - .5f) * .24f);
        return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.035f, .19f, distance));
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect bounds = rectTransform.rect;
        if (bounds.width <= 0f || bounds.height <= 0f)
            return;
        Rect paper = PaperRect();
        Fill(mesh, bounds, Desk);
        Fill(mesh, new Rect(bounds.xMin, bounds.yMin, bounds.width, bounds.height * .03f), WorkshopStoryUI.Wood);
        Fill(mesh, new Rect(paper.xMin + 7f, paper.yMin - 7f, paper.width, paper.height), new Color32(31, 22, 16, 255));

        const int columns = 36, rows = 7;
        for (int row = 0; row < rows; row++)
        {
            float y = row / (float)rows, top = (row + 1) / (float)rows;
            for (int column = 0; column < columns; column++)
            {
                float x = column / (float)columns, right = (column + 1) / (float)columns;
                AddPaperCell(mesh, paper, new Vector2(x, y), new Vector2(right, top));
            }
        }
        for (int i = 1; i < 45; i++)
        {
            float y = paper.yMin + paper.height * (i / 45f);
            float inset = (Mathf.Sin(i * 7.9f) + 1.2f) * paper.width * .026f;
            Fill(mesh, new Rect(paper.xMin + inset, y, paper.width - 2f * inset, i % 4 == 0 ? 1.3f : .6f), Fiber);
        }
        DrawDefect(mesh, paper);
    }

    void AddPaperCell(VertexHelper mesh, Rect paper, Vector2 min, Vector2 max)
    {
        int start = mesh.currentVertCount;
        Vector2 topLeft = new(min.x, max.y), bottomRight = new(max.x, min.y);
        mesh.AddVert(At(paper, min), Color32.Lerp(Shade, Light, Illumination(min)), Vector2.zero);
        mesh.AddVert(At(paper, topLeft), Color32.Lerp(Shade, Light, Illumination(topLeft)), Vector2.zero);
        mesh.AddVert(At(paper, max), Color32.Lerp(Shade, Light, Illumination(max)), Vector2.zero);
        mesh.AddVert(At(paper, bottomRight), Color32.Lerp(Shade, Light, Illumination(bottomRight)), Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start, start + 2, start + 3);
    }

    void DrawDefect(VertexHelper mesh, Rect paper)
    {
        Vector2 origin = At(paper, sample.Position);
        float strength = Illumination(sample.Position);
        if (strength < .04f)
            return;
        float size = paper.height;
        switch (sample.Defect)
        {
            case PaperInspectionRound.Condition.Crease:
                Vector2 direction = new Vector2(1f, sample.Slant).normalized * size * .20f;
                Line(mesh, origin - direction, origin + direction, size * .013f, Fade(118, 87, 60, strength * .82f));
                Line(mesh, origin - direction + Vector2.up * 3f, origin + direction + Vector2.up * 3f,
                    size * .008f, Fade(255, 250, 227, strength));
                break;
            case PaperInspectionRound.Condition.ThinSpot:
                Spot(mesh, origin, new Vector2(size * .17f, size * .116f), Fade(250, 234, 169, strength * .91f), sample.Slant);
                Spot(mesh, origin + Vector2.right * size * .025f, new Vector2(size * .122f, size * .078f),
                    Fade(255, 252, 220, strength), sample.Slant);
                break;
            case PaperInspectionRound.Condition.Pinholes:
                for (int i = 0; i < 4; i++)
                {
                    Vector2 hole = origin + new Vector2((i - 1.5f) * .052f, Mathf.Sin(i * 3.9f) * .049f) * size;
                    float radius = size * (.013f + i * .002f);
                    Spot(mesh, hole, Vector2.one * radius * 1.37f, Fade(118, 85, 53, strength), 0f);
                    Spot(mesh, hole, Vector2.one * radius, Fade(255, 253, 230, strength), 0f);
                }
                break;
        }
    }

    static Vector2 At(Rect rect, Vector2 position) => rect.min + Vector2.Scale(rect.size, position);
    static Color32 Fade(byte r, byte g, byte b, float alpha) => new(r, g, b, (byte)Mathf.RoundToInt(alpha * 255f));

    static void Spot(VertexHelper mesh, Vector2 center, Vector2 radius, Color32 color, float slant)
    {
        const int segments = 16;
        int first = mesh.currentVertCount;
        mesh.AddVert(center, color, Vector2.zero);
        for (int i = 0; i < segments; i++)
        {
            float angle = 2f * Mathf.PI * i / segments;
            float r = 1f + .14f * Mathf.Sin(angle * 3f) * slant;
            Vector2 offset = Vector2.Scale(radius * r, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
            mesh.AddVert(center + offset, color, Vector2.zero);
        }
        for (int i = 0; i < segments; i++)
            mesh.AddTriangle(first, first + 1 + i, first + 1 + (i + 1) % segments);
    }

    static void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color32 color)
    {
        Vector2 delta = b - a;
        Vector2 offset = new Vector2(-delta.y, delta.x).normalized * width * .5f;
        Quad(mesh, a - offset, a + offset, b + offset, b - offset, color);
    }

    static void Fill(VertexHelper mesh, Rect rect, Color32 color) =>
        Quad(mesh, rect.min, new Vector2(rect.xMin, rect.yMax), rect.max, new Vector2(rect.xMax, rect.yMin), color);

    static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 color)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(a, color, Vector2.zero);
        mesh.AddVert(b, color, Vector2.zero);
        mesh.AddVert(c, color, Vector2.zero);
        mesh.AddVert(d, color, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start, start + 2, start + 3);
    }
}
