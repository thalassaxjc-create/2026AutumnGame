using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class WorkshopOrder
{
    public enum Phase { Opening, Village, Home, NewOrder, Production, ReadyToSubmit, OrderComplete, Complete }
    public enum Stage { Preparation, BambooFinishing, Weaving, Lacquer, Drying, Pattern }
    public enum StageStatus { Locked, Available, InProgress, Complete }
    public enum QualityGrade { S, A, B, C, D }
    public enum ClockSpeed { Paused = 0, Normal = 1, Double = 2, Quadruple = 4 }

    public readonly struct SheetDelivery
    {
        public QualityGrade Weaving { get; }
        public QualityGrade Lacquer { get; }
        public CurtainPatternResult? Pattern { get; }
        public QualityGrade Grade { get; }
        public decimal QualityMultiplier { get; }
        public decimal MaterialMultiplier { get; }
        public bool UsedSpottedBamboo { get; }
        public bool SoldToCollector { get; }
        public int Reward { get; }

        internal SheetDelivery(QualityGrade weaving, QualityGrade lacquer, CurtainPatternResult? pattern,
            QualityGrade grade, decimal qualityMultiplier, decimal materialMultiplier,
            bool usedSpottedBamboo, bool soldToCollector, int reward)
        {
            Weaving = weaving;
            Lacquer = lacquer;
            Pattern = pattern;
            Grade = grade;
            QualityMultiplier = qualityMultiplier;
            MaterialMultiplier = materialMultiplier;
            UsedSpottedBamboo = usedSpottedBamboo;
            SoldToCollector = soldToCollector;
            Reward = reward;
        }
    }

    public readonly struct DaySettlement
    {
        public int Day { get; }
        public int DeliveredCount { get; }
        public int RequiredSheets { get; }
        public bool Delivered => DeliveredCount == RequiredSheets;
        public bool ReachedMidnight { get; }
        public int BatchBonus { get; }
        public bool XiaoPiPenalized { get; }
        public int PlayerIncome { get; }
        public int XiaoPiIncome { get; }
        public int XiaoBaiIncome { get; }
        public int PlayerTotal { get; }
        public int XiaoPiTotal { get; }
        public int XiaoBaiTotal { get; }

        internal DaySettlement(WorkshopOrder order, int piIncome, int baiIncome)
        {
            Day = order.Days;
            RequiredSheets = order.RequiredSheets;
            BatchBonus = order.BatchBonus;
            DeliveredCount = order.DeliveredCount;
            ReachedMidnight = order.MinutesUntilClose == 0;
            XiaoPiPenalized = order.XiaoPiSabotaged;
            PlayerIncome = order.TodayIncome;
            XiaoPiIncome = piIncome;
            XiaoBaiIncome = baiIncome;
            PlayerTotal = order.Funds;
            XiaoPiTotal = order.XiaoPiFunds;
            XiaoBaiTotal = order.XiaoBaiFunds;
        }

        public int RankOf(int funds) => 1 + (PlayerTotal > funds ? 1 : 0) +
            (XiaoPiTotal > funds ? 1 : 0) + (XiaoBaiTotal > funds ? 1 : 0);
    }

    public const int FirstDayStartMinute = 6 * 60;
    public const int FirstDayEndMinute = 24 * 60;
    public const int LastStoryDay = 5;
    public const int SheetsPerDay = 2;
    public static WorkshopOrder Current { get; internal set; }

    readonly WorkshopOrderSettings settings;
    readonly List<DaySettlement> settlements = new(LastStoryDay);
    readonly List<SheetDelivery> deliveries = new(SheetsPerDay);
    internal readonly HashSet<string> HarvestedStalks = new();
    int completedStages;
    int minuteOfDay;
    int elapsedGameMinutes;
    int dryingStartedAt = -1;
    float realSecondAccumulator;
    Stage? running;
    bool orderRead;
    bool morningPending;

    public Phase CurrentPhase { get; private set; } = Phase.Opening;
    public bool HasBamboo => WorkshopInventory.GreenBamboo > 0 || SpottedBamboo > 0 ||
        WorkshopInventory.TrackedCraft?.IsProcessingBamboo == true;
    public bool DayEnded { get; private set; }
    public bool IsDrying => running == Stage.Drying && dryingStartedAt >= 0;
    public bool UsedDefectiveMaterials { get; private set; }
    public int Revision { get; private set; }
    public int Funds { get; private set; }
    public int XiaoPiFunds { get; private set; }
    public int XiaoBaiFunds { get; private set; }
    public int Days { get; private set; } = 1;
    public string DayName => Days switch
    {
        1 => "第一天", 2 => "第二天", 3 => "第三天", 4 => "第四天", _ => "第五天"
    };
    public int RequiredSheets => Days switch { 4 => 3, 5 => 1, _ => SheetsPerDay };
    public const int SampleBatchBonus = 120;
    public bool SampleBatch { get; private set; }
    public int BatchBonus { get; private set; }
    public string CustomerNotes => Days switch
    {
        4 when SampleBatch => $"按样验货：三张均达 B 级或以上，整批奖励 {SampleBatchBonus} 元。未交齐仍按张结算。",
        4 => "分批验收：每张单独结算，不计整批奖励。",
        5 => "18:00 前到院子找艾琳，选一张旧样帘或试纸教她试捞。",
        _ => ""
    };
    public CurtainPatternKind PatternKind => Days switch
    {
        2 => CurtainPatternKind.Turtle,
        3 => CurtainPatternKind.VermilionJade,
        _ => CurtainPatternKind.None
    };
    public bool IsPatternOrder => PatternKind != CurtainPatternKind.None;
    public string OrderName => Days switch
    {
        2 => "龟纹小帘",
        3 => "朱边青示范帘",
        4 => "纸坊试捞批次",
        5 => "艾琳的第一张纸",
        _ => "普通宣纸帘"
    };
    public ClockSpeed Speed { get; private set; } = ClockSpeed.Normal;
    public int MinutesUntilClose => FirstDayEndMinute - minuteOfDay;
    public int Hour => minuteOfDay / 60;
    public int Minute => minuteOfDay % 60;
    public int ElapsedGameMinutes => elapsedGameMinutes;
    int SheetSteps => IsPatternOrder ? 7 : 6; // 工序加上最后的交付。
    public int DeliveredCount => IsTeachingDay && TaughtLesson.HasValue ? 1 : deliveries.Count;
    public int CurrentSheetNumber => Mathf.Min(DeliveredCount + 1, RequiredSheets);
    public int TodayIncome { get; private set; }
    public float Progress => CurrentPhase is Phase.Complete or Phase.OrderComplete
        ? DeliveredCount / (float)RequiredSheets
        : (DeliveredCount + CompletedSteps / (float)SheetSteps) / RequiredSheets;
    public bool ComputerNotice => !IsTeachingDay && (CurrentPhase == Phase.NewOrder || CurrentPhase == Phase.ReadyToSubmit);
    public WeavingMinigameController.WeavingResult? WeavingResult { get; private set; }
    public LacquerMinigameController.LacquerResult? LacquerResult { get; private set; }
    public CurtainPatternDraft PatternDraft { get; private set; }
    public CurtainPatternResult? PatternResult { get; private set; }
    public bool HasPatternBlank => PatternDraft != null && !PatternResult.HasValue && CurrentPhase == Phase.Production;
    public bool CanUsePatternStation => HasPatternBlank && GetStatus(Stage.Pattern) == StageStatus.Available;
    public int PatternMinutes => PatternKind == CurtainPatternKind.VermilionJade
        ? Math.Max(1, settings.vermilionJadeMinutes) : Math.Max(1, settings.turtleMinutes);
    public DaySettlement? CurrentSettlement => CurrentPhase == Phase.Complete && settlements.Count > 0
        ? settlements[settlements.Count - 1] : null;
    public DaySettlement? PreviousSettlement => settlements.Count == Days - 1 && settlements.Count > 0
        ? settlements[settlements.Count - 1] : null;
    public IReadOnlyList<DaySettlement> Settlements { get; }
    public IReadOnlyList<SheetDelivery> Deliveries { get; }
    public SheetDelivery? LastDelivery => deliveries.Count > 0 ? deliveries[deliveries.Count - 1] : null;
    public bool CanAdvanceDay => CurrentPhase == Phase.Complete && Days < LastStoryDay;
    internal WorkshopJournal Journal { get; } = new();
    public bool FinalSettlementAcknowledged { get; private set; }
    public QualityGrade? FinalGrade => LastDelivery?.Grade;
    public int Reward => LastDelivery?.Reward ?? 0;
    public int BasePrice => Days switch
    {
        2 => settings.turtlePrice,
        3 => settings.vermilionJadePrice,
        4 => SampleBatch ? 130 : 150,
        5 => 240,
        _ => settings.baseReward
    };
    public decimal QualityMultiplier => LastDelivery?.QualityMultiplier ?? 1m;
    public decimal MaterialMultiplier => LastDelivery?.MaterialMultiplier ?? 1m;
    public int RemainingDryingMinutes => !IsDrying ? 0 :
        Math.Max(0, settings.dryingMinutes - (elapsedGameMinutes - dryingStartedAt));
    public bool CanExplore => !DayEnded && CurrentPhase is Phase.Home or Phase.NewOrder or Phase.Production
        or Phase.ReadyToSubmit or Phase.OrderComplete;
    public bool CanHarvest => CanExplore;
    public bool CanWorkBamboo => CanExplore && HasBamboo;
    public bool NeedsBamboo => !IsTeachingDay && CurrentPhase == Phase.Production && CompletedSteps == 0 && !HasBamboo;
    public bool CanStartDrying => !DayEnded && CurrentPhase == Phase.Production &&
        completedStages == (int)Stage.Drying && !running.HasValue;

    int CompletedSteps
    {
        get
        {
            if (completedStages >= (int)Stage.Lacquer || CurrentPhase != Phase.Production)
                return completedStages;
            if (batchMaterialReady || WorkshopInventory.TotalThreads >= BambooCraftProgress.ThreadsPerBatch)
                return (int)Stage.Weaving;
            var craft = WorkshopInventory.TrackedCraft;
            return craft != null && (craft.Stage == BambooCraftProgress.Step.BatchFinished || craft.IsFinishingStage)
                ? (int)Stage.BambooFinishing : 0;
        }
    }

    public WorkshopOrder(WorkshopOrderSettings settings)
    {
        this.settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
        Settlements = settlements.AsReadOnly();
        Deliveries = deliveries.AsReadOnly();
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

    public void CloseIntroduction(Phase phase, int choice = 0)
    {
        if (CurrentPhase != phase || (phase != Phase.Village && phase != Phase.Home))
            return;
        if (phase == Phase.Home && Days == 4)
            SampleBatch = choice == 1;
        CurrentPhase = phase == Phase.Village ? Phase.Home : IsTeachingDay ? Phase.Production : Phase.NewOrder;
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
        orderRead = false;
        StartSheet();
    }

    public void SetClockSpeed(ClockSpeed speed)
    {
        if (speed != ClockSpeed.Paused && speed != ClockSpeed.Normal &&
            speed != ClockSpeed.Double && speed != ClockSpeed.Quadruple)
            throw new ArgumentOutOfRangeException(nameof(speed));
        if (Speed == speed)
            return;
        Speed = speed;
        Revision++;
    }

    public void Tick(float realSeconds, bool allowClock)
    {
        if (!allowClock || Speed == ClockSpeed.Paused || realSeconds <= 0f ||
            float.IsNaN(realSeconds) || float.IsInfinity(realSeconds) ||
            CurrentPhase == Phase.Opening || CurrentPhase == Phase.Complete || DayEnded)
            return;
        float secondsPerMinute = Mathf.Max(.1f, settings.realSecondsPerGameMinute);
        realSecondAccumulator = Mathf.Min(realSecondAccumulator + realSeconds * (int)Speed,
            secondsPerMinute * MinutesUntilClose);
        int minutes = Mathf.FloorToInt(realSecondAccumulator / secondsPerMinute);
        if (minutes <= 0)
            return;
        realSecondAccumulator -= minutes * secondsPerMinute;
        AdvanceTime(minutes);
    }

    public void AdvanceTime(int gameMinutes)
    {
        if (gameMinutes <= 0 || DayEnded || CurrentPhase == Phase.Complete)
            return;
        int advanced = Math.Min(gameMinutes, FirstDayEndMinute - minuteOfDay);
        minuteOfDay += advanced;
        elapsedGameMinutes += advanced;
        if (minuteOfDay >= FirstDayEndMinute)
            SettleDay();
        else
            CompleteDryingIfReady();
        Revision++;
    }

    public void PickUpBamboo()
    {
        if (!CanHarvest)
            return;
        AdvanceTime(settings.harvestMinutes);
        Revision++;
    }

    public StageStatus GetStatus(Stage stage)
    {
        if (IsTeachingDay || (stage == Stage.Pattern && !IsPatternOrder))
            return StageStatus.Locked;
        int steps = CompletedSteps;
        if ((int)stage < steps)
            return StageStatus.Complete;
        if (running == stage || (CurrentPhase == Phase.Production && stage == Stage.Preparation && HasBamboo && steps == 0))
            return StageStatus.InProgress;
        return !DayEnded && CurrentPhase == Phase.Production && (int)stage == steps
            ? StageStatus.Available : StageStatus.Locked;
    }

    public bool CanPlay(Stage stage)
    {
        return !IsTeachingDay && !DayEnded && CurrentPhase == Phase.Production && !running.HasValue &&
            (int)stage == CompletedSteps && stage is Stage.Weaving or Stage.Lacquer;
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
        if (!CanExplore)
            return;
        int inventoryRevision = WorkshopInventory.Revision;
        WorkshopInventory.CaptureCraft(craft);
        if (WorkshopInventory.Revision != inventoryRevision)
            Revision++;
        AdvanceTime(craft.TakeCompletedWorkMinutes(settings.preparationMinutes, settings.bambooFinishingMinutes));
    }

    public void ConfirmWeaving(WeavingMinigameController.WeavingResult result)
    {
        if (running != Stage.Weaving)
            return;
        if (!batchMaterialReady)
        {
            if (!WorkshopInventory.ConsumeThreads(BambooCraftProgress.ThreadsPerBatch, out bool defective, out bool spotted))
                return;
            UsedDefectiveMaterials |= defective;
            UsedSpottedBamboo = spotted;
        }
        WeavingResult = result;
        if (batchMaterialReady && LacquerResult.HasValue)
            Accept(Stage.Lacquer, result.gameMinutes);
        else
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
        if (IsPatternOrder)
            PatternDraft = new CurtainPatternDraft(PatternKind);
        else
            CurrentPhase = Phase.ReadyToSubmit;
        Revision++;
    }

    void Accept(Stage stage, int gameMinutes)
    {
        running = null;
        completedStages = (int)stage + 1;
        AdvanceTime(Math.Max(0, gameMinutes));
        Revision++;
    }

    public bool Submit() => Deliver(soldToCollector: false);

    bool Deliver(bool soldToCollector)
    {
        if (DayEnded || PendingTrial || CurrentPhase != Phase.ReadyToSubmit || running.HasValue ||
            !WeavingResult.HasValue || !LacquerResult.HasValue || (IsPatternOrder && !PatternResult.HasValue))
            return false;
        var weaving = (QualityGrade)WeavingResult.Value.grade;
        var lacquer = (QualityGrade)LacquerResult.Value.grade;
        QualityGrade grade = CombineGrades(weaving, lacquer, PatternResult?.Grade);
        decimal quality = Coefficient(grade);
        decimal material = UsedDefectiveMaterials ? (decimal)settings.defectiveMaterialMultiplier : 1m;
        if (soldToCollector && (!CollectorAvailable || grade > QualityGrade.A))
            return false;
        int price = soldToCollector ? CollectorBasePrice : BasePrice * (UsedSpottedBamboo ? 2 : 1);
        int reward = (int)Math.Round(price * quality * material, MidpointRounding.AwayFromZero);
        deliveries.Add(new SheetDelivery(weaving, lacquer, PatternResult, grade, quality, material,
            UsedSpottedBamboo, soldToCollector, reward));
        ChangeFunds(reward);
        if (DeliveredCount == RequiredSheets)
        {
            CurrentPhase = Phase.OrderComplete;
            if (Days == 4 && SampleBatch && deliveries.TrueForAll(sheet => sheet.Grade <= QualityGrade.B))
            {
                BatchBonus = SampleBatchBonus;
                ChangeFunds(BatchBonus);
            }
        }
        else
            StartSheet();
        Journal.AddEvent($"{DayName} · {OrderName}",
            WorkshopStoryUI.SheetDetails(deliveries[DeliveredCount - 1], DeliveredCount) +
            (BatchBonus > 0 ? $"\n整批通过验收，另获 {BatchBonus} 元。" : ""));
        return true;
    }

    public bool TryConfirmPattern()
    {
        if (!CanUsePatternStation || !PatternDraft.CanConfirm || MinutesUntilClose <= PatternMinutes)
            return false;
        PatternResult = PatternDraft.Evaluate();
        completedStages++;
        AdvanceTime(PatternMinutes);
        CurrentPhase = Phase.ReadyToSubmit;
        Revision++;
        return true;
    }

    public bool CloseDay()
    {
        if (!CanExplore)
            return false;
        SettleDay();
        return true;
    }

    void SettleDay()
    {
        if (CurrentPhase == Phase.Complete)
            return;
        var (piIncome, baiIncome) = Days switch
        {
            2 => (settings.xiaoPiDayTwoIncome, settings.xiaoBaiDayTwoIncome),
            3 => (settings.xiaoPiDayThreeIncome, settings.xiaoBaiDayThreeIncome),
            4 => (450, 390),
            5 => (240, 210),
            _ => (settings.xiaoPiDayOneIncome, settings.xiaoBaiDayOneIncome)
        };
        if (XiaoPiSabotaged)
            piIncome -= SabotagePenalty;
        XiaoPiFunds += piIncome;
        XiaoBaiFunds += baiIncome;
        settlements.Add(new DaySettlement(this, piIncome + partnerIncome, baiIncome));
        running = null;
        dryingStartedAt = -1;
        DayEnded = true;
        CurrentPhase = Phase.Complete;
        Revision++;
    }

    public bool TryStartNextDay()
    {
        if (!CanAdvanceDay)
            return false;
        Days++;
        morningPending = true;
        minuteOfDay = FirstDayStartMinute;
        realSecondAccumulator = 0f;
        elapsedGameMinutes = 0;
        orderRead = false;
        DayEnded = false;
        HarvestedStalks.Clear();
        deliveries.Clear();
        TodayIncome = 0;
        BatchBonus = 0;
        partnerIncome = 0;
        SampleBatch = false;
        ResetSheet();
        CurrentPhase = Phase.Home;
        Revision++;
        return true;
    }

    void ResetSheet()
    {
        completedStages = 0;
        batchMaterialReady = false;
        running = null;
        dryingStartedAt = -1;
        UsedDefectiveMaterials = false;
        UsedSpottedBamboo = false;
        PatternDraft = null;
        PatternResult = null;
        WeavingResult = null;
        LacquerResult = null;
    }

    void StartSheet()
    {
        ResetSheet();
        CurrentPhase = Phase.Production;
        Revision++;
    }

    internal bool TakeMorningNotice()
    {
        if (!morningPending)
            return false;
        morningPending = false;
        return true;
    }

    internal void AcknowledgeFinalSettlement()
    {
        if (Days == LastStoryDay && CurrentPhase == Phase.Complete)
            FinalSettlementAcknowledged = true;
    }

    static QualityGrade CombineGrades(QualityGrade weaving, QualityGrade lacquer, QualityGrade? pattern)
    {
        int count = pattern.HasValue ? 3 : 2;
        int sum = (int)weaving + (int)lacquer + (int)(pattern ?? QualityGrade.S);
        return (QualityGrade)Mathf.Clamp((sum + count - 1) / count, 0, 4);
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
