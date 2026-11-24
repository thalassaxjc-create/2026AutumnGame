using System;
using UnityEngine;

/// <summary>后两环节的材料、刀孔与张力状态。仅工位打开时推进，不持有 Unity 原生资源。</summary>
public sealed partial class BambooCraftProgress
{
    public const int FinishingGroups = ScrapingRounds;
    public const int RodsPerPiece = 25;
    public const int RodsPerGroup = PiecesPerRound * RodsPerPiece;
    public const int ThreadsPerBatch = FinishingGroups * RodsPerGroup;
    public const float GateX = 0.58f;
    public const float GateHalfOpening = 0.15f;
    public const float PieceHalfLength = 0.12f;
    public const float PieceHalfHeight = 0.025f;
    public const float GateAspect = 924f / 260f;
    public const float SafeGateTilt = 10f;
    readonly int[] goodGateInputs = new int[FinishingGroups];
    readonly int[] goodRodOutputs = new int[FinishingGroups];
    float gateDelay, resultDelay, preFinishingAccuracy = 1;
    float dangerTimer, unsafePullTime;
    int dangerPhase, drawingErrorsTotal;
    int drawingDefectiveInput;
    int spottedGoodThreads;
    int spottedDefectiveThreads;

    public bool IsFinishingStage => Stage == Step.GatePassing || Stage == Step.GateResult
        || Stage == Step.DrawingReady || Stage == Step.Drawing || Stage == Step.CraftFinished;
    public bool IsGateStage => Stage == Step.GatePassing || Stage == Step.GateResult;
    public int ProcessedPieceCount => FineCount + DefectiveCount;
    public int InheritedDefectivePieces { get; private set; }
    public bool GateSourceDefective => IsGateStage && goodGateInputs[Mathf.Clamp(
        GatesCompleted - (Stage == Step.GateResult ? 1 : 0), 0, FinishingGroups - 1)] < PiecesPerRound;
    public int GatesCompleted { get; private set; }
    public int PerfectGates { get; private set; }
    public float GateHeight { get; private set; } = 0.5f;
    public float PieceX { get; private set; }
    public float PieceY { get; private set; } = 0.5f;
    public float PieceTilt { get; private set; }
    public bool GateDamaged { get; private set; }
    public int RodCount { get; private set; }
    public int DefectiveRodCount { get; private set; }
    public int ThreadCount { get; private set; }
    public int DefectiveThreadCount { get; private set; }
    public int TotalThreads => ThreadCount + DefectiveThreadCount;
    public int SpottedThreads => spottedGoodThreads + spottedDefectiveThreads;
    public int DrawingGroupsCompleted { get; private set; }
    public float DrawProgress { get; private set; }
    public float Tension { get; private set; }
    public float DangerMin { get; private set; } = 0.6f;
    public float DangerMax => DangerMin + 0.16f;
    public bool DangerWarning => dangerPhase == 1;
    public bool DangerActive => dangerPhase == 2;
    public bool DrawNeedsRelease { get; private set; }
    public int DrawErrors { get; private set; }
    public int FinishingFeedbackSerial { get; private set; }
    public float GateAccuracy => GatesCompleted == 0 ? (GateDamaged ? 0.35f : 1f)
        : (PerfectGates + (GatesCompleted - PerfectGates) * 0.35f) / GatesCompleted;
    public float DrawingAccuracy => Mathf.Clamp01(1 - drawingErrorsTotal * 0.07f);
    public float FinishingAccuracy => IsGateStage
        ? preFinishingAccuracy * 0.5f + GateAccuracy * 0.5f
        : preFinishingAccuracy * 0.5f + GateAccuracy * 0.25f + DrawingAccuracy * 0.25f;

    public bool Advance()
    {
        switch (Stage)
        {
            case Step.PlaceBamboo: return Place();
            case Step.RoughReady:
            case Step.StripFinished: return StartNextStrip();
            case Step.BatchFinished: return BeginGates();
            case Step.CraftFinished: return ProcessedPieceCount >= PiecesPerBatch ? BeginGates() : Place();
            case Step.DrawingReady: return BeginDrawingGroup();
            default: return false;
        }
    }

    public bool BeginGates()
    {
        if (Stage != Step.BatchFinished && Stage != Step.CraftFinished) return false;
        if (ProcessedPieceCount < PiecesPerBatch)
        {
            Say($"需要先完成 {PiecesPerBatch} 片剐青；合格片和残次片都可以继续加工");
            return false;
        }
        preFinishingAccuracy = Accuracy;
        int goodInputs = Mathf.Min(FineCount, PiecesPerBatch);
        InheritedDefectivePieces = PiecesPerBatch - goodInputs;
        FineCount -= goodInputs;
        DefectiveCount -= InheritedDefectivePieces;
        for (int i = 0; i < FinishingGroups; i++)
            goodGateInputs[i] = Mathf.Clamp(goodInputs - i * PiecesPerRound, 0, PiecesPerRound);
        GatesCompleted = PerfectGates = DrawingGroupsCompleted = drawingErrorsTotal = 0;
        DrawProgress = Tension = 0;
        DrawErrors = 0;
        DrawNeedsRelease = false;
        Array.Clear(goodRodOutputs, 0, goodRodOutputs.Length);
        BeginGate();
        return true;
    }

