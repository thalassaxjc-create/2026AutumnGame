using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>本局工坊材料和加工进度；退出工位/切换场景后继续，新一局重置。</summary>
public static class BambooCraftSession
{
    public static BambooCraftProgress Current { get; private set; } = new BambooCraftProgress();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession() => Current = new BambooCraftProgress();
}

/// <summary>用工作面归一化坐标判定划线、覆盖、速度和竹节碰撞，不依赖屏幕分辨率。</summary>
public sealed partial class BambooCraftProgress
{
    public enum Step { PlaceBamboo, Splitting, RoughReady, Scraping, StripFinished, BatchFinished,
        GatePassing, GateResult, DrawingReady, Drawing, CraftFinished }
    public const int Columns = 80;
    public const int Rows = 12;
    public const int PiecesPerBatch = 4;
    public const int PiecesPerRound = 2;
    public const int ScrapingRounds = PiecesPerBatch / PiecesPerRound;
    public const float SafeNodeSpeed = 1.8f;
    public static readonly float[] Nodes = { 0.24f, 0.51f, 0.78f };
    public const float SplitGuideTolerance = 0.4f;
    const int ScrapeMinutesPerRound = 5;
    const int GateMinutesPerPass = 5;
    readonly bool[] removed = new bool[Columns * Rows];
    readonly bool[] nodeContact = new bool[3];
    readonly List<Vector2> scars = new List<Vector2>(32);
    readonly Func<float> random;
    readonly Func<bool> takeBamboo;
    Vector2 last, previousDirection;
    bool dragging, validStart;
    float strokeLength, strokeError, backtracking, startX, strokeDirection;
    float splitScoreSum, scrapeLength, unsafeLength;
    int splitAttempts, removedCount, scrapingPieceCount;
    bool preparationCharged;
    bool finishingCharged;
    bool batchIsSpotted;

    public Step Stage { get; private set; }
    public bool IsProcessingBamboo => Stage != Step.PlaceBamboo && Stage != Step.CraftFinished;
    public bool UpgradedKnife { get; internal set; }
    public int Splits { get; private set; }
    public int RoughCount { get; private set; }
    public int FineCount { get; private set; }
    public int DefectiveCount { get; private set; }
    public int FinishedInBatch { get; private set; }
    public int ScrapingRoundsCompleted { get; private set; }
    public int ScrapingPiecesThisRound => scrapingPieceCount;
    int ScrapingCapacity => UpgradedKnife ? PiecesPerBatch : PiecesPerRound;
    public int ScrapingRoundCount => ScrapingRoundsCompleted + (Stage == Step.Scraping ? 1 : 0) +
        (RoughCount + ScrapingCapacity - 1) / ScrapingCapacity;
    public int Damage { get; private set; }
    public int Revision { get; private set; }
    public bool LastDefective { get; private set; }
    public string Message { get; private set; } = "按空格放置 1 根完整青竹";
    public float GuideY => 0.5f;
    public float Coverage => removedCount / (float)removed.Length;
    public float SplitAccuracy => splitAttempts == 0 ? 1f : splitScoreSum / splitAttempts;
    public float ScrapeAccuracy => Mathf.Clamp01(1f - unsafeLength / Mathf.Max(0.001f, scrapeLength) - Damage * 0.045f);
    public float Accuracy => IsFinishingStage ? FinishingAccuracy : Stage == Step.Splitting || Stage == Step.RoughReady
        ? SplitAccuracy : Mathf.Clamp01(SplitAccuracy * 0.35f + ScrapeAccuracy * 0.65f);
    public float DefectChance => Mathf.Clamp(Damage * 0.18f, 0f, 0.85f);
    public IReadOnlyList<Vector2> Scars => scars;
    public bool IsRemoved(int x, int y) => removed[y * Columns + x];

    public BambooCraftProgress(Func<float> randomValue = null, Func<bool> consumeBamboo = null)
    {
        random = randomValue ?? (() => UnityEngine.Random.value);
        takeBamboo = consumeBamboo ?? GreenBambooBag.TryTakeOne;
    }

    public bool Place() => Place(takeBamboo, false);

    internal bool PlaceSpotted(Func<bool> takeSpottedBamboo)
    {
        if (IsProcessingBamboo)
            return false;
        return Place(takeSpottedBamboo, true);
    }

    bool Place(Func<bool> takeMaterial, bool spotted)
    {
        if (Stage != Step.PlaceBamboo && Stage != Step.BatchFinished && Stage != Step.CraftFinished)
            return false;
        if (!takeMaterial())
        {
            Say(spotted ? "背包没有斑竹" : "没有完整青竹，请先到竹林采集");
            return false;
        }
        Splits = FinishedInBatch = splitAttempts = ScrapingRoundsCompleted = scrapingPieceCount = 0;
        splitScoreSum = 0;
        preparationCharged = finishingCharged = false;
        batchIsSpotted = spotted;
        ResetSurface();
        Stage = Step.Splitting;
        Say($"已放置{(spotted ? "斑竹" : "青竹")}。按住左键沿亮线横划，松开获得 {PiecesPerBatch} 块粗竹片");
        return true;
    }

