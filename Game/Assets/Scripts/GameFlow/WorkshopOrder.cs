using System;
using UnityEngine;

public sealed class WorkshopOrder
{
    public enum Phase { Opening, Village, Home, NewOrder, Production, ReadyToSubmit, Complete }
    public enum Stage { Preparation, BambooFinishing, Weaving, Lacquer, Drying }
    public enum StageStatus { Locked, Available, InProgress, Complete }
    public enum QualityGrade { S, A, B, C, D }

    public const int FirstDayStartMinute = 6 * 60;
    public const int FirstDayEndMinute = 24 * 60;
    public static WorkshopOrder Current { get; internal set; }

    readonly WorkshopOrderSettings settings;
    int completedStages;
    int minuteOfDay;
    int elapsedGameMinutes;
    int dryingStartedAt = -1;
    float realSecondAccumulator;
    Stage? running;
    bool orderRead;

    public Phase CurrentPhase { get; private set; } = Phase.Opening;
    public bool HasBamboo { get; private set; }
    public bool DayEnded { get; private set; }
    public bool IsDrying => running == Stage.Drying && dryingStartedAt >= 0;
    public bool UsedDefectiveMaterials { get; private set; }
    public int Revision { get; private set; }
    public int Funds { get; private set; }
    public int XiaoPiFunds { get; private set; }
    public int XiaoBaiFunds { get; private set; }
    public int Days => 1;
    public int Hour => minuteOfDay / 60;
    public int Minute => minuteOfDay % 60;
    public int ElapsedGameMinutes => elapsedGameMinutes;
    public float Progress => completedStages / 5f;
    public bool ComputerNotice => CurrentPhase == Phase.NewOrder || CurrentPhase == Phase.ReadyToSubmit;
    public WeavingMinigameController.WeavingResult? WeavingResult { get; private set; }
    public LacquerMinigameController.LacquerResult? LacquerResult { get; private set; }
    public QualityGrade? FinalGrade { get; private set; }
    public int? FundsBeforeSubmission { get; private set; }
    public int Reward { get; private set; }
    public int BasePrice => settings.baseReward;
    public decimal QualityMultiplier { get; private set; } = 1m;
    public decimal MaterialMultiplier { get; private set; } = 1m;
    public int RemainingDryingMinutes => !IsDrying ? 0 :
        Math.Max(0, settings.dryingMinutes - (elapsedGameMinutes - dryingStartedAt));
    public bool CanHarvest => !DayEnded && CurrentPhase == Phase.Production && completedStages == 0 && !HasBamboo;
    public bool CanWorkBamboo => !DayEnded && HasBamboo && CurrentPhase == Phase.Production && completedStages < 2;
    public bool CanStartDrying => !DayEnded && CurrentPhase == Phase.Production && completedStages == 4 && !running.HasValue;

    public WorkshopOrder(WorkshopOrderSettings settings)
    {
        this.settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
        Funds = settings.initialFunds;
        XiaoPiFunds = settings.initialXiaoPiFunds;
        XiaoBaiFunds = settings.initialXiaoBaiFunds;
        minuteOfDay = Mathf.Clamp(settings.initialHour * 60 + settings.initialMinute,
            FirstDayStartMinute, FirstDayEndMinute);
    }

    public void CompleteOpening()
    {
        if (CurrentPhase != Phase.Opening)
            return;
        CurrentPhase = Phase.Village;
        Revision++;
    }

    public void CloseIntroduction(Phase phase)
    {
        if (CurrentPhase != phase || (phase != Phase.Village && phase != Phase.Home))
            return;
        CurrentPhase = phase == Phase.Village ? Phase.Home : Phase.NewOrder;
        Revision++;
    }

    public void ReadOrder()
    {
        if (CurrentPhase == Phase.NewOrder)
            orderRead = true;
    }

    public void OpenComputer() => orderRead = false;

    public void CloseComputer()
    {
        if (CurrentPhase != Phase.NewOrder || !orderRead)
            return;
        CurrentPhase = Phase.Production;
        Revision++;
    }

    public void Tick(float realSeconds, bool allowClock)
    {
        if (!allowClock || realSeconds <= 0f || float.IsNaN(realSeconds) || float.IsInfinity(realSeconds) ||
            CurrentPhase == Phase.Opening || CurrentPhase == Phase.Complete || DayEnded)
            return;
        realSecondAccumulator += realSeconds;
        float secondsPerMinute = Mathf.Max(.1f, settings.realSecondsPerGameMinute);
        int minutes = Mathf.FloorToInt(realSecondAccumulator / secondsPerMinute);
        if (minutes <= 0)
            return;
        realSecondAccumulator -= minutes * secondsPerMinute;
        AdvanceTime(minutes);
    }

    public void AdvanceTime(int gameMinutes)
    {
        if (gameMinutes <= 0 || DayEnded)
            return;
        int advanced = Math.Min(gameMinutes, FirstDayEndMinute - minuteOfDay);
        minuteOfDay += advanced;
        elapsedGameMinutes += advanced;
        if (minuteOfDay >= FirstDayEndMinute)
            DayEnded = true;
        CompleteDryingIfReady();
        Revision++;
    }

