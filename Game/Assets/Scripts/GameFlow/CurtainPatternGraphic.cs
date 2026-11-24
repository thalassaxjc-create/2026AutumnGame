using UnityEngine;
using UnityEngine.UI;

/// <summary>The same bamboo panel draws the work in progress and the completed sample.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class CurtainPatternGraphic : MaskableGraphic
{
    public static readonly Color32 Jade = new(40, 113, 99, 255);
    public static readonly Color32 Vermilion = new(162, 66, 48, 255);
    static readonly Color32 NaturalThread = new(83, 77, 52, 255);
    static readonly Color32 LightBamboo = new(234, 215, 172, 255);
    static readonly Color32 DarkBamboo = new(217, 193, 147, 255);
    static readonly Color32 Wood = new(98, 66, 46, 255);
    static readonly Color32 Gold = new(247, 192, 74, 255);
    static readonly Color32 Pin = new(101, 84, 61, 255);

    CurtainPatternKind kind;
    CurtainPatternDraft draft;
    bool sample;
    bool showPins;

    protected override void Awake()
    {
        base.Awake();
        useLegacyMeshGeneration = false;
        raycastTarget = false;
    }

    public void Show(CurtainPatternKind pattern, CurtainPatternDraft work = null, bool isSample = false, bool showPins = true)
    {
        kind = pattern;
        draft = work;
        sample = isSample;
        this.showPins = showPins;
        SetVerticesDirty();
    }

    public Vector2 PinLocalPosition(int loop, int pin)
    {
        Rect board = ClothRect();
        Vector2 at = CurtainPatternDraft.PinPosition(loop, pin);
        return new Vector2(board.xMin + board.width * at.x, board.yMin + board.height * at.y);
    }

    public bool TryHitPin(Vector2 screenPoint, out int loop, out int pin)
    {
        loop = pin = -1;
        if (draft == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, screenPoint, null, out var pointer) || !rectTransform.rect.Contains(pointer))
            return false;

        float reach = Mathf.Clamp(ClothRect().height * .049f, 12f, 25f);
        float nearest = reach * reach;
        int last = draft.UsesVermilion ? CurtainPatternDraft.Frame : CurtainPatternDraft.TurtleCount - 1;
        for (int ring = draft.UsesVermilion ? CurtainPatternDraft.Frame : 0; ring <= last; ring++)
        {
            if (draft.IsDemonstration(ring) || draft.IsClosed(ring))
                continue;
            for (int hole = 0; hole < CurtainPatternDraft.PinCount(ring); hole++)
            {
                float distance = (pointer - PinLocalPosition(ring, hole)).sqrMagnitude;
                if (distance >= nearest)
                    continue;
                nearest = distance;
                loop = ring;
                pin = hole;
            }
        }
        return pin >= 0;
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect panel = rectTransform.rect;
        Rect cloth = ClothRect();
        if (cloth.width <= 0 || cloth.height <= 0)
            return;

        Box(mesh, panel, Wood);
        Box(mesh, cloth, LightBamboo);
        for (int i = 0; i < 30; i++)
        {
            float height = cloth.height / 30f;
            Box(mesh, new Rect(cloth.xMin, cloth.yMin + i * height, cloth.width, height * .18f), DarkBamboo);
        }
        for (int i = 1; i < 16; i++)
        {
            float x = cloth.xMin + cloth.width * i / 16f;
            Box(mesh, new Rect(x - .5f, cloth.yMin, 1f, cloth.height), new Color32(147, 115, 80, 65));
        }
        if (kind == CurtainPatternKind.None)
            return;

        float width = Mathf.Max(2f, cloth.height * .012f);
        Color32 thread = kind == CurtainPatternKind.Turtle ? NaturalThread : Jade;
        if (kind == CurtainPatternKind.Turtle && !sample && showPins)
            Line(mesh, new(cloth.center.x, cloth.yMin + 8f), new(cloth.center.x, cloth.yMax - 8f),
                width * .4f, new Color32(117, 92, 62, 105));
        if (kind == CurtainPatternKind.VermilionJade && (sample || draft?.UsesVermilion == true))
            DrawTails(mesh, width);

        for (int loop = 0; loop < CurtainPatternDraft.TurtleCount; loop++)
            DrawLoop(mesh, loop, width, thread);
        if (kind == CurtainPatternKind.VermilionJade)
            DrawLoop(mesh, CurtainPatternDraft.Frame, width, Vermilion);
    }

    void DrawTails(VertexHelper mesh, float width)
    {
        int frame = CurtainPatternDraft.Frame;
        for (int pin = CurtainPatternDraft.RightJoin; pin <= CurtainPatternDraft.LeftJoin; pin += 3)
        {
            Vector2 joint = PinLocalPosition(frame, pin);
            float inward = (pin == CurtainPatternDraft.RightJoin ? -1f : 1f) * ClothRect().width;
            Vector2 end = sample || draft.IsPressed(pin) ? joint : joint + new Vector2(-inward * .028f, width * 1.4f);
            Line(mesh, joint + new Vector2(inward * .13f, -width * 2.8f), end, width * .68f, Jade);
        }
    }

    void DrawLoop(VertexHelper mesh, int loop, float width, Color32 thread)
    {
        bool frame = loop == CurtainPatternDraft.Frame;
        bool demonstration = draft?.IsDemonstration(loop) == true;
        if (sample || demonstration)
        {
            if (demonstration)
                thread = new Color32(124, 111, 79, 255);
            for (int i = 0; i < CurtainPatternDraft.MainPins; i++)
                Line(mesh, PinLocalPosition(loop, i), PinLocalPosition(loop, (i + 1) % CurtainPatternDraft.MainPins),
                    width, thread);
            Diamond(mesh, PinLocalPosition(loop, 0), width * .94f, thread);
            if (frame)
            {
                Diamond(mesh, PinLocalPosition(loop, CurtainPatternDraft.LeftJoin), width, thread);
                Diamond(mesh, PinLocalPosition(loop, CurtainPatternDraft.RightJoin), width, thread);
            }
            return;
        }
        if (draft == null || (frame && !draft.UsesVermilion))
            return;

        var path = draft.Path(loop);
        for (int i = 1; i < path.Count; i++)
            Line(mesh, PinLocalPosition(loop, path[i - 1]), PinLocalPosition(loop, path[i]), width, thread);
        bool closed = draft.IsClosed(loop);
        if (closed)
            Diamond(mesh, PinLocalPosition(loop, 0), width, thread);
        if (closed && !frame || !frame && draft.UsesVermilion)
            return;

        bool active = draft.ActiveLoop == loop;
        int hint = loop == 1 ? draft.GuidedPin : -1;
        int pins = CurtainPatternDraft.PinCount(loop);
        for (int pin = 0; pin < pins; pin++)
        {
            Vector2 at = PinLocalPosition(loop, pin);
            if (frame && draft.IsPressed(pin))
                Diamond(mesh, at, width * 1.35f, Vermilion);
            else if (!closed && showPins)
            {
                bool start = pin == 0 && draft.CanStart(loop);
                bool current = active && draft.CurrentPin == pin;
                bool highlighted = pin == hint || current || start;
                if (highlighted)
                    Diamond(mesh, at, width * 1.78f, Gold);
                Diamond(mesh, at, highlighted ? width * .93f : width * .63f, highlighted ? thread : Pin);
            }
        }
    }

    Rect ClothRect()
    {
        Rect rect = rectTransform.rect;
        float inset = Mathf.Max(3f, rect.height * .022f);
        return new Rect(rect.x + inset, rect.y + inset, rect.width - 2f * inset, rect.height - 2f * inset);
    }

    static void Box(VertexHelper mesh, Rect rect, Color32 tint) =>
        Quad(mesh, new(rect.xMin, rect.yMin), new(rect.xMin, rect.yMax), new(rect.xMax, rect.yMax),
            new(rect.xMax, rect.yMin), tint);

    static void Diamond(VertexHelper mesh, Vector2 center, float size, Color32 tint) =>
        Quad(mesh, center + Vector2.left * size, center + Vector2.up * size,
            center + Vector2.right * size, center + Vector2.down * size, tint);

    static void Line(VertexHelper mesh, Vector2 start, Vector2 end, float width, Color32 tint)
    {
        Vector2 normal = new Vector2(-(end - start).y, (end - start).x).normalized * (width * .5f);
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
