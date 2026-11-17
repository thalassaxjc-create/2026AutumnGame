using System;

public sealed class WorkshopOrder
{
    public enum Phase { Village, Home, NewOrder, Production, ReadyToSubmit, Complete }
    public enum Stage { Preparation, BambooFinishing, Weaving, Lacquer }
    public enum StageStatus { Locked, Available, InProgress, Complete }

    public static WorkshopOrder Current { get; internal set; }

    readonly WorkshopOrderSettings settings;
    int completedStages;
    int minutes;
    Stage? running;
    bool orderRead;
    public Phase CurrentPhase { get; private set; }
    public bool HasBamboo { get; private set; }
    public int Revision { get; private set; }
    public int Funds { get; private set; }
    public int Days => minutes / 1440 + 1;
    public int Hour => minutes / 60 % 24;
    public int Minute => minutes % 60;
    public float Progress => completedStages / 4f;
    public bool ComputerNotice => CurrentPhase == Phase.NewOrder || CurrentPhase == Phase.ReadyToSubmit;
    public WeavingMinigameController.WeavingResult? WeavingResult { get; private set; }
    public LacquerMinigameController.LacquerResult? LacquerResult { get; private set; }
    public int? FundsBeforeSubmission { get; private set; }
    public int Reward { get; private set; }
    public bool CanHarvest => CurrentPhase == Phase.Production && completedStages == 0 && !HasBamboo;
    public bool CanWorkBamboo => HasBamboo && CurrentPhase == Phase.Production && completedStages < 2;

    public WorkshopOrder(WorkshopOrderSettings settings)
    {
        this.settings = settings;
        Funds = settings.initialFunds;
        minutes = (settings.initialDay - 1) * 1440 + settings.initialHour * 60 + settings.initialMinute;
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

    public void PickUpBamboo()
    {
        if (!CanHarvest)
            return;
        HasBamboo = true;
        Revision++;
    }

    public StageStatus GetStatus(Stage stage)
    {
        if ((int)stage < completedStages)
            return StageStatus.Complete;
        if (running == stage || (stage == Stage.Preparation && HasBamboo && completedStages == 0))
            return StageStatus.InProgress;
        return CurrentPhase == Phase.Production && (int)stage == completedStages
            ? StageStatus.Available : StageStatus.Locked;
    }

    public bool CanPlay(Stage stage) =>
        (CurrentPhase == Phase.Production || CurrentPhase == Phase.ReadyToSubmit) &&
        !running.HasValue && (stage != Stage.Preparation || HasBamboo) && ((int)stage == completedStages ||
        ((stage == Stage.Weaving || stage == Stage.Lacquer) && (int)stage < completedStages));

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
            Accept(Stage.BambooFinishing, settings.bambooFinishingMinutes);
        }
    }

    public void ConfirmWeaving(WeavingMinigameController.WeavingResult result)
    {
        if (running != Stage.Weaving)
            return;
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

    void Accept(Stage stage, int gameMinutes)
    {
        running = null;
        if ((int)stage == completedStages)
        {
            completedStages++;
            minutes += Math.Max(0, gameMinutes);
            if (completedStages == 4)
                CurrentPhase = Phase.ReadyToSubmit;
        }
        Revision++;
    }

    public bool Submit()
    {
        if (CurrentPhase != Phase.ReadyToSubmit || running.HasValue || !WeavingResult.HasValue || !LacquerResult.HasValue)
            return false;

        decimal total = (decimal)settings.baseReward *
            (Coefficient(WeavingResult.Value.grade.ToString()) + Coefficient(LacquerResult.Value.grade.ToString())) / 2m;
        Reward = (int)Math.Round(total, MidpointRounding.AwayFromZero);
        FundsBeforeSubmission = Funds;
        Funds += Reward;
        CurrentPhase = Phase.Complete;
        Revision++;
        return true;
    }

    decimal Coefficient(string grade) => (decimal)(grade switch
    {
        "S" => settings.rewardS,
        "A" => settings.rewardA,
        "B" => settings.rewardB,
        "C" => settings.rewardC,
        _ => settings.rewardD
    });
}
