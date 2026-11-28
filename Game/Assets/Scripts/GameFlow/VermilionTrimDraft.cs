using UnityEngine;

public enum CurtainEdgeStyle { None, VermilionJade, PaleBamboo, LacquerRusset, DarkBrown }

internal readonly struct CurtainEdgePalette
{
    internal readonly string Name;
    internal readonly string Description;
    internal readonly Color32 Face;
    internal readonly Color32 Frame;
    internal readonly Color32 Edge;
    internal readonly Color32 Highlight;

    static readonly CurtainEdgePalette[] Palettes =
    {
        new("素帘", "", new(230, 222, 195, 255), new(93, 65, 49, 255), default, default),
        new("朱边青", "浅青帘面 · 朱红绸边", new(167, 192, 157, 255), new(95, 67, 49, 255),
            new(158, 57, 42, 255), new(228, 138, 104, 255)),
        new("浅竹黄", "浅竹黄帘面 · 原木框", new(233, 209, 145, 255), new(163, 113, 66, 255),
            new(184, 136, 80, 255), new(242, 197, 132, 255)),
        new("棕红", "棕红帘面 · 深木框", new(161, 94, 68, 255), new(61, 37, 30, 255),
            new(85, 51, 39, 255), new(171, 109, 77, 255)),
        new("深褐", "浅竹黄帘面 · 靛蓝包边", new(230, 208, 145, 255), new(65, 44, 37, 255),
            new(44, 64, 116, 255), new(120, 151, 202, 255))
    };

    internal static readonly CurtainEdgeStyle[] Choices =
    {
        CurtainEdgeStyle.VermilionJade, CurtainEdgeStyle.PaleBamboo,
        CurtainEdgeStyle.LacquerRusset, CurtainEdgeStyle.DarkBrown
    };

    CurtainEdgePalette(string name, string description, Color32 face, Color32 frame, Color32 edge, Color32 highlight)
    {
        Name = name;
        Description = description;
        Face = face;
        Frame = frame;
        Edge = edge;
        Highlight = highlight;
    }

    internal static bool IsFinish(CurtainEdgeStyle style) => style > CurtainEdgeStyle.None && (int)style < Palettes.Length;
    internal static CurtainEdgePalette Get(CurtainEdgeStyle style) => Palettes[(int)style];
}

/// <summary>The roller lays each straight edge and then follows the rounded fold into the next side.</summary>
public sealed class VermilionTrimDraft
{
    public const int SideCount = 4;
    public const int SectionsPerSide = 8;
    public const int SectionsPerCorner = 6;
    public const int StepsPerSide = SectionsPerSide + SectionsPerCorner;
    public const int TotalSteps = SideCount * StepsPerSide;
    const float CornerRadius = .115f;
    const float StraightTolerance = .055f;
    const float CornerTolerance = .024f;
    const float GripReach = .055f;

    static readonly Vector2[] Corners =
    {
        new(.055f, .945f), new(.945f, .945f), new(.945f, .055f), new(.055f, .055f)
    };

    Vector2 previous;

    public VermilionTrimDraft(CurtainEdgeStyle style = CurtainEdgeStyle.VermilionJade)
    {
        if (!CurtainEdgePalette.IsFinish(style))
            throw new System.ArgumentOutOfRangeException(nameof(style));
        Style = style;
    }

    public CurtainEdgeStyle Style { get; }
    public int LaidSteps { get; private set; }
    public int SealedCorners => LaidSteps / StepsPerSide;
    public int ActiveSide => Mathf.Min(SealedCorners, SideCount - 1);
    public bool Complete => LaidSteps == TotalSteps;
    public bool Turning => !Complete && LaidSteps % StepsPerSide >= SectionsPerSide;
    public bool Held { get; private set; }
    public Vector2 Front => GuidePoint(LaidSteps);
    public Vector2 Pointer => previous;

    public static Vector2 EdgePosition(int side, float along)
    {
        Vector2 start = Corners[side], end = Corners[(side + 1) % SideCount];
        Vector2 inset = (end - start).normalized * CornerRadius;
        return Vector2.LerpUnclamped(start + inset, end - inset, along);
    }

    public static Vector2 CornerPosition(int side, float along)
    {
        Vector2 end = Corners[(side + 1) % SideCount];
        Vector2 incoming = (end - Corners[side]).normalized;
        Vector2 outgoing = (Corners[(side + 2) % SideCount] - end).normalized;
        float angle = along * Mathf.PI * .5f;
        Vector2 center = end + (outgoing - incoming) * CornerRadius;
        return center + (incoming * Mathf.Sin(angle) - outgoing * Mathf.Cos(angle)) * CornerRadius;
    }

    public static Vector2 GuidePoint(int step)
    {
        int side = step / StepsPerSide % SideCount;
        int segment = step % StepsPerSide;
        return segment <= SectionsPerSide ? EdgePosition(side, segment / (float)SectionsPerSide)
            : CornerPosition(side, (segment - SectionsPerSide) / (float)SectionsPerCorner);
    }

    public bool TryGrip(Vector2 pointer)
    {
        if (Complete || Held || (pointer - Front).sqrMagnitude > GripReach * GripReach)
            return false;
        previous = pointer;
        Held = true;
        return true;
    }

    public bool Drag(Vector2 pointer)
    {
        if (!Held)
            return false;
        Vector2 motion = pointer - previous;
        float distanceSquared = motion.sqrMagnitude;
        if (distanceSquared < .00000001f)
            return false;

        int before = LaidSteps;
        float previousHit = 0f;
        while (!Complete)
        {
            Vector2 checkpoint = GuidePoint(LaidSteps + 1);
            Vector2 direction = checkpoint - Front;
            if (Vector2.Dot(pointer - checkpoint, direction) < -.00001f)
                break;

            float hit = Mathf.Clamp(Vector2.Dot(checkpoint - previous, motion) / distanceSquared, previousHit, 1f);
            float tolerance = Turning ? CornerTolerance : StraightTolerance;
            if ((previous + motion * hit - checkpoint).sqrMagnitude > tolerance * tolerance)
                break;
            LaidSteps++;
            previousHit = hit;
        }

        previous = pointer;
        if (Complete)
            Release();
        return before != LaidSteps;
    }

    public void Release() => Held = false;
}
