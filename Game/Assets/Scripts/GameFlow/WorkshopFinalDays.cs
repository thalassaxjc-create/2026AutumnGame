public sealed partial class WorkshopOrder
{
    public enum LessonTopic { LevelFrame, TurtleKnot, ThinCorner }
    public enum BatchMethod { HandWeave, HandLacquer, Partner }

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
    public bool CanPlanBatch => Days == 4 && DeliveredCount > 0 && TrialResolved &&
        CurrentPhase == Phase.Production && completedStages == 0 && !batchMaterialReady && !running.HasValue;
    public bool HasBatchMaterial => WorkshopInventory.TotalThreads >= BambooCraftProgress.ThreadsPerBatch ||
        (WorkshopInventory.GreenBamboo > 0 && WorkshopInventory.TrackedCraft?.IsProcessingBamboo != true);

    bool batchMaterialReady;
    int partnerIncome;

    public bool TryTurnTrial()
    {
        if (!PendingTrial || TrialTurned || MinutesUntilClose <= 10)
            return false;
        ThinCornerFollowsCurtain = (int)WeavingResult.Value.grade > (int)QualityGrade.B;
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

    public bool TryPrepareBatch(BatchMethod method)
    {
        if (method is not (BatchMethod.HandWeave or BatchMethod.HandLacquer or BatchMethod.Partner) ||
            !CanPlanBatch || !HasBatchMaterial || (method == BatchMethod.Partner && Funds < PartnerFee))
            return false;
        int minutes = method switch
        {
            BatchMethod.HandWeave => 45,
            BatchMethod.HandLacquer => 75,
            _ => 95
        };
        if (MinutesUntilClose <= minutes)
            return false;

        if (WorkshopInventory.TotalThreads >= BambooCraftProgress.ThreadsPerBatch)
        {
            if (!WorkshopInventory.ConsumeThreads(BambooCraftProgress.ThreadsPerBatch, out bool defective, out bool spotted))
                return false;
            UsedDefectiveMaterials = defective;
            UsedSpottedBamboo = spotted;
        }
        else if (!GreenBambooBag.TryTakeOne())
            return false;

        batchMaterialReady = true;
        if (method != BatchMethod.HandWeave)
        {
            WeavingResult = new WeavingMinigameController.WeavingResult { grade = WeavingMinigameController.WeavingGrade.B };
            completedStages = (int)Stage.Lacquer;
        }
        if (method != BatchMethod.HandLacquer)
            LacquerResult = new LacquerMinigameController.LacquerResult { grade = LacquerMinigameController.LacquerGrade.B };
        if (method == BatchMethod.Partner)
        {
            completedStages = (int)Stage.Drying;
            ChangeFunds(-PartnerFee);
            XiaoPiFunds += PartnerFee;
            partnerIncome += PartnerFee;
        }
        AdvanceTime(minutes);
        return true;
    }

    public bool TryTeachLesson(LessonTopic topic, bool correct)
    {
        if (!CanTeachLesson || topic is not (LessonTopic.LevelFrame or LessonTopic.TurtleKnot or LessonTopic.ThinCorner) ||
            (topic == LessonTopic.TurtleKnot && !CanUseTurtleSample) ||
            (topic == LessonTopic.ThinCorner && !TrialResolved))
            return false;
        TaughtLesson = topic;
        LessonSucceeded = correct;
        CurrentPhase = Phase.OrderComplete;
        ChangeFunds(correct ? 240 : 200);
        Journal.AddEvent("第五天 · 艾琳的第一张纸", LessonDescription + "\n" + LessonResult, announce: true);
        return true;
    }

    internal string LessonDescription => TaughtLesson switch
    {
        LessonTopic.TurtleKnot => "艾琳在旧龟纹样帘上找到了起针孔，试着自己收了一次线。",
        LessonTopic.ThinCorner => "艾琳把第四天的两张试纸举到窗前，照着它们检查薄角。",
        LessonTopic.LevelFrame => "艾琳把普通帘浸进纸浆，试着让两边一起离开水面。",
        _ => ""
    };

    internal string LessonResult => LessonSucceeded
        ? "艾琳举起一张匀净的纸：这张我自己捞的！"
        : "纸的一角薄得透光。艾琳还是把它收好，说下次想再试一次。";
}
