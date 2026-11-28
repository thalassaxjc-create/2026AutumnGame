public sealed partial class WorkshopOrder
{
    public enum PaperDestination { Visitor, Workshop, Journal }

    public const int PartnerFee = 65;
    public const int LessonClosingHour = 18;
    public bool IsTeachingDay => Days == LastStoryDay;
    public bool TrialTurned { get; private set; }
    public bool TrialResolved { get; private set; }
    public bool ThreadRepaired { get; private set; }
    public bool WarpRepaired { get; private set; }
    public bool NeedsThreadRepair => Days == 4 && !ThreadRepaired && DeliveredCount == 0 &&
        CurrentPhase == Phase.Production;
    public bool NeedsWarpRepair => Days == 4 && ThreadRepaired && !WarpRepaired && DeliveredCount >= 1 &&
        CurrentPhase == Phase.Production && PlainCurtains == 0 && AvailableOrderCurtains == 0;
    public bool AwaitingReturnedCurtain => NeedsThreadRepair || NeedsWarpRepair;
    public bool PendingTrial => false;
    public bool ThinCornerFollowsCurtain => false;
    public CurtainPatternKind AilinPattern => CurtainPatternKind.TiedFlower;
    public enum LessonBeat { None, Scrape, Weave, Pattern, Advice }

    public bool LessonPrepared { get; private set; }
    public LessonBeat CurrentLesson { get; private set; }
    public bool LessonAdviceGiven { get; private set; }
    public string PatternFault { get; private set; } = "";
    public string PatternCritique => PatternFault switch
    {
        "反过来" => "艾琳，第二道和第一道是反的。上下要倒过来，不能照搬。",
        "贴样稿" => "艾琳，木梭飘离样稿了。按住梭，贴着线走。",
        _ => "艾琳，花心这样扎就对了。"
    };
    public bool AilinCurtainWoven { get; private set; }
    public QualityGrade? AilinWeaveGrade { get; private set; }
    public CurtainPatternKind? TaughtLesson { get; private set; }
    public bool LessonSucceeded { get; private set; }
    public bool CanTeachLesson => IsTeachingDay && CurrentPhase == Phase.Production && Hour < LessonClosingHour;
    public bool CanCloseDay => CanExplore && (!IsTeachingDay || Hour >= LessonClosingHour || TaughtLesson.HasValue);
    public PaperDestination? FirstPaperDestination { get; private set; }

    int partnerIncome;

    public bool TryFinishThreadRepair()
    {
        if (!NeedsThreadRepair || MinutesUntilClose <= 10)
            return false;
        AddRepairedCurtain();
        ThreadRepaired = true;
        StockChanged(10);
        Journal.AddEvent("第四天 · 补上断线", "龟纹断掉的那一段顺着原路接了回去。这张还要改成扎花。", announce: true);
        return true;
    }

    public bool TryFinishWarpRepair()
    {
        if (!NeedsWarpRepair || MinutesUntilClose <= 10)
            return false;
        AddRepairedCurtain();
        WarpRepaired = true;
        StockChanged(10);
        Journal.AddEvent("第四天 · 压平翘边", "翘边跟着帘走，松边拉平了。这张还要收一圈朱边青。", announce: true);
        return true;
    }

    void AddRepairedCurtain()
    {
        curtainStock.Add(new Curtain(QualityGrade.B, defective: false, spotted: false)
        {
            Lacquer = QualityGrade.B,
            State = CurtainState.Dried
        });
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

    public bool PrepareAilinLesson()
    {
        if (!CanTeachLesson || LessonPrepared || AilinCurtainWoven)
            return false;
        LessonPrepared = true;
        CurrentLesson = LessonBeat.Scrape;
        Revision++;
        return true;
    }

    public bool CompleteLessonPass(LessonBeat beat)
    {
        if (!LessonPrepared || CurrentLesson != beat)
            return false;
        if (beat == LessonBeat.Scrape)
            CurrentLesson = LessonBeat.Weave;
        else if (beat == LessonBeat.Weave)
        {
            if (PlainCurtains == 0)
                AddRepairedCurtain();
            CurrentLesson = LessonBeat.Pattern;
        }
        else
            return false;
        Revision++;
        return true;
    }

    public void RecordAilinWeave(QualityGrade? grade, CurtainPatternDraft draft)
    {
        if (!IsTeachingDay || !LessonPrepared || grade == null || CurrentLesson != LessonBeat.Pattern)
            return;
        AilinCurtainWoven = true;
        AilinWeaveGrade = grade;
        int reversedWrong = 0;
        if (draft != null)
        {
            for (int column = 0; column < CurtainPatternDraft.CrossingsPerRow; column++)
                if (draft.ResultAt(1, column) == CurtainPatternDraft.CrossingResult.Wrong)
                    reversedWrong++;
            PatternFault = reversedWrong > 0 ? "反过来" : draft.AlignmentScore < 28 ? "贴样稿" : "就这样";
        }
        else
            PatternFault = "就这样";
        CurrentLesson = LessonBeat.Advice;
        Revision++;
    }

    public bool TryGivePatternAdvice(string advice)
    {
        if (CurrentLesson != LessonBeat.Advice || LessonAdviceGiven || advice != PatternFault)
            return false;
        LessonAdviceGiven = true;
        Revision++;
        return true;
    }

    public bool TryFinishLesson(PaperDestination destination)
    {
        if (!CanTeachLesson || !AilinCurtainWoven || !LessonAdviceGiven || TaughtLesson.HasValue || destination is not
            (PaperDestination.Visitor or PaperDestination.Workshop or PaperDestination.Journal))
            return false;
        TaughtLesson = AilinPattern;
        LessonSucceeded = AilinWeaveGrade is QualityGrade grade && grade <= QualityGrade.B;
        FirstPaperDestination = destination;
        CurrentPhase = Phase.OrderComplete;
        ChangeFunds(LessonSucceeded ? 240 : 200);
        Journal.AddEvent("第五天 · 艾琳的第一张帘", LessonDescription + "\n" + LessonResult + "\n" + PaperDestinationText,
            announce: true);
        return true;
    }

    internal string PaperDestinationText => FirstPaperDestination switch
    {
        PaperDestination.Visitor => "艾琳把这张扎花帘卷好，放进旅行包带走。",
        PaperDestination.Workshop => "你们把这张扎花帘挂在工坊的墙上。",
        PaperDestination.Journal => "你把这张扎花帘的做法收进手册。",
        _ => ""
    };

    internal string LessonDescription => TaughtLesson.HasValue
        ? "艾琳从海外来学非遗宣纸帘。你教她刮青、编一道素帘，再教扎花。做完告诉她哪里要改。"
        : "";

    internal string LessonResult => LessonSucceeded
        ? "艾琳举起那张扎花帘：这张是我自己做的。"
        : "花心松了一圈。艾琳还是把帘收好，说下次想再扎紧一点。";
}
