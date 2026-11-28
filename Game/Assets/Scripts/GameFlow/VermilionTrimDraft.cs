using UnityEngine;

/// <summary>Fit one strip of red silk along each edge, then press the fold at its corner.</summary>
public sealed class VermilionTrimDraft
{
    public const int SideCount = 4;
    public const int SectionsPerSide = 8;
    public const float LaneTolerance = .061f;
    const float GripReach = .095f;

    static readonly Vector2[] Corners =
    {
        new(.038f, .962f), new(.962f, .962f), new(.962f, .038f), new(.038f, .038f)
    };

    int sections;
    Vector2 previous;
    public int SealedCorners { get; private set; }
    public int ActiveSide => SealedCorners;
    public bool Complete => SealedCorners == SideCount;
    public bool NeedsCorner => !Complete && sections == SectionsPerSide;
    public bool Held { get; private set; }
    public float EdgeProgress => sections / (float)SectionsPerSide;

    public static Vector2 EdgePosition(int side, float along) =>
        Vector2.LerpUnclamped(Corners[side], Corners[(side + 1) % SideCount], along);

    public float Coverage(int side) => side < ActiveSide ? 1f : side == ActiveSide ? EdgeProgress : 0f;

    public bool TryGrip(float along, float offset)
    {
        if (Complete || NeedsCorner || Held || Mathf.Abs(along - EdgeProgress) > GripReach ||
            Mathf.Abs(offset) > LaneTolerance)
            return false;
        // The leading edge must pass the next mark even if the grip is a little ahead of it.
        previous = new Vector2(Mathf.Min(along, EdgeProgress), offset);
        Held = true;
        return true;
    }

    public bool Drag(float along, float offset)
    {
        if (!Held)
            return false;
        if (along >= .988f && along < 1f)
            along = 1f;
        int before = sections;
        if (along > previous.x)
        {
            while (sections < SectionsPerSide)
            {
                float checkpoint = (sections + 1) / (float)SectionsPerSide;
                if (checkpoint < previous.x || checkpoint > along)
                    break;
                float error = Mathf.Abs(Mathf.Lerp(previous.y, offset, (checkpoint - previous.x) / (along - previous.x)));
                if (error > LaneTolerance)
                    break;
                sections++;
            }
        }
        previous = new Vector2(along, offset);
        if (NeedsCorner)
            Release();
        return sections != before;
    }

    public void Release() => Held = false;

    public bool TryPressCorner(float along, float offset)
    {
        if (!NeedsCorner || Mathf.Abs(1f - along) > GripReach || Mathf.Abs(offset) > LaneTolerance)
            return false;
        SealedCorners++;
        sections = 0;
        return true;
    }
}
