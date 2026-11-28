using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared linework for selector swatches, the workbench and the order sample.</summary>
internal sealed class CurtainPatternDesign
{
    internal readonly struct Stroke
    {
        public readonly Vector2 Start;
        public readonly Vector2 End;
        public readonly bool Fine;

        public Stroke(Vector2 start, Vector2 end, bool fine)
        {
            Start = start;
            End = end;
            Fine = fine;
        }
    }

    internal static readonly IReadOnlyList<CurtainPatternKind> Choices = Array.AsReadOnly(new[]
    {
        CurtainPatternKind.SingleThread, CurtainPatternKind.DoubleThread, CurtainPatternKind.Ribbed,
        CurtainPatternKind.Turtle, CurtainPatternKind.TiedFlower
    });

    // A crosses above the bamboo and B goes below. Read each row in the direction of travel.
    static readonly CurtainPatternDesign Single = new("单丝路", "ABABABABABAB",
        new[] { .50f, .50f, .50f, .50f, .50f, .50f, .50f }, MakeThreadLines(13, 0f));
    static readonly CurtainPatternDesign Double = new("双丝路", "AABBAABBAABB",
        new[] { .42f, .55f, .42f, .55f, .42f, .55f, .42f }, MakeThreadLines(11, .017f));
    static readonly CurtainPatternDesign Ribbed = new("罗纹", "AABBBBAAAABB",
        new[] { .45f, .55f, .55f, .45f, .45f, .55f, .55f }, MakeThreadLines(43, 0f));
    static readonly CurtainPatternDesign Turtle = new("龟纹", "ABBABAABABBA",
        new[] { .48f, .66f, .66f, .38f, .38f, .64f, .48f }, MakeTurtleScutes());
    static readonly CurtainPatternDesign Flower = new("扎花", "BAABABBABAAB",
        new[] { .49f, .69f, .50f, .30f, .49f, .69f, .49f }, MakeFlowers());

    const float EdgeInset = .075f;
    readonly float[] guide;
    readonly string crossings;
    internal string Name { get; }
    internal Stroke[][] Bands { get; }

    CurtainPatternDesign(string name, string crossings, float[] guide, List<Stroke> strokes)
    {
        Name = name;
        this.guide = guide;
        this.crossings = crossings;
        Bands = new Stroke[CurtainPatternDraft.RowCount][];
        for (int row = 0; row < Bands.Length; row++)
        {
            Rect band = RowBounds(row);
            var pieces = new List<Stroke>();
            foreach (Stroke stroke in strokes)
            {
                Vector2 start = stroke.Start;
                Vector2 end = stroke.End;
                if (Clip(ref start, ref end, band))
                    pieces.Add(new Stroke(start, end, stroke.Fine));
            }
            Bands[row] = pieces.ToArray();
        }
    }

    internal static CurtainPatternDesign Get(CurtainPatternKind kind) => kind switch
    {
        CurtainPatternKind.SingleThread => Single,
        CurtainPatternKind.DoubleThread => Double,
        CurtainPatternKind.Ribbed => Ribbed,
        CurtainPatternKind.Turtle => Turtle,
        CurtainPatternKind.TiedFlower => Flower,
        _ => null
    };

    internal static Rect RowBounds(int row)
    {
        float height = 1f / CurtainPatternDraft.RowCount;
        return new Rect(0f, (CurtainPatternDraft.RowCount - 1 - row) * height, 1f, height);
    }

    internal bool GoesUnder(int row, int crossing) =>
        crossings[row * CurtainPatternDraft.CrossingsPerRow + crossing] == 'B';

    internal static bool IsReverse(int row) => (row & 1) != 0;

    internal static Vector2 ClothPosition(int row, Vector2 travel)
    {
        Rect band = RowBounds(row);
        float across = IsReverse(row) ? 1f - travel.x : travel.x;
        return new Vector2(Mathf.LerpUnclamped(EdgeInset, 1f - EdgeInset, across),
            band.yMin + travel.y * band.height);
    }

    internal static Vector2 TravelPosition(int row, Vector2 cloth)
    {
        Rect band = RowBounds(row);
        float across = (cloth.x - EdgeInset) / (1f - 2f * EdgeInset);
        return new Vector2(IsReverse(row) ? 1f - across : across, (cloth.y - band.yMin) / band.height);
    }