    internal int TakeCompletedWorkMinutes(int preparationMinutes, int finishingMinutes)
    {
        int minutes = 0;
        if (!preparationCharged && FinishedInBatch == PiecesPerBatch)
        {
            preparationCharged = true;
            minutes += Mathf.Max(0, preparationMinutes - (ScrapingRounds - ScrapingRoundsCompleted) * ScrapeMinutesPerRound);
        }
        if (!finishingCharged && Stage == Step.CraftFinished && DrawingGroupsCompleted == FinishingGroups)
        {
            finishingCharged = true;
            minutes += Mathf.Max(0, finishingMinutes - (FinishingGroups - GatePassCount) * GateMinutesPerPass);
        }
        return minutes;
    }

    public bool StartNextStrip()
    {
        if ((Stage != Step.RoughReady && Stage != Step.StripFinished) || RoughCount < PiecesPerRound)
            return false;
        scrapingPieceCount = Mathf.Min(RoughCount, ScrapingCapacity);
        RoughCount -= scrapingPieceCount;
        ResetSurface();
        Stage = Step.Scraping;
        Say($"本轮处理 {scrapingPieceCount} 块竹片。按住左键刮竹青，刮净至 100% 完成");
        return true;
    }

    void ResetSurface()
    {
        Array.Clear(removed, 0, removed.Length);
        Array.Clear(nodeContact, 0, nodeContact.Length);
        scars.Clear();
        Damage = removedCount = 0;
        scrapeLength = unsafeLength = 0;
        LastDefective = false;
        CancelStroke();
    }

    public void BeginStroke(Vector2 point)
    {
        CancelStroke();
        if ((Stage != Step.Splitting && Stage != Step.Scraping)
            || (Stage == Step.Splitting && !InsideSplitArea(point)))
            return;
        dragging = true;
        last = point;
        if (Stage == Step.Splitting)
        {
            startX = point.x;
            strokeDirection = startX < 0.5f ? 1f : -1f;
            validStart = (startX <= 0.3f || startX >= 0.7f) && Mathf.Abs(point.y - GuideY) <= 0.45f;
        }
    }

    public int Drag(Vector2 point, float deltaTime)
    {
        if (!dragging)
            return 0;
        Vector2 delta = point - last;
        float length = new Vector2(delta.x, delta.y * 0.18f).magnitude;
        if (length < 0.0001f)
            return 0;
        if (Stage == Step.Splitting)
        {
            strokeLength += length;
            // Judge the overall path, allowing a broad corridor and small corrections.
            strokeError += length * (Mathf.Abs(last.y - GuideY) + Mathf.Abs(point.y - GuideY)) * 0.5f;
            backtracking += Mathf.Max(0f, -delta.x * strokeDirection);
            if (!InsideSplitArea(point)) validStart = false;
            last = point;
            return 0;
        }
        if (Stage != Step.Scraping)
            return 0;
        Vector2 previousPoint = last;
        last = point;
        // Keep holding the knife off the bamboo. Only the segment touching the
        // surface removes skin, including the edge when entering or leaving it.
        if (!ClipToSurface(previousPoint, point, out Vector2 from, out Vector2 to))
        {
            previousDirection = Vector2.zero;
            Array.Clear(nodeContact, 0, nodeContact.Length);
            return 0;
        }

        float speed = length / Mathf.Max(0.001f, deltaTime);
        Vector2 contactDelta = to - from;
        float contactLength = new Vector2(contactDelta.x, contactDelta.y * 0.18f).magnitude;
        if (contactLength < 0.0001f) return 0;
        if (!Inside(previousPoint)) previousDirection = Vector2.zero;
        bool turning = previousDirection.sqrMagnitude > 0 && Vector2.Dot(delta.normalized, previousDirection) < -0.05f && length < 0.12f;
        bool alongNode = Mathf.Abs(delta.x) < Mathf.Abs(delta.y * 0.18f) * 0.6f;
        bool safe = speed <= SafeNodeSpeed || length <= 0.018f || turning || alongNode;
        bool hit = false;
        for (int i = 0; i < Nodes.Length; i++)
        {
            float node = Nodes[i];
            bool touches = Mathf.Min(from.x, to.x) <= node + 0.014f && Mathf.Max(from.x, to.x) >= node - 0.014f;
            if (touches && !safe)
            {
                hit = true;
                if (!nodeContact[i])
                {
                    Damage++;
                    if (scars.Count < 32) scars.Add(new Vector2(node, to.y));
                    Say("撞到竹节，留下深痕！请减速或短距离换向");
                }
            }
            nodeContact[i] = touches && Inside(point) && Mathf.Abs(point.x - node) <= 0.04f;
        }
        scrapeLength += contactLength;
        if (hit) unsafeLength += contactLength;
        int before = removedCount;
        // Sweep the brush along the full segment, so low frame rates cannot leave holes or skip knots.
        int steps = Mathf.Clamp(Mathf.CeilToInt(contactLength * Columns * 2), 1, Columns * 3);
        for (int step = 0; step <= steps; step++)
            RemoveAround(Vector2.Lerp(from, to, step / (float)steps));
        previousDirection = Inside(point) ? delta.normalized : Vector2.zero;
        Revision++;
        if (removedCount == removed.Length)
        {
            int defects = 0;
            for (int piece = 0; piece < scrapingPieceCount; piece++)
            {
                if (random() < DefectChance)
                    defects++;
            }
            LastDefective = defects > 0;
            DefectiveCount += defects;
            FineCount += scrapingPieceCount - defects;
            FinishedInBatch += scrapingPieceCount;
            ScrapingRoundsCompleted++;
            Stage = FinishedInBatch == PiecesPerBatch ? Step.BatchFinished : Step.StripFinished;
            CancelStroke();
            Say(LastDefective ? $"本轮获得 {scrapingPieceCount - defects} 块精竹片、{defects} 块残次竹片"
                : $"本轮剐青完成，获得 {scrapingPieceCount} 块精竹片");
        }
        return removedCount - before;
    }

