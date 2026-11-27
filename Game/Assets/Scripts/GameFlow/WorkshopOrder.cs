using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class WorkshopOrder
{
    public enum Phase { Opening, Village, Home, NewOrder, Production, ReadyToSubmit, OrderComplete, Complete }
    public enum Stage { Preparation, BambooFinishing, Weaving, Lacquer, Drying, Pattern, VermilionTrim }
    public enum StageStatus { Locked, Available, InProgress, Complete }
    public enum QualityGrade { S, A, B, C, D }
    public enum ClockSpeed { Paused = 0, Normal = 1, Double = 2, Quadruple = 4 }

    public readonly struct SheetDelivery
    {
        public QualityGrade Weaving { get; }
        public QualityGrade Lacquer { get; }
        public QualityGrade? PatternGrade { get; }
        public QualityGrade Grade { get; }
        public decimal QualityMultiplier { get; }
        public decimal MaterialMultiplier { get; }
        public bool UsedSpottedBamboo { get; }
        public int Reward { get; }

        internal SheetDelivery(QualityGrade weaving, QualityGrade lacquer, QualityGrade? patternGrade,
            QualityGrade grade, decimal qualityMultiplier, decimal materialMultiplier, bool usedSpottedBamboo, int reward)
        {
            Weaving = weaving;
            Lacquer = lacquer;
            PatternGrade = patternGrade;
            Grade = grade;
            QualityMultiplier = qualityMultiplier;
            MaterialMultiplier = materialMultiplier;
            UsedSpottedBamboo = usedSpottedBamboo;
            Reward = reward;
        }
    }

    public readonly struct DaySettlement
    {
        public int Day { get; }
        public int DeliveredCount { get; }
        public bool Delivered => DeliveredCount == SheetsPerDay;
        public bool ReachedMidnight { get; }
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
    public const int LastStoryDay = 3;
    public const int SheetsPerDay = 2;
    public static WorkshopOrder Current { get; internal set; }

    readonly WorkshopOrderSettings settings;
    readonly List<DaySettlement> settlements = new(LastStoryDay);
    readonly List<SheetDelivery> deliveries = new(SheetsPerDay);
    internal readonly HashSet<string> HarvestedStalks = new();
    int minuteOfDay;
    int elapsedGameMinutes;
    float realSecondAccumulator;
    bool orderRead;
    bool morningPending;

    public Phase CurrentPhase { get; private set; } = Phase.Opening;
    public bool HasBamboo => WorkshopInventory.GreenBamboo > 0 || SpottedBamboo > 0 ||
        WorkshopInventory.TrackedCraft?.IsProcessingBamboo == true;
    public bool DayEnded { get; private set; }
    public int Revision { get; private set; }
    public int Funds { get; private set; }
    public int XiaoPiFunds { get; private set; }
    public int XiaoBaiFunds { get; private set; }
    public int Days { get; private set; } = 1;
    public string DayName => Days == 1 ? "第一天" : Days == 2 ? "第二天" : "第三天";
    public bool IsPatternOrder => Days == 2;
    public bool RequiresVermilionTrim => Days == 3;
    public CurtainPatternKind PatternKind => IsPatternOrder ? CurtainPatternKind.Turtle : CurtainPatternKind.None;
    public string OrderName => Days switch
    {
        2 => "龟纹小帘",
        3 => "朱边青示范帘",
        _ => "普通宣纸帘"
    };
    public ClockSpeed Speed { get; private set; } = ClockSpeed.Normal;
    public int MinutesUntilClose => FirstDayEndMinute - minuteOfDay;
    public int Hour => minuteOfDay / 60;
    public int Minute => minuteOfDay % 60;
    public int ElapsedGameMinutes => elapsedGameMinutes;
    int SheetSteps => IsPatternOrder || RequiresVermilionTrim ? 7 : 6;
    public int DeliveredCount => deliveries.Count;
    public int CurrentSheetNumber => Mathf.Min(DeliveredCount + 1, SheetsPerDay);
    public int TodayIncome { get; private set; }
    public float Progress => CurrentPhase is Phase.Complete or Phase.OrderComplete
        ? DeliveredCount / (float)SheetsPerDay
        : (DeliveredCount + CompletedSteps / (float)SheetSteps) / SheetsPerDay;
    public bool ComputerNotice => CurrentPhase == Phase.NewOrder || CurrentPhase == Phase.ReadyToSubmit;
    public int PatternMinutes => Math.Max(1, settings.turtleMinutes);
    public int TrimMinutes => Math.Max(1, settings.vermilionTrimMinutes);
    public DaySettlement? CurrentSettlement => CurrentPhase == Phase.Complete && settlements.Count > 0
        ? settlements[settlements.Count - 1] : null;
    public DaySettlement? PreviousSettlement => settlements.Count == Days - 1 && settlements.Count > 0
        ? settlements[settlements.Count - 1] : null;
    public IReadOnlyList<DaySettlement> Settlements { get; }
    public IReadOnlyList<SheetDelivery> Deliveries { get; }
    public SheetDelivery? LastDelivery => DeliveredCount > 0 ? deliveries[DeliveredCount - 1] : null;
    public bool CanAdvanceDay => CurrentPhase == Phase.Complete && Days < LastStoryDay;
    public bool FinalSettlementAcknowledged { get; private set; }
    public QualityGrade? FinalGrade => LastDelivery?.Grade;
    public int Reward => LastDelivery?.Reward ?? 0;
    public int BasePrice => Days switch
    {
        2 => settings.turtlePrice,
        3 => settings.borderedCurtainPrice,
        _ => settings.baseReward
    };
    public decimal QualityMultiplier => LastDelivery?.QualityMultiplier ?? 1m;
    public decimal MaterialMultiplier => LastDelivery?.MaterialMultiplier ?? 1m;
    public bool CanExplore => !DayEnded && CurrentPhase is Phase.Home or Phase.NewOrder or Phase.Production
        or Phase.ReadyToSubmit or Phase.OrderComplete;
    public bool CanHarvest => CanExplore;
    public bool CanWorkBamboo => !DayEnded && HasBamboo;
    public bool NeedsBamboo => CurrentPhase == Phase.Production && CompletedSteps == 0 && !HasBamboo;

    int CompletedSteps
    {
        get
        {
            if (CurrentPhase is Phase.ReadyToSubmit or Phase.OrderComplete)
                return SheetSteps - 1;
            if (CurrentPhase != Phase.Production)
                return 0;
            if ((IsPatternOrder || RequiresVermilionTrim) && FirstPlainCurtain() != null)
                return (int)Stage.Pattern;
            if (FirstCurtain(CurtainState.Lacquered) != null || IsDrying)
                return (int)Stage.Drying;
            if (FirstCurtain(CurtainState.Woven) != null)
                return (int)Stage.Lacquer;
            if (WorkshopInventory.TotalThreads >= BambooCraftProgress.ThreadsPerBatch)
                return (int)Stage.Weaving;
            var craft = WorkshopInventory.TrackedCraft;
            return craft?.IsProcessingBamboo == true &&
                (craft.Stage == BambooCraftProgress.Step.BatchFinished || craft.IsFinishingStage)
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
        orderRead = false;
        CurrentPhase = Phase.Production;
        UpdateOrderPhase();
        Revision++;
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
        CompleteDryingIfReady();
        if (minuteOfDay >= FirstDayEndMinute)
            SettleDay();
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
        if ((stage == Stage.Pattern && !IsPatternOrder) ||
            (stage == Stage.VermilionTrim && !RequiresVermilionTrim))
            return StageStatus.Locked;
        int steps = CompletedSteps;
        int step = StepIndex(stage);
        if (step < steps)
            return StageStatus.Complete;
        if (running == stage || (stage == Stage.Drying && IsDrying && !CanStartDrying) ||
            (CurrentPhase == Phase.Production && stage == Stage.Preparation && HasBamboo && steps == 0))
            return StageStatus.InProgress;
        return !DayEnded && CurrentPhase == Phase.Production && step == steps
            ? StageStatus.Available : StageStatus.Locked;
    }

    // The two custom orders finish with different work at the same point after drying.
    static int StepIndex(Stage stage) => stage == Stage.VermilionTrim ? (int)Stage.Pattern : (int)stage;

    public bool Submit()
    {
        if (DayEnded || CurrentPhase != Phase.ReadyToSubmit || running.HasValue)
            return false;
        Curtain curtain = FindDeliverable();
        if (curtain == null)
            return false;
        QualityGrade grade = curtain.Grade;
        decimal quality = Coefficient(grade);
        decimal material = MaterialCoefficient(curtain);
        int price = BasePrice * (curtain.UsedSpotted ? 2 : 1);
        int reward = ValueCurtain(price, quality, material);
        curtainStock.Remove(curtain);
        CurtainRevision++;
        deliveries.Add(new SheetDelivery(curtain.Weaving, curtain.Lacquer, curtain.PatternGrade, grade,
            quality, material, curtain.UsedSpotted, reward));
        ChangeFunds(reward);
        if (DeliveredCount == SheetsPerDay)
            CurrentPhase = Phase.OrderComplete;
        else
            UpdateOrderPhase();
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
            _ => (settings.xiaoPiDayOneIncome, settings.xiaoBaiDayOneIncome)
        };
        if (XiaoPiSabotaged)
            piIncome -= SabotagePenalty;
        XiaoPiFunds += piIncome;
        XiaoBaiFunds += baiIncome;
        settlements.Add(new DaySettlement(this, piIncome, baiIncome));
        running = null;
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
        running = null;
        PatternDraft = null;
        CurrentPhase = Phase.Home;
        CompleteDryingIfReady();
        Revision++;
        return true;
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
        int sum = (int)weaving + (int)lacquer + (int)pattern.GetValueOrDefault();
        return (QualityGrade)Mathf.Clamp((sum + count - 1) / count, 0, 4);
    }

    decimal MaterialCoefficient(Curtain curtain) => curtain.UsedDefective ? (decimal)settings.defectiveMaterialMultiplier : 1m;

    static int ValueCurtain(int price, decimal quality, decimal material) =>
        (int)Math.Round(price * quality * material, MidpointRounding.AwayFromZero);

    decimal Coefficient(QualityGrade grade) => (decimal)(grade switch
    {
        QualityGrade.S => settings.rewardS,
        QualityGrade.A => settings.rewardA,
        QualityGrade.B => settings.rewardB,
        QualityGrade.C => settings.rewardC,
        _ => settings.rewardD
    });
}
