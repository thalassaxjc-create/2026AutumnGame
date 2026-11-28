using UnityEngine;
using UnityEngine.UI;

/// <summary>Renders a curtain in swatches, pattern work and edge binding.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class CurtainPatternGraphic : MaskableGraphic
{
    public static readonly Color32 Vermilion = new(158, 57, 42, 255);
    static readonly Color32 Paper = new(230, 222, 195, 255);
    static readonly Color32 Grain = new(173, 153, 113, 62);
    static readonly Color32 Seam = new(105, 103, 76, 192);
    static readonly Color32 Facet = new(132, 120, 87, 124);
    static readonly Color32 GuideSeam = new(131, 115, 87, 43);
    static readonly Color32 GuideFacet = new(131, 115, 87, 29);
    static readonly Color32 DarkWood = new(94, 77, 55, 255);
    static readonly Color32 Gold = new(251, 193, 78, 255);
    static readonly Color32 Track = new(77, 115, 97, 38);
    static readonly Color32 PrecisionTrack = new(77, 115, 97, 72);
    static readonly Color32 Bamboo = new(165, 126, 82, 255);
    static readonly Color32 Thread = new(46, 94, 76, 255);
    static readonly Color32 ShuttleWood = new(152, 96, 55, 255);

    CurtainPatternDesign design;
    CurtainPatternDraft draft;
    VermilionTrimDraft trim;
    bool sample;
    bool guide;
    bool shuttleUnder;
    CurtainEdgeStyle edgeStyle;

    protected override void Awake()
    {
        base.Awake();
        useLegacyMeshGeneration = false;
        raycastTarget = false;
    }

    public void Show(CurtainPatternKind pattern, CurtainPatternDraft work = null, bool isSample = false,
        bool showGuide = true, CurtainEdgeStyle finish = CurtainEdgeStyle.None, VermilionTrimDraft edgeWork = null)
    {
        design = CurtainPatternDesign.Get(pattern);
        draft = work;
        trim = edgeWork;
        sample = isSample;
        guide = showGuide;
        shuttleUnder = false;
        edgeStyle = edgeWork?.Style ?? finish;
        SetVerticesDirty();
    }

    public Vector2 ClothPoint(Vector2 normalized) => At(ClothRect(), normalized);

    public Vector2 ShuttleLocalPosition(int row, Vector2 travel) => ClothPoint(CurtainPatternDesign.ClothPosition(row, travel));

    public bool TryShuttlePointer(Vector2 screenPoint, int row, out Vector2 travel)
    {
        travel = default;
        if (!TryClothPointer(screenPoint, out Vector2 point))
            return false;
        travel = CurtainPatternDesign.TravelPosition(row, point);
        return true;
    }

    internal bool TryClothPointer(Vector2 screenPoint, out Vector2 point)
    {
        point = default;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, null, out Vector2 local))
            return false;
        Rect cloth = ClothRect();
        if (cloth.width <= 0f || cloth.height <= 0f)
            return false;
        point = new Vector2((local.x - cloth.xMin) / cloth.width, (local.y - cloth.yMin) / cloth.height);
        return true;
    }

    internal void RefreshShuttle(bool under)
    {
        shuttleUnder = under;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect panel = rectTransform.rect;
        Rect cloth = ClothRect();
        if (cloth.width <= 0f || cloth.height <= 0f)
            return;

        CurtainEdgePalette palette = CurtainEdgePalette.Get(edgeStyle);
        Box(mesh, panel, palette.Frame);
        Box(mesh, cloth, palette.Face);
        float grainPitch = cloth.height / 57f;
        for (int i = 1; i < 57; i++)
            Box(mesh, new Rect(cloth.xMin, cloth.yMin + i * grainPitch, cloth.width, grainPitch * (i % 4 == 0 ? .28f : .14f)),
                Grain);
        if (design != null)
            DrawPattern(mesh, cloth);
        if (edgeStyle != CurtainEdgeStyle.None)
            DrawEdge(mesh, cloth, palette);
        if (draft != null)
        {
            if (guide && !draft.CanConfirm)
                DrawShuttleGuide(mesh, cloth);
            DrawMisplacedKnots(mesh, cloth);
        }
    }

    void DrawPattern(VertexHelper mesh, Rect cloth)
    {
        float width = Mathf.Max(.8f, Mathf.Min(cloth.width, cloth.height) * .006f);
        for (int row = 0; row < design.Bands.Length; row++)
        {
            float fraction = sample ? 1f : draft?.WorkedFraction(row) ?? 0f;
            Rect band = CurtainPatternDesign.RowBounds(row);
            float frontier = CurtainPatternDesign.ClothPosition(row, new Vector2(fraction, 0f)).x;
            bool reversed = CurtainPatternDesign.IsReverse(row);
            Rect worked = Rect.MinMaxRect(reversed ? frontier : 0f, band.yMin, reversed ? 1f : frontier, band.yMax);
            foreach (CurtainPatternDesign.Stroke stroke in design.Bands[row])
            {
                float thickness = stroke.Fine ? width * .47f : width;
                Vector2 start = stroke.Start;
                Vector2 end = stroke.End;
                float left = Mathf.Min(start.x, end.x);
                float right = Mathf.Max(start.x, end.x);
                Color32 color = stroke.Fine ? Facet : Seam;
                if (fraction >= 1f || (fraction > 0f && left >= worked.xMin && right <= worked.xMax))
                {
                    Line(mesh, At(cloth, start), At(cloth, end), thickness, color);
                    continue;
                }
                if (guide && draft != null)
                    Line(mesh, At(cloth, start), At(cloth, end), thickness, stroke.Fine ? GuideFacet : GuideSeam);
                if (fraction > 0f && left < worked.xMax && right > worked.xMin &&
                    CurtainPatternDesign.Clip(ref start, ref end, worked))
                    Line(mesh, At(cloth, start), At(cloth, end), thickness, color);
            }
        }
    }

    void DrawShuttleGuide(VertexHelper mesh, Rect cloth)
    {
        int row = draft.CompletedRows;
        float rowHeight = cloth.height / CurtainPatternDraft.RowCount;
        float inner = rowHeight * CurtainPatternDraft.PrecisionHalfWidth;
        Vector2 start = ShuttleLocalPosition(row, draft.GuidePosition(row, 0f));
        Vector2 end = start;
        const int segments = 32;
        for (int i = 1; i <= segments; i++)
        {
            Vector2 next = ShuttleLocalPosition(row, draft.GuidePosition(row, i / (float)segments));
            Rail(mesh, end, next, rowHeight * .23f, Track);
            Rail(mesh, end, next, inner, PrecisionTrack);
            end = next;
        }
        float marker = rowHeight * .058f;
        Mark(mesh, start, marker, Bamboo);
        Mark(mesh, end, marker * 1.1f, Gold);

        int nextCrossing = draft.CheckedCrossings(row);
        for (int crossing = 0; crossing < CurtainPatternDraft.CrossingsPerRow; crossing++)
        {
            float progress = CurtainPatternDraft.CrossingProgress(crossing);
            Vector2 center = ShuttleLocalPosition(row, draft.GuidePosition(row, progress));
            var result = draft.ResultAt(row, crossing);
            bool active = crossing == nextCrossing;
            Color32 fiber = active ? Gold : Bamboo;
            bool under = result == CurtainPatternDraft.CrossingResult.Pending ? active && shuttleUnder :
                (result == CurtainPatternDraft.CrossingResult.Correct) == draft.GoesUnder(row, crossing);
            Vector2 warp = Vector2.up * (inner * (under ? 1.9f : 1.45f));
            Vector2 weft = Vector2.right * (marker * 1.24f);
            float line = Mathf.Max(1.3f, marker * .40f);
            if (under)
                Line(mesh, center - weft, center + weft, line, Thread);
            Line(mesh, center - warp, center + warp, line, fiber);
            if (!under)
                Line(mesh, center - weft, center + weft, line, Thread);
        }

        Vector2 shuttle = ShuttleLocalPosition(row, draft.Shuttle);
        Vector2 shaft = Vector2.right * (rowHeight * .23f);
        Vector2 halfHeight = Vector2.up * (rowHeight * .082f);
        Quad(mesh, shuttle - shaft, shuttle - shaft * .55f + halfHeight, shuttle + shaft * .55f + halfHeight,
            shuttle + shaft, ShuttleWood);
        Quad(mesh, shuttle - shaft, shuttle + shaft, shuttle + shaft * .55f - halfHeight,
            shuttle - shaft * .55f - halfHeight, DarkWood);
        Line(mesh, shuttle - shaft * .45f, shuttle + shaft * .45f, marker * .57f, shuttleUnder ? Gold : Paper);
    }

    void DrawMisplacedKnots(VertexHelper mesh, Rect cloth)
    {
        float size = cloth.height * .014f;
        for (int row = 0; row < CurtainPatternDraft.RowCount; row++)
            for (int crossing = 0; crossing < draft.CheckedCrossings(row); crossing++)
            {
                if (draft.ResultAt(row, crossing) != CurtainPatternDraft.CrossingResult.Wrong)
                    continue;
                Vector2 knot = ShuttleLocalPosition(row, draft.GuidePosition(row, CurtainPatternDraft.CrossingProgress(crossing)));
                Mark(mesh, knot, size, Vermilion);
                Vector2 upRight = new Vector2(size, size) * .6f;
                Vector2 downRight = new Vector2(size, -size) * .6f;
                Line(mesh, knot - upRight, knot + upRight, size * .22f, Paper);
                Line(mesh, knot - downRight, knot + downRight, size * .22f, Paper);
            }
    }

    static void Rail(VertexHelper mesh, Vector2 start, Vector2 end, float halfWidth, Color32 color)
    {
        Vector2 up = Vector2.up * halfWidth;
        Quad(mesh, start - up, start + up, end + up, end - up, color);
    }

    void DrawEdge(VertexHelper mesh, Rect cloth, CurtainEdgePalette palette)
    {
        float width = Mathf.Min(cloth.width, cloth.height) * .036f;
        int placed = trim?.LaidSteps ?? VermilionTrimDraft.TotalSteps;
        Vector2 start = At(cloth, VermilionTrimDraft.GuidePoint(0));
        for (int step = 1; step <= VermilionTrimDraft.TotalSteps; step++)
        {
            Vector2 end = At(cloth, VermilionTrimDraft.GuidePoint(step));
            if (step <= placed)
            {
                Line(mesh, start, end, width * 1.18f, palette.Frame);
                Line(mesh, start, end, width, palette.Edge);
                Line(mesh, start, end, width * .18f, palette.Highlight);
                Diamond(mesh, end, width * .45f, palette.Edge);
            }
            else
            {
                Line(mesh, start, end, width * .72f, new Color32(112, 89, 65, 126));
                if (step <= placed + 3)
                    Mark(mesh, end, width * .38f, Gold);
            }
            start = end;
        }
        if (trim == null || trim.Complete)
            return;

        Vector2 front = At(cloth, trim.Front);
        Mark(mesh, front, width * .70f, Gold);
        Vector2 cursor = trim.Pointer;
        if (!trim.Held || cursor.x < 0 || cursor.x > 1 || cursor.y < 0 || cursor.y > 1)
            return;
        Vector2 roller = At(cloth, cursor);
        Vector2 tangent = (At(cloth, VermilionTrimDraft.GuidePoint(placed + 1)) - front).normalized;
        Vector2 grip = new(-tangent.y, tangent.x);
        Line(mesh, roller - grip * width * .87f, roller + grip * width * 1.3f, width * .54f, DarkWood);
        Line(mesh, roller - grip * width * .74f, roller + grip * width * .70f, width * .95f, palette.Edge);
        Line(mesh, roller - grip * width * .62f, roller + grip * width * .56f, width * .30f, palette.Highlight);
    }

    Rect ClothRect()
    {
        Rect board = rectTransform.rect;
        float inset = Mathf.Min(board.width, board.height) * .028f;
        return Rect.MinMaxRect(board.xMin + inset, board.yMin + inset, board.xMax - inset, board.yMax - inset);
    }

    static Vector2 At(Rect rect, Vector2 point) =>
        new(rect.xMin + rect.width * point.x, rect.yMin + rect.height * point.y);

    static void Mark(VertexHelper mesh, Vector2 position, float radius, Color32 tint)
    {
        Diamond(mesh, position, radius, DarkWood);
        Diamond(mesh, position, radius * .67f, tint);
    }

    static void Box(VertexHelper mesh, Rect rect, Color32 tint) =>
        Quad(mesh, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin, rect.yMax),
            new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMax, rect.yMin), tint);

    static void Diamond(VertexHelper mesh, Vector2 center, float size, Color32 tint) =>
        Quad(mesh, center + Vector2.left * size, center + Vector2.up * size,
            center + Vector2.right * size, center + Vector2.down * size, tint);

    static void Line(VertexHelper mesh, Vector2 start, Vector2 end, float width, Color32 tint)
    {
        Vector2 delta = end - start;
        Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (width * .5f);
        Quad(mesh, start - normal, start + normal, end + normal, end - normal, tint);
    }

    static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 tint)
    {
        int index = mesh.currentVertCount;
        mesh.AddVert(a, tint, Vector2.zero);
        mesh.AddVert(b, tint, Vector2.zero);
        mesh.AddVert(c, tint, Vector2.zero);
        mesh.AddVert(d, tint, Vector2.zero);
        mesh.AddTriangle(index, index + 1, index + 2);
        mesh.AddTriangle(index, index + 2, index + 3);
    }
}