    internal float GuideHeight(int row, float progress)
    {
        float across = IsReverse(row) ? 1f - progress : progress;
        float step = Mathf.Clamp01(across) * (guide.Length - 1);
        int left = Mathf.Min((int)step, guide.Length - 2);
        return Mathf.SmoothStep(guide[left], guide[left + 1], step - left);
    }

    static List<Stroke> MakeThreadLines(int count, float pairGap)
    {
        var strokes = new List<Stroke>(count * 2);
        for (int thread = 0; thread < count; thread++)
        {
            float x = (thread + .5f) / count;
            strokes.Add(new Stroke(new Vector2(x - pairGap * .5f, 0f), new Vector2(x - pairGap * .5f, 1f), false));
            if (pairGap > 0f)
                strokes.Add(new Stroke(new Vector2(x + pairGap * .5f, 0f), new Vector2(x + pairGap * .5f, 1f), false));
        }
        return strokes;
    }

    static List<Stroke> MakeTurtleScutes()
    {
        const int columns = 14;
        const int rows = 11;
        const float width = 1f / columns;
        const float rise = 1f / rows;
        const float radius = rise * 2f / 3f;
        Vector2[] corners =
        {
            new(0f, radius), new(width * .5f, radius * .5f), new(width * .5f, -radius * .5f),
            new(0f, -radius), new(-width * .5f, -radius * .5f), new(-width * .5f, radius * .5f)
        };
        var strokes = new List<Stroke>(columns * rows * 9);
        for (int row = -1; row <= rows; row++)
        {
            for (int column = -1; column <= columns; column++)
            {
                var center = new Vector2((column + (row & 1) * .5f) * width, (row + .23f) * rise);
                // Each right-facing seam has a neighboring cell on its other side.
                for (int edge = 0; edge < 3; edge++)
                    strokes.Add(new Stroke(center + corners[edge], center + corners[edge + 1], false));
                for (int edge = 0; edge < corners.Length; edge++)
                    strokes.Add(new Stroke(center + corners[edge] * .78f,
                        center + corners[(edge + 1) % corners.Length] * .78f, true));
            }
        }
        return strokes;
    }

    static List<Stroke> MakeFlowers()
    {
        var strokes = new List<Stroke>();
        for (int row = 0; row < 5; row++)
            for (int column = -1; column < 9; column++)
            {
                var center = new Vector2((column + .5f + (row & 1) * .5f) / 8f, (row + .5f) / 5f);
                for (int petal = 0; petal < 4; petal++)
                {
                    float angle = Mathf.PI * .5f * petal + Mathf.PI * .25f;
                    var axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    var cross = new Vector2(-axis.y, axis.x);
                    Vector2 tip = center + axis * .050f;
                    Vector2 left = center + axis * .026f + cross * .012f;
                    Vector2 right = center + axis * .026f - cross * .012f;
                    strokes.Add(new Stroke(center, left, false));
                    strokes.Add(new Stroke(left, tip, false));
                    strokes.Add(new Stroke(tip, right, false));
                    strokes.Add(new Stroke(right, center, false));
                }
            }
        return strokes;
    }

    internal static bool Clip(ref Vector2 start, ref Vector2 end, Rect rect)
    {
        Vector2 delta = end - start;
        float first = 0f;
        float last = 1f;
        if (!ClipEdge(-delta.x, start.x - rect.xMin, ref first, ref last) ||
            !ClipEdge(delta.x, rect.xMax - start.x, ref first, ref last) ||
            !ClipEdge(-delta.y, start.y - rect.yMin, ref first, ref last) ||
            !ClipEdge(delta.y, rect.yMax - start.y, ref first, ref last))
            return false;
        end = start + delta * last;
        start += delta * first;
        return (end - start).sqrMagnitude > .000000001f;
    }

    static bool ClipEdge(float direction, float distance, ref float first, ref float last)
    {
        if (Mathf.Approximately(direction, 0f))
            return distance >= 0f;
        float t = distance / direction;
        if (direction < 0f)
            first = Mathf.Max(first, t);
        else
            last = Mathf.Min(last, t);
        return first <= last;
    }
}