    public void PickUpBamboo()
    {
        if (!CanHarvest)
            return;
        HasBamboo = true;
        AdvanceTime(settings.harvestMinutes);
        Revision++;
    }

    public StageStatus GetStatus(Stage stage)
    {
        if ((int)stage < completedStages)
            return StageStatus.Complete;
        if (running == stage || (stage == Stage.Preparation && HasBamboo && completedStages == 0))
            return StageStatus.InProgress;
        return !DayEnded && CurrentPhase == Phase.Production && (int)stage == completedStages
            ? StageStatus.Available : StageStatus.Locked;
    }

    public bool CanPlay(Stage stage)
    {
        if (DayEnded || CurrentPhase != Phase.Production || running.HasValue || (int)stage != completedStages)
            return false;
        if (stage == Stage.Preparation)
            return HasBamboo;
        if (stage == Stage.Weaving)
            return WorkshopInventory.TotalThreads >= 100;
        return stage != Stage.Drying;
    }

    public bool Begin(Stage stage)
    {
        if (!CanPlay(stage))
            return false;
        running = stage;
        Revision++;
        return true;
    }

    public void Leave(Stage stage)
    {
        if (running != stage)
            return;
        running = null;
        Revision++;
    }

    public void ObserveBamboo(BambooCraftProgress craft)
    {
        if (running != Stage.Preparation && running != Stage.BambooFinishing)
            return;
        WorkshopInventory.CaptureCraft(craft);
        if (running == Stage.Preparation && HasBamboo && craft.Stage == BambooCraftProgress.Step.BatchFinished &&
            craft.FinishedInBatch == 4)
        {
            Accept(Stage.Preparation, settings.preparationMinutes);
            Begin(Stage.BambooFinishing);
        }
        else if (running == Stage.BambooFinishing && craft.Stage == BambooCraftProgress.Step.CraftFinished &&
            craft.GatesCompleted == BambooCraftProgress.FinishingGroups &&
            craft.DrawingGroupsCompleted == BambooCraftProgress.FinishingGroups)
        {
            WorkshopInventory.CaptureCraft(craft);
            Accept(Stage.BambooFinishing, settings.bambooFinishingMinutes);
        }
    }

    public void ConfirmWeaving(WeavingMinigameController.WeavingResult result)
    {
        if (running != Stage.Weaving || !WorkshopInventory.ConsumeThreads(100, out bool defective))
            return;
        UsedDefectiveMaterials |= defective;
        WeavingResult = result;
        Accept(Stage.Weaving, result.gameMinutes);
    }

    public void ConfirmLacquer(LacquerMinigameController.LacquerResult result)
    {
        if (running != Stage.Lacquer)
            return;
        LacquerResult = result;
        Accept(Stage.Lacquer, result.gameMinutes);
    }

    public bool BeginDrying()
    {
        if (!CanStartDrying)
            return false;
        running = Stage.Drying;
        dryingStartedAt = elapsedGameMinutes;
        Revision++;
        return true;
    }

    void CompleteDryingIfReady()
    {
        if (!IsDrying || elapsedGameMinutes - dryingStartedAt < settings.dryingMinutes)
            return;
        running = null;
        dryingStartedAt = -1;
        completedStages++;
        CurrentPhase = Phase.ReadyToSubmit;
        Revision++;
    }

    void Accept(Stage stage, int gameMinutes)
    {
        running = null;
        if ((int)stage == completedStages)
        {
            completedStages++;
            AdvanceTime(Math.Max(0, gameMinutes));
        }
        Revision++;
    }

    public bool Submit()
    {
        if (CurrentPhase != Phase.ReadyToSubmit || running.HasValue || !WeavingResult.HasValue ||
            !LacquerResult.HasValue)
            return false;
        FinalGrade = CombineGrades((int)WeavingResult.Value.grade, (int)LacquerResult.Value.grade);
        QualityMultiplier = Coefficient(FinalGrade.Value);
        MaterialMultiplier = UsedDefectiveMaterials ? (decimal)settings.defectiveMaterialMultiplier : 1m;
        decimal total = settings.baseReward * QualityMultiplier * MaterialMultiplier;
        Reward = (int)Math.Round(total, MidpointRounding.AwayFromZero);
        FundsBeforeSubmission = Funds;
        Funds += Reward;
        XiaoPiFunds += settings.xiaoPiDayOneIncome;
        XiaoBaiFunds += settings.xiaoBaiDayOneIncome;
        CurrentPhase = Phase.Complete;
        Revision++;
        return true;
    }

    static QualityGrade CombineGrades(int weavingIndex, int lacquerIndex)
    {
        int combined = Mathf.Clamp((weavingIndex + lacquerIndex + 1) / 2, 0, 4);
        return (QualityGrade)combined;
    }

    decimal Coefficient(QualityGrade grade) => (decimal)(grade switch
    {
        QualityGrade.S => settings.rewardS,
        QualityGrade.A => settings.rewardA,
        QualityGrade.B => settings.rewardB,
        QualityGrade.C => settings.rewardC,
        _ => settings.rewardD
    });
}
