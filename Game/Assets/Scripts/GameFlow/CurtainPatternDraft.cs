using System;
using UnityEngine;

public enum CurtainPatternKind { None, Turtle, SingleThread, DoubleThread, Ribbed, TiedFlower }

/// <summary>Records the shuttle position and the fixed scoring points on each pass.</summary>
public sealed class CurtainPatternDraft
{
    public enum CrossingResult { Pending, Correct, Wrong }

    public const int RowCount = 3;
    public const int CrossingsPerRow = 4;
    public const int SamplesPerRow = 8;
    internal const float PrecisionHalfWidth = .115f;
    const float GripReach = .052f;
    const float GripHeight = .24f;
    const float EndOfPass = .998f;

    sealed class Pass
    {
        public Vector2 Shuttle;
        public readonly CrossingResult[] Crossings = new CrossingResult[CrossingsPerRow];
        public int CheckedCrossings;
        public int CheckedSamples;
        public int AlignedSamples;

        public void Reset(Vector2 start)
        {
            Shuttle = start;
            Array.Clear(Crossings, 0, Crossings.Length);
            CheckedCrossings = CheckedSamples = AlignedSamples = 0;
        }
    }

    readonly CurtainPatternDesign design;
    readonly Pass[] passes = { new(), new(), new() };
    Vector2 gripOffset;

    public CurtainPatternKind Kind { get; }
    public int CompletedRows { get; private set; }
    public bool CanConfirm => CompletedRows == RowCount;
    public bool CanRedo => CompletedRows > 0 || passes[0].Shuttle.x > 0f;
    public bool IsGripped { get; private set; }
    public Vector2 Shuttle => passes[Mathf.Min(CompletedRows, RowCount - 1)].Shuttle;

    public int ThreadingScore
    {
        get
        {
            int correct = 0;
            foreach (Pass pass in passes)
                foreach (CrossingResult crossing in pass.Crossings)
                    if (crossing == CrossingResult.Correct)
                        correct++;
            return correct * 5;
        }
    }

    public int AlignmentScore => 40 * (passes[0].AlignedSamples + passes[1].AlignedSamples + passes[2].AlignedSamples)
        / (RowCount * SamplesPerRow);
    public int Score => ThreadingScore + AlignmentScore;
    public WorkshopOrder.QualityGrade Grade => Score switch
    {
        >= 96 => WorkshopOrder.QualityGrade.S,
        >= 90 => WorkshopOrder.QualityGrade.A,
        >= 75 => WorkshopOrder.QualityGrade.B,
        >= 60 => WorkshopOrder.QualityGrade.C,
        _ => WorkshopOrder.QualityGrade.D
    };

    public CurtainPatternDraft(CurtainPatternKind kind)
    {
        design = CurtainPatternDesign.Get(kind) ?? throw new ArgumentOutOfRangeException(nameof(kind));
        Kind = kind;
        for (int row = 0; row < passes.Length; row++)
            passes[row].Shuttle = GuidePosition(row, 0f);
    }

    public static float CrossingProgress(int crossing) => (crossing + 1f) / (CrossingsPerRow + 1);
    static float SampleProgress(int sample) => (sample + 1f) / (SamplesPerRow + 1);
    public Vector2 GuidePosition(int row, float progress) => new(progress, design.GuideHeight(row, progress));
    public bool GoesUnder(int row, int crossing) => design.GoesUnder(row, crossing);
    public CrossingResult ResultAt(int row, int crossing) => passes[row].Crossings[crossing];
    public int CheckedCrossings(int row) => passes[row].CheckedCrossings;
    public float WorkedFraction(int row) => passes[row].Shuttle.x;

    /// <param name="pointer">Position relative to the active row, with X in the direction of travel.</param>
    public bool TryGrip(Vector2 pointer)
    {
        if (CanConfirm || IsGripped)
            return false;
        Vector2 offset = Shuttle - pointer;
        if (Mathf.Abs(offset.x) > GripReach || Mathf.Abs(offset.y) > GripHeight)
            return false;
        gripOffset = offset;
        IsGripped = true;
        return true;
    }