    void BeginGate()
    {
        float previousHeight = GateHeight;
        GateHeight = Mathf.Lerp(0.25f, 0.75f, Mathf.Clamp01(random()));
        // Always change the next opening, even when a random source repeats.
        if (GatesCompleted > 0 && Mathf.Abs(GateHeight - previousHeight) < 0.16f)
            GateHeight = previousHeight < 0.5f ? 0.72f : 0.28f;
        PieceX = 0.12f;
        if (GatesCompleted == 0) PieceY = 0.5f;
        PieceTilt = 0;
        GateDamaged = false;
        gateDelay = 1.2f;
        Stage = Step.GatePassing;
        Say(GateSourceDefective ? "本组含剐青残次片；W / S 对齐刀孔，松开回平"
            : "W / S 调整高度；临近刀孔时松开，让竹片回平后穿过");
    }

    public bool BeginDrawingGroup()
    {
        if (Stage != Step.DrawingReady || DrawingGroupsCompleted >= FinishingGroups) return false;
        int good = goodRodOutputs[DrawingGroupsCompleted];
        int defective = RodsPerGroup - good;
        if (RodCount < good || DefectiveRodCount < defective) return false;
        RodCount -= good;
        DefectiveRodCount -= defective;
        drawingDefectiveInput = defective;
        DrawProgress = Tension = unsafePullTime = 0;
        DrawErrors = 0;
        DrawNeedsRelease = false;
        dangerPhase = 0;
        dangerTimer = 1.5f;
        Stage = Step.Drawing;
        Say(drawingDefectiveInput > 0
            ? $"本组有 {drawingDefectiveInput} 根残次竹条；按住右键抽丝，松开降张力"
            : "按住右键拉拽，松开回落张力；分段抽拉，避开红色危险区");
        return true;
    }