    static bool ClipToSurface(Vector2 from, Vector2 to, out Vector2 start, out Vector2 end)
    {
        Vector2 direction = to - from;
        float enter = 0, leave = 1;
        start = end = Vector2.zero;
        if (!ClipAxis(from.x, direction.x, ref enter, ref leave)
            || !ClipAxis(from.y, direction.y, ref enter, ref leave)) return false;
        start = Vector2.Lerp(from, to, enter);
        end = Vector2.Lerp(from, to, leave);
        return true;
    }

    static bool ClipAxis(float origin, float direction, ref float enter, ref float leave)
    {
        if (Mathf.Abs(direction) < 0.000001f) return origin >= 0 && origin <= 1;
        float near = -origin / direction;
        float far = (1 - origin) / direction;
        if (near > far) { float swap = near; near = far; far = swap; }
        enter = Mathf.Max(enter, near);
        leave = Mathf.Min(leave, far);
        return enter <= leave;
    }

    void RemoveAround(Vector2 point)
    {
        int cx = Mathf.RoundToInt(point.x * (Columns - 1));
        int cy = Mathf.RoundToInt(point.y * (Rows - 1));
        for (int y = Mathf.Max(0, cy - 1); y <= Mathf.Min(Rows - 1, cy + 1); y++)
        for (int x = Mathf.Max(0, cx - 2); x <= Mathf.Min(Columns - 1, cx + 2); x++)
        {
            int index = y * Columns + x;
            if (!removed[index]) { removed[index] = true; removedCount++; }
        }
    }

    public bool EndStroke()
    {
        if (!dragging) return false;
        bool completed = false;
        if (Stage == Step.Splitting)
        {
            splitAttempts++;
            float averageDeviation = strokeError / Mathf.Max(0.001f, strokeLength);
            float accuracy = Mathf.Clamp01(1f - averageDeviation / (SplitGuideTolerance * 2));
            bool reachedEnd = strokeDirection > 0 ? last.x >= 0.7f : last.x <= 0.3f;
            completed = validStart && reachedEnd && Mathf.Abs(last.x - startX) >= 0.55f
                && averageDeviation <= SplitGuideTolerance && backtracking <= 0.35f;
            splitScoreSum += completed ? accuracy : 0f;
            if (completed)
            {
                Splits = 1;
                RoughCount += PiecesPerBatch;
                Stage = Step.RoughReady;
                Say($"一劈完成，获得 {PiecesPerBatch} 块粗竹片；按空格开始剐竹青");
            }
            else Say("再试一次：从竹子一侧横向划向另一侧，大致顺着亮线即可");
        }
        CancelStroke();
        return completed;
    }

    public void CancelStroke()
    {
        dragging = validStart = false;
        strokeLength = strokeError = backtracking = 0;
        previousDirection = Vector2.zero;
        Array.Clear(nodeContact, 0, nodeContact.Length);
    }

    void Say(string message) { Message = message; Revision++; }
    public static bool InsideSplitArea(Vector2 point) => point.x >= -0.15f && point.x <= 1.15f && point.y >= -0.2f && point.y <= 1.2f;
    public static bool Inside(Vector2 point) => point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1;
}