    public bool Drag(Vector2 pointer, bool under)
    {
        if (!IsGripped)
            return false;
        int row = CompletedRows;
        Pass pass = passes[row];
        Vector2 from = pass.Shuttle;
        Vector2 to = pointer + gripOffset;
        if (to.y < 0f || to.y > 1f)
        {
            Release();
            return false;
        }
        if (to.x < from.x || to == from)
            return false;

        // Stop at the clamp and score each fixed position crossed by this segment.
        if (to.x > 1f)
            to = Vector2.LerpUnclamped(from, to, (1f - from.x) / (to.x - from.x));
        if (to.x >= EndOfPass)
            to.x = 1f;
        while (pass.CheckedCrossings < CrossingsPerRow && CrossingProgress(pass.CheckedCrossings) <= to.x)
        {
            int crossing = pass.CheckedCrossings++;
            pass.Crossings[crossing] = under == GoesUnder(row, crossing) ? CrossingResult.Correct : CrossingResult.Wrong;
        }
        while (pass.CheckedSamples < SamplesPerRow && SampleProgress(pass.CheckedSamples) <= to.x)
        {
            float progress = SampleProgress(pass.CheckedSamples++);
            float height = Mathf.LerpUnclamped(from.y, to.y, (progress - from.x) / (to.x - from.x));
            if (Mathf.Abs(height - design.GuideHeight(row, progress)) <= PrecisionHalfWidth)
                pass.AlignedSamples++;
        }
        pass.Shuttle = to;
        if (to.x == 1f)
        {
            CompletedRows++;
            Release();
        }
        return true;
    }

    public void Release() => IsGripped = false;

    /// <summary>Keep hold of Ailin's shuttle while it moves. Wider than a normal grip.</summary>
    public bool TrySteady(Vector2 pointer)
    {
        if (CanConfirm || CompletedRows == 0)
            return false;
        Vector2 offset = Shuttle - pointer;
        if (Mathf.Abs(offset.x) > .16f || Mathf.Abs(offset.y) > .32f)
        {
            Release();
            return false;
        }
        if (!IsGripped)
        {
            gripOffset = Vector2.zero;
            IsGripped = true;
        }
        return true;
    }

    /// <summary>Move her shuttle forward along the row. Off the guide she drifts and misses the line.</summary>
    public void GlideLesson(float targetX, bool under, bool onGuide)
    {
        if (CanConfirm || CompletedRows == 0)
            return;
        int row = CompletedRows;
        targetX = Mathf.Clamp(Mathf.Max(targetX, Shuttle.x), 0f, 1f);
        if (targetX <= Shuttle.x + .0001f)
            return;
        if (!IsGripped && !TryGrip(Shuttle))
            return;
        gripOffset = Vector2.zero;
        Vector2 dest = GuidePosition(row, targetX);
        if (!onGuide)
            dest.y = Mathf.Clamp(dest.y + .22f, .02f, .98f);
        Drag(dest, under);
    }

    /// <summary>Ailin advances one crossing along the guide using the instruction she was just given.</summary>
    public bool TeachCrossing(bool under)
    {
        if (CanConfirm || IsGripped || CompletedRows == 0)
            return false;
        int row = CompletedRows;
        int crossing = passes[row].CheckedCrossings;
        if (crossing >= CrossingsPerRow)
            return false;
        float target = crossing == CrossingsPerRow - 1 ? 1f : CrossingProgress(crossing) + .012f;
        if (!TryGrip(Shuttle))
            return false;
        float start = Shuttle.x;
        const int steps = 16;
        for (int step = 1; step <= steps; step++)
        {
            float progress = Mathf.Lerp(start, target, step / (float)steps);
            if (Drag(GuidePosition(row, progress), under))
                continue;
            Release();
            return CompletedRows != row || passes[row].CheckedCrossings > crossing;
        }
        Release();
        return true;
    }

    public bool RedoRow()
    {
        if (!CanRedo)
            return false;
        Release();
        if (CanConfirm || Shuttle.x == 0f)
            CompletedRows--;
        passes[CompletedRows].Reset(GuidePosition(CompletedRows, 0f));
        return true;
    }
}
