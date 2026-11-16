using System;
using UnityEngine;

/// <summary>后两环节的材料、刀孔与张力状态。仅工位打开时推进，不持有 Unity 原生资源。</summary>
public sealed partial class BambooCraftProgress
{
    public const int FinishingGroups = 4;
    public const int RodsPerPiece = 25;
    public const float GateX = 0.58f;
    public const float GateHalfOpening = 0.15f;
    public const float PieceHalfLength = 0.12f;
    public const float PieceHalfHeight = 0.025f;
    public const float GateAspect = 924f / 260f;
    public const float SafeGateTilt = 10f;
    readonly bool[] damagedGatePieces = new bool[FinishingGroups];
    readonly bool[] flawedGateInputs = new bool[FinishingGroups];
    float gateDelay, resultDelay, preFinishingAccuracy = 1;
    float dangerTimer, unsafePullTime;
    int dangerPhase, drawingErrorsTotal;
    bool drawingInputDefective;

    public bool IsFinishingStage => Stage == Step.GatePassing || Stage == Step.GateResult
        || Stage == Step.DrawingReady || Stage == Step.Drawing || Stage == Step.CraftFinished;
    public bool IsGateStage => Stage == Step.GatePassing || Stage == Step.GateResult;
    public int ProcessedPieceCount => FineCount + DefectiveCount;
    public int InheritedDefectivePieces { get; private set; }
    public bool GateSourceDefective => IsGateStage && flawedGateInputs[Mathf.Clamp(
        GatesCompleted - (Stage == Step.GateResult ? 1 : 0), 0, FinishingGroups - 1)];
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
            case Step.CraftFinished: return ProcessedPieceCount >= FinishingGroups ? BeginGates() : Place();
            case Step.DrawingReady: return BeginDrawingGroup();
            default: return false;
        }
    }

    public bool BeginGates()
    {
        if (Stage != Step.BatchFinished && Stage != Step.CraftFinished) return false;
        if (ProcessedPieceCount < FinishingGroups)
        {
            Say("需要先完成 4 片剐青；合格片和残次片都可以继续加工");
            return false;
        }
        preFinishingAccuracy = Accuracy;
        int goodInputs = Mathf.Min(FineCount, FinishingGroups);
        InheritedDefectivePieces = FinishingGroups - goodInputs;
        FineCount -= goodInputs;
        DefectiveCount -= InheritedDefectivePieces;
        for (int i = 0; i < FinishingGroups; i++) flawedGateInputs[i] = i >= goodInputs;
        GatesCompleted = PerfectGates = DrawingGroupsCompleted = drawingErrorsTotal = 0;
        DrawProgress = Tension = 0;
        DrawErrors = 0;
        DrawNeedsRelease = false;
        Array.Clear(damagedGatePieces, 0, damagedGatePieces.Length);
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
        Say(GateSourceDefective ? "本片有剐青深痕，保留残次标记；W / S 对齐刀孔，松开回平"
            : "W / S 调整高度；临近刀孔时松开，让竹片回平后穿过");
    }

    public bool BeginDrawingGroup()
    {
        if (Stage != Step.DrawingReady || DrawingGroupsCompleted >= FinishingGroups) return false;
        drawingInputDefective = damagedGatePieces[DrawingGroupsCompleted];
        if (drawingInputDefective)
        {
            if (DefectiveRodCount < RodsPerPiece) return false;
            DefectiveRodCount -= RodsPerPiece;
        }
        else
        {
            if (RodCount < RodsPerPiece) return false;
            RodCount -= RodsPerPiece;
        }
        DrawProgress = Tension = unsafePullTime = 0;
        DrawErrors = 0;
        DrawNeedsRelease = false;
        dangerPhase = 0;
        dangerTimer = 1.5f;
        Stage = Step.Drawing;
        Say(drawingInputDefective
            ? "本组竹条已有碰刃缺口，保留残次标记；按住右键抽丝，松开降张力"
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
            Say("呲啦——蹭到刀刃，竹片出现缺口；本次计为 25 根残次竹条");
        }
        if (PieceX - PieceHalfLength <= GateX + 0.04f) return;
        bool defective = GateDamaged || flawedGateInputs[GatesCompleted];
        damagedGatePieces[GatesCompleted] = defective;
        GatesCompleted++;
        if (!GateDamaged) PerfectGates++;
        if (defective) DefectiveRodCount += RodsPerPiece;
        else RodCount += RodsPerPiece;
        if (GatesCompleted == FinishingGroups)
        {
            Stage = Step.DrawingReady;
            Say("四次过剑门完成，共获得 100 根竹条（含残次）；按空格开始第 1 组抽丝");
        }
        else
        {
            Stage = Step.GateResult;
            resultDelay = 0.9f;
            Say(GateDamaged ? "本片有缺口；下一道剑门即将换位"
                : defective ? "平稳通过，原有深痕保留为残次；下一道剑门即将换位"
                : "平稳通过，厚度均匀！下一道剑门即将换位");
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
        int defective = drawingInputDefective ? RodsPerPiece : Mathf.Min(RodsPerPiece, DrawErrors * 5);
        DefectiveThreadCount += defective;
        ThreadCount += RodsPerPiece - defective;
        DrawingGroupsCompleted++;
        Tension = 0;
        Stage = DrawingGroupsCompleted == FinishingGroups ? Step.CraftFinished : Step.DrawingReady;
        Say(Stage == Step.CraftFinished
            ? "四组抽丝完成！本批 100 根已结算，合格与残次分别入库"
            : "本组 25 根已结算；按空格开始下一组张力挑战");
    }
}