    public void TickFinishing(float deltaTime, float vertical, bool pulling)
    {
        if (!IsGateStage && Stage != Step.Drawing) return;
        if (deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
        // Substeps prevent skipping the fixed blade or a danger interval on a slow frame.
        float remaining = Mathf.Min(deltaTime, 0.25f);
        while (remaining > 0.00001f)
        {
            float dt = Mathf.Min(remaining, 1f / 60f);
            remaining -= dt;
            if (Stage == Step.GatePassing) TickGate(dt, Mathf.Clamp(vertical, -1, 1));
            else if (Stage == Step.GateResult)
            {
                resultDelay -= dt;
                if (resultDelay <= 0) BeginGate();
            }
            else if (Stage == Step.Drawing) TickDrawing(dt, pulling);
            else break;
        }
        if (IsFinishingStage) Revision++;
    }

    void TickGate(float dt, float vertical)
    {
        PieceY = Mathf.Clamp(PieceY + vertical * dt * 0.4f, 0.08f, 0.92f);
        PieceTilt = Mathf.MoveTowards(PieceTilt, vertical * 14f, dt * 80f);
        if (gateDelay > 0) { gateDelay -= dt; return; }
        PieceX += dt * 0.13f;
        float angle = PieceTilt * Mathf.Deg2Rad;
        float verticalExtent = Mathf.Abs(Mathf.Cos(angle)) * PieceHalfHeight
            + Mathf.Abs(Mathf.Sin(angle)) * PieceHalfLength * GateAspect;
        bool touching = PieceX + PieceHalfLength >= GateX - 0.018f
            && PieceX - PieceHalfLength <= GateX + 0.018f;
        bool clipped = Mathf.Abs(PieceY - GateHeight) + verticalExtent > GateHalfOpening + 0.018f
            || Mathf.Abs(PieceTilt) > SafeGateTilt;
        if (touching && clipped && !GateDamaged)
        {
            GateDamaged = true;
            FinishingFeedbackSerial++;
            Say($"呲啦——蹭到刀刃，本组计为 {RodsPerGroup} 根残次竹条");
        }
        if (PieceX - PieceHalfLength <= GateX + 0.04f) return;
        int good = GateDamaged ? 0 : goodGateInputs[GatesCompleted] * RodsPerPiece;
        int defective = RodsPerGroup - good;
        goodRodOutputs[GatesCompleted] = good;
        GatesCompleted++;
        if (!GateDamaged) PerfectGates++;
        RodCount += good;
        DefectiveRodCount += defective;
        if (GatesCompleted == FinishingGroups)
        {
            Stage = Step.DrawingReady;
            Say($"两次过剑门完成，共获得 {ThreadsPerBatch} 根竹条（含残次）；按空格开始抽丝");
        }
        else
        {
            Stage = Step.GateResult;
            resultDelay = 0.9f;
            Say($"本组获得 {RodsPerGroup} 根竹条（合格 {good}，残次 {defective}）；下一道剑门即将换位");
        }
    }

    void TickDrawing(float dt, bool pulling)
    {
        dangerTimer -= dt;
        if (dangerTimer <= 0)
        {
            if (dangerPhase == 0)
            {
                DangerMin = Mathf.Lerp(0.58f, 0.74f, Mathf.Clamp01(random()));
                dangerPhase = 1; dangerTimer = 0.75f;
            }
            else if (dangerPhase == 1) { dangerPhase = 2; dangerTimer = 2.5f + Mathf.Clamp01(random()); }
            else { dangerPhase = 0; dangerTimer = 1.2f + Mathf.Clamp01(random()); }
        }
        if (!pulling)
        {
            Tension = Mathf.Max(0, Tension - dt * 0.48f);
            unsafePullTime = 0;
            if (DrawNeedsRelease && Tension <= 0.24f)
            {
                DrawNeedsRelease = false;
                Say("张力已回落，可以继续分段抽拉");
            }
            return;
        }
        if (DrawNeedsRelease) return;
        Tension = Mathf.Min(1, Tension + dt * 0.22f);
        bool unsafePull = Tension >= 0.94f || (DangerActive && Tension >= DangerMin && Tension <= DangerMax);
        unsafePullTime = unsafePull ? unsafePullTime + dt : Mathf.Max(0, unsafePullTime - dt * 2);
        if (unsafePullTime >= 0.35f)
        {
            DrawErrors++; drawingErrorsTotal++; FinishingFeedbackSerial++;
            DrawNeedsRelease = true;
            Say("张力过高，纤维受损！松开右键，等张力降到 24% 以下再继续");
            return;
        }
        DrawProgress = Mathf.Min(1, DrawProgress + dt * 0.1f);
        if (DrawProgress < 1) return;
        int defective = Mathf.Min(RodsPerGroup, drawingDefectiveInput + DrawErrors * 5);
        DefectiveThreadCount += defective;
        ThreadCount += RodsPerGroup - defective;
        if (batchIsSpotted)
        {
            spottedDefectiveThreads += defective;
            spottedGoodThreads += RodsPerGroup - defective;
        }
        DrawingGroupsCompleted++;
        Tension = 0;
        Stage = DrawingGroupsCompleted == FinishingGroups ? Step.CraftFinished : Step.DrawingReady;
        Say(Stage == Step.CraftFinished
            ? $"两组抽丝完成！本批 {ThreadsPerBatch} 根已入库，合格与残次分别计数"
            : $"本组 {RodsPerGroup} 根已入库；按空格开始下一组抽丝");
    }

    public bool ConsumeThreads(int count, out bool usedDefective) =>
        ConsumeThreads(count, out usedDefective, out _);

    internal bool ConsumeThreads(int count, out bool usedDefective, out bool usedSpotted)
    {
        usedDefective = false;
        usedSpotted = false;
        if (count <= 0 || TotalThreads < count)
            return false;

        // A full spotted batch is used first. Incomplete batches can be combined.
        int spotted = SpottedThreads >= count ? count : Mathf.Max(0, count - (TotalThreads - SpottedThreads));
        int plain = count - spotted;
        int spottedGood = Mathf.Min(spotted, spottedGoodThreads);
        int plainGood = Mathf.Min(plain, ThreadCount - spottedGoodThreads);
        int spottedDefective = spotted - spottedGood;
        int plainDefective = plain - plainGood;
        usedDefective = spottedDefective + plainDefective > 0;
        usedSpotted = spotted == count;
        RemoveThreads(plainGood, plainDefective, spottedGood, spottedDefective);
        return true;
    }

    internal bool TradeThreads(int count, bool defective)
    {
        int total = defective ? DefectiveThreadCount : ThreadCount;
        if (count <= 0 || total < count)
            return false;
        int spotted = Mathf.Max(0, count - (total - (defective ? spottedDefectiveThreads : spottedGoodThreads)));
        int plain = count - spotted;
        if (defective)
            RemoveThreads(0, plain, 0, spotted);
        else
            RemoveThreads(plain, 0, spotted, 0);
        return true;
    }

    void RemoveThreads(int plainGood, int plainDefective, int spottedGood, int spottedDefective)
    {
        ThreadCount -= plainGood + spottedGood;
        DefectiveThreadCount -= plainDefective + spottedDefective;
        spottedGoodThreads -= spottedGood;
        spottedDefectiveThreads -= spottedDefective;
        if (Stage == Step.CraftFinished && TotalThreads == 0)
        {
            Stage = Step.PlaceBamboo;
            Say("按空格放置下一根完整青竹");
        }
        else
            Revision++;
    }
}
