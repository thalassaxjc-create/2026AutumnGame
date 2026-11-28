public sealed partial class WorkshopOrder
{
    public enum LessonTopic { LevelFrame, TurtleKnot, ThinCorner }
    public enum PaperDestination { Visitor, Workshop, Journal }

    public const int PartnerFee = 65;
    public const int LessonClosingHour = 18;
    public bool IsTeachingDay => Days == LastStoryDay;
    public bool TrialTurned { get; private set; }
    public bool TrialResolved { get; private set; }
    public bool PendingTrial => Days == 4 && DeliveredCount == 0 && CurrentPhase == Phase.ReadyToSubmit && !TrialResolved;
    public bool ThinCornerFollowsCurtain { get; private set; }
    public LessonTopic? TaughtLesson { get; private set; }
    public bool LessonSucceeded { get; private set; }
    public bool CanTeachLesson => IsTeachingDay && CurrentPhase == Phase.Production && Hour < LessonClosingHour;
    public bool CanUseTurtleSample => settlements.Exists(day => day.Day == 2 && day.DeliveredCount > 0);
    public bool AwaitingReturnedCurtain => Days == 4 && DeliveredCount > 0 &&
        CurrentPhase == Phase.Production;
    public bool CanCloseDay => CanExplore && (!IsTeachingDay || Hour >= LessonClosingHour || TaughtLesson.HasValue);
    public PaperDestination? FirstPaperDestination { get; private set; }

    int partnerIncome;

    public bool TryTurnTrial()
    {
        Curtain curtain = FindDeliverable();
        if (!PendingTrial || TrialTurned || curtain == null || MinutesUntilClose <= 10)
            return false;
        ThinCornerFollowsCurtain = (int)curtain.Weaving > (int)QualityGrade.B;
        TrialTurned = true;
        AdvanceTime(10);
        return true;
    }

    public bool TryRepairTrial(bool repairCurtain)
    {
        if (!PendingTrial || !TrialTurned || MinutesUntilClose <= 25)
            return false;
        bool fixedFault = repairCurtain == ThinCornerFollowsCurtain;
        AdvanceTime(fixedFault ? 25 : 15);
        if (fixedFault)
        {
            TrialResolved = true;
            Journal.AddEvent("第四天 · 两张试纸", repairCurtain
                ? "薄角跟着帘转。重新拉平松边之后，薄角消失了。"
                : "薄角留在原处。调平夹架之后，薄角消失了。", announce: true);
            Revision++;
        }
        return fixedFault;
    }

    public bool TryFinishReturnedCurtain(bool hiredPartner)
    {
        int minutes = DeliveredCount == 1 ? 20 : hiredPartner ? 40 : 25;
        if (!AwaitingReturnedCurtain || !TrialResolved || MinutesUntilClose <= minutes ||
            (hiredPartner && (DeliveredCount != 2 || Funds < PartnerFee)))
            return false;

        curtainStock.Add(new Curtain(QualityGrade.B, defective: false, spotted: false)
        {
            Lacquer = QualityGrade.B,
            State = CurtainState.Dried
        });
        if (hiredPartner)
        {
            ChangeFunds(-PartnerFee);
            XiaoPiFunds += PartnerFee;
            partnerIncome += PartnerFee;
        }
        StockChanged(minutes);
        return true;
    }

    public bool CanUseLessonSample(LessonTopic topic) => CanTeachLesson && (topic switch
    {
        LessonTopic.LevelFrame => true,
        LessonTopic.TurtleKnot => CanUseTurtleSample,
        LessonTopic.ThinCorner => settlements.Exists(day => day.Day == 4 && day.DeliveredCount >= 2),
        _ => false
    });

    public bool TryFinishLesson(LessonTopic topic, bool correct, PaperDestination destination)
    {
        if (!CanUseLessonSample(topic) || destination is not
            (PaperDestination.Visitor or PaperDestination.Workshop or PaperDestination.Journal))
            return false;
        TaughtLesson = topic;
        LessonSucceeded = correct;
        FirstPaperDestination = destination;
        CurrentPhase = Phase.OrderComplete;
        ChangeFunds(correct ? 240 : 200);
        Journal.AddEvent("第五天 · 艾琳的第一张纸", LessonDescription + "\n" + LessonResult + "\n" + PaperDestinationText,
            announce: true);
        return true;
    }

    internal string PaperDestinationText => FirstPaperDestination switch
    {
        PaperDestination.Visitor => "盖好村印后，艾琳把纸夹进旅行本带走。",
        PaperDestination.Workshop => "盖好村印后，你们把纸挂在工坊的墙上。",
        PaperDestination.Journal => "盖好村印后，你把纸夹进小册子。",
        _ => ""
    };

    internal string LessonDescription => TaughtLesson switch
    {
        LessonTopic.TurtleKnot => "你和艾琳用旧龟纹样帘练习，顺着花纹看纸浆铺开的样子。",
        LessonTopic.ThinCorner => "你和艾琳用修过的旧帘试捞，还拿昨天的试纸对照了薄角。",
        LessonTopic.LevelFrame => "艾琳把普通帘浸进纸浆，试着让两边一起离开水面。",
        _ => ""
    };

    internal string LessonResult => LessonSucceeded
        ? "艾琳举起一张匀净的纸：这张我自己捞的！"
        : "纸的一角薄得透光。艾琳还是把它收好，说下次想再试一次。";
}
