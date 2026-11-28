using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class WorkshopOrder
{
    public enum Phase { Opening, Village, Home, NewOrder, Production, ReadyToSubmit, OrderComplete, Complete }
    public enum Stage { Preparation, BambooFinishing, Weaving, Lacquer, Drying, Pattern, VermilionTrim }
    public enum StageStatus { Locked, Available, InProgress, Complete }
    public enum QualityGrade { SPlus = -1, S, A, B, C, D }
    public enum ClockSpeed { Paused = 0, Normal = 1, Double = 2, Quadruple = 4 }

    public readonly struct SheetStyle
    {
        public CurtainPatternKind Pattern { get; }
        public CurtainEdgeStyle Edge { get; }
        public string Name => Edge != CurtainEdgeStyle.None ? CurtainEdgePalette.Get(Edge).Name
            : Pattern == CurtainPatternKind.None ? "普通宣纸帘" : CurtainPatternDesign.Get(Pattern).Name + "帘";

        internal SheetStyle(CurtainPatternKind pattern, CurtainEdgeStyle edge = CurtainEdgeStyle.None)
        {
            Pattern = pattern;
            Edge = edge;
        }

        internal bool Matches(SheetStyle other) => Pattern == other.Pattern && Edge == other.Edge;
    }

    public readonly struct SheetDelivery
    {
        public SheetStyle Style { get; }
        public QualityGrade Weaving { get; }
        public QualityGrade Lacquer { get; }
        public QualityGrade? PatternGrade { get; }
        public QualityGrade Grade { get; }
        public decimal QualityMultiplier { get; }
        public decimal MaterialMultiplier { get; }
        public bool UsedSpottedBamboo { get; }
        public int Reward { get; }

        internal SheetDelivery(SheetStyle style, QualityGrade weaving, QualityGrade lacquer, QualityGrade? patternGrade,
            QualityGrade grade, decimal qualityMultiplier, decimal materialMultiplier, bool usedSpottedBamboo, int reward)
        {
            Style = style;
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
    public bool IsPatternOrder => Days == 2;
    public bool RequiresVermilionTrim => Days == 3;
    public SheetStyle NextSheetStyle
    {
        get
        {
            if (CurrentPhase == Phase.ReadyToSubmit && FindDeliverable() is { } ready)
                return ready.Style;
            for (int index = 0; index < SheetsPerDay; index++)
                if (!IsSheetDelivered(index))
                    return RequestedSheet(index);
            return RequestedSheet(SheetsPerDay - 1);
        }
    }
    public CurtainPatternKind PatternKind => NextSheetStyle.Pattern;
    public string OrderName => Days switch
    {
        2 => "龟纹帘与单丝路帘",
        3 => "朱边青与深褐",
        4 => "纸坊返修批次",
        5 => "艾琳的第一张纸",
        _ => "普通宣纸帘"
    };

    public SheetStyle RequestedSheet(int index) => Days switch
    {
        2 => new SheetStyle(index == 0 ? CurtainPatternKind.Turtle : CurtainPatternKind.SingleThread),
        3 => new SheetStyle(CurtainPatternKind.None,
            index == 0 ? CurtainEdgeStyle.VermilionJade : CurtainEdgeStyle.DarkBrown),
        _ => default
    };

    int DeliveredFor(SheetStyle style)
    {
        int count = 0;
        foreach (SheetDelivery delivery in deliveries)
            if (delivery.Style.Matches(style))
                count++;
        return count;
    }

    int RequiredThrough(int index, SheetStyle style)
    {
        int count = 0;
        for (int i = 0; i <= index; i++)
            if (RequestedSheet(i).Matches(style))
                count++;
        return count;
    }

    public bool IsSheetDelivered(int index)
    {
        SheetStyle style = RequestedSheet(index);
        return DeliveredFor(style) >= RequiredThrough(index, style);
    }

    internal bool NeedsStyle(SheetStyle style) => DeliveredFor(style) < RequiredThrough(SheetsPerDay - 1, style);
    public ClockSpeed Speed { get; private set; } = ClockSpeed.Normal;
    public int MinutesUntilClose => FirstDayEndMinute - minuteOfDay;
    public int Hour => minuteOfDay / 60;
    public int Minute => minuteOfDay % 60;
    public int ElapsedGameMinutes => elapsedGameMinutes;
    int SheetSteps => IsPatternOrder || RequiresVermilionTrim ? 7 : 6;
    public int DeliveredCount => IsTeachingDay && TaughtLesson.HasValue ? 1 : deliveries.Count;
    public int CurrentSheetNumber => Mathf.Min(DeliveredCount + 1, RequiredSheets);
    public int TodayIncome { get; private set; }
    public float Progress => CurrentPhase is Phase.Complete or Phase.OrderComplete
        ? DeliveredCount / (float)RequiredSheets
        : (DeliveredCount + CompletedSteps / (float)SheetSteps) / RequiredSheets;
    public bool ComputerNotice => !IsTeachingDay && (CurrentPhase == Phase.NewOrder || CurrentPhase == Phase.ReadyToSubmit);
    public int PatternMinutes => Math.Max(1, settings.turtleMinutes);
    public int TrimMinutes => Math.Max(1, settings.vermilionTrimMinutes);
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
    internal int SPlusPremiumPercent => Mathf.RoundToInt((settings.rewardSPlus - 1f) * 100);
    public int BasePrice => Days switch
    {
        2 => settings.turtlePrice,
        3 => settings.borderedCurtainPrice,
        4 => SampleBatch ? 130 : 150,
        5 => 240,
        _ => settings.baseReward
    };
    public decimal QualityMultiplier => LastDelivery?.QualityMultiplier ?? 1m;
    public decimal MaterialMultiplier => LastDelivery?.MaterialMultiplier ?? 1m;
    public bool CanExplore => !DayEnded && CurrentPhase is Phase.Home or Phase.NewOrder or Phase.Production
        or Phase.ReadyToSubmit or Phase.OrderComplete;
    public bool CanHarvest => CanExplore;
    public bool CanWorkBamboo => !DayEnded && !IsTeachingDay && !AwaitingReturnedCurtain && HasBamboo;
    public string IntroductionHint => CurrentPhase switch
    {
        Phase.Village => "先与村口大爷交谈，再去院子找舅舅开启订单",
        Phase.Home => "先到院子与舅舅交谈，再回工坊电脑查看订单",
        _ => null
    };
    public bool NeedsBamboo => !IsTeachingDay && !AwaitingReturnedCurtain &&
        CurrentPhase == Phase.Production && CompletedSteps == 0 && !HasBamboo;

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
        if (IsTeachingDay || AwaitingReturnedCurtain ||
            (stage == Stage.Pattern && !IsPatternOrder) ||
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
        if (DayEnded || PendingTrial || CurrentPhase != Phase.ReadyToSubmit || running.HasValue)
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
        deliveries.Add(new SheetDelivery(curtain.Style, curtain.Weaving, curtain.Lacquer, curtain.PatternGrade, grade,
            quality, material, curtain.UsedSpotted, reward));
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
            UpdateOrderPhase();
        Journal.AddEvent($"{DayName} · {OrderName}",
            WorkshopStoryUI.SheetDetails(deliveries[deliveries.Count - 1], deliveries.Count) +
            (BatchBonus > 0 ? $"\n整批通过验收，另获 {BatchBonus} 元。" : ""),
            announce: deliveries.Count == 1 && !settlements.Exists(day => day.DeliveredCount > 0));
        return true;
    }

    public bool CloseDay()
    {
        if (!CanCloseDay)
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
        running = null;
        PatternDraft = null;
        TrimDraft = null;
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
        if (weaving == QualityGrade.SPlus)
        {
            if (lacquer == QualityGrade.S && (!pattern.HasValue || pattern.Value == QualityGrade.S))
                return QualityGrade.SPlus;
            weaving = QualityGrade.S;
        }
        int count = pattern.HasValue ? 3 : 2;
        int sum = (int)weaving + (int)lacquer + (int)pattern.GetValueOrDefault();
        return (QualityGrade)Mathf.Clamp((sum + count - 1) / count, 0, 4);
    }

    internal static string GradeText(QualityGrade grade) => grade == QualityGrade.SPlus ? "S+" : grade.ToString();

    decimal MaterialCoefficient(Curtain curtain) => curtain.UsedDefective ? (decimal)settings.defectiveMaterialMultiplier : 1m;

    static int ValueCurtain(int price, decimal quality, decimal material) =>
        (int)Math.Round(price * quality * material, MidpointRounding.AwayFromZero);

    decimal Coefficient(QualityGrade grade) => (decimal)(grade switch
    {
        QualityGrade.SPlus => settings.rewardSPlus,
        QualityGrade.S => settings.rewardS,
        QualityGrade.A => settings.rewardA,
        QualityGrade.B => settings.rewardB,
        QualityGrade.C => settings.rewardC,
        _ => settings.rewardD
    });
}
