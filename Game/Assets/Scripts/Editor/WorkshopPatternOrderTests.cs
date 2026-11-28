using NUnit.Framework;
using UnityEngine;

public sealed class WorkshopPatternOrderTests
{
    WorkshopOrderSettings settings;
    WorkshopOrder order;

    [SetUp]
    public void CreateOrder()
    {
        WorkshopInventory.StartFirstDay();
        settings = ScriptableObject.CreateInstance<WorkshopOrderSettings>();
        order = new WorkshopOrder(settings);
    }

    [TearDown]
    public void DisposeSettings() => Object.DestroyImmediate(settings);

    [Test]
    public void TeaNoticeIsAvailableBeforeElderDialogueButNotDuringOpeningOrAfterClosing()
    {
        Assert.That(order.CanVisitTeaStall, Is.False);
        Assert.That(order.TryFinishTeaNotice(), Is.False);
        order.CompleteOpening();
        Assert.That(order.CanExplore, Is.False);
        Assert.That(order.CanVisitTeaStall, Is.True);
        Assert.That(order.TryFinishTeaNotice(), Is.True);
        Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.Village));
        Assert.That(order.Funds, Is.EqualTo(30));
        order.AdvanceTime(order.MinutesUntilClose);
        Assert.That(order.CanVisitTeaStall, Is.False);
    }

    [Test]
    public void ToolsAndOfficialTipChargeOnceAndPaperInspectionPaysByCorrectCount()
    {
        settings.initialFunds = 100;
        order = new WorkshopOrder(settings);
        order.CompleteOpening();
        Assert.That(order.TryBuyKnifeUpgrade(), Is.False);
        Assert.That(order.TryBuyShuttleUpgrade(), Is.False);
        Assert.That(order.TryBuyOfficialTip(), Is.False);
        order.CloseIntroduction(WorkshopOrder.Phase.Village);
        ReadNewOrder();
        Assert.That(order.TryBuyKnifeUpgrade(), Is.True);
        Assert.That(order.TryBuyKnifeUpgrade(), Is.False);
        Assert.That(order.TryBuyShuttleUpgrade(), Is.True);
        Assert.That(order.TryBuyShuttleUpgrade(), Is.False);
        Assert.That(order.HasUpgradedKnife && order.HasAutomaticShuttle, Is.True);
        Assert.That(order.Funds, Is.EqualTo(50));
        order.AdvanceTime(6 * 60);
        Assert.That(order.TryBuyOfficialTip(), Is.False);
        Assert.That(order.OfficialStewardAvailable, Is.False);
        Assert.That(order.CloseDay(), Is.True);
        Assert.That(order.TryStartNextDay(), Is.True);
        Assert.That(order.Days, Is.EqualTo(2));
        Assert.That(order.TryBuyOfficialTip(), Is.True);
        Assert.That(order.TryBuyOfficialTip(), Is.False);
        Assert.That(order.Funds, Is.Zero);
        Assert.That(order.OfficialStewardAvailable, Is.False);
        Assert.That(order.OpenOfficialInspection(), Is.Null);
        WorkshopOrderTests.CompleteBaseCurtain(order);
        int minuteOfDay = order.Hour * 60 + order.Minute;
        order.AdvanceTime(WorkshopOrder.OfficialOpeningHour * 60 - minuteOfDay);
        Assert.That(order.OfficialStewardAvailable, Is.True);
        var papers = order.OpenOfficialInspection();
        Assert.That(papers, Is.Not.Null);
        int before = order.ElapsedGameMinutes;
        var incorrect = (PaperInspectionRound.Condition)(((int)papers.Current.Defect + 1) % 4);
        Assert.That(order.TryClassifyOfficialPaper(incorrect), Is.True);
        Assert.That(order.OpenOfficialInspection(), Is.SameAs(papers));
        Assert.That(order.Funds, Is.Zero);
        Assert.That(order.ElapsedGameMinutes, Is.EqualTo(before));
        while (!papers.Complete)
            Assert.That(order.TryClassifyOfficialPaper(papers.Current.Defect), Is.True);
        Assert.That(order.TryClassifyOfficialPaper(PaperInspectionRound.Condition.Clean), Is.False);
        Assert.That(order.OpenOfficialInspection(), Is.Null);
        Assert.That(order.OfficialJobCompleted, Is.True);
        Assert.That(order.Funds, Is.EqualTo(150));
        Assert.That(order.TodayIncome, Is.EqualTo(100));
        Assert.That(order.ElapsedGameMinutes - before, Is.EqualTo(WorkshopOrder.OfficialJobMinutes));
        Assert.That(order.PlainCurtains, Is.EqualTo(1));
        Assert.That(order.DeliveredCount, Is.Zero);
        order.AdvanceTime(6 * 60);
        Assert.That(order.OfficialStewardAvailable, Is.False);
    }

    [Test]
    public void ClockScalesOnlyAutomaticTimeAndKeepsFractions()
    {
        order.CompleteOpening();
        order.Tick(1f, true);
        Assert.That(order.Minute, Is.Zero);
        order.SetClockSpeed(WorkshopOrder.ClockSpeed.Paused);
        order.Tick(100f, true);
        Assert.That(order.Minute, Is.Zero);
        order.SetClockSpeed(WorkshopOrder.ClockSpeed.Double);
        order.Tick(.5f, true);
        Assert.That(order.Minute, Is.EqualTo(1));
        order.SetClockSpeed(WorkshopOrder.ClockSpeed.Quadruple);
        order.Tick(2f, false);
        Assert.That(order.Minute, Is.EqualTo(1));
        order.Tick(2f, true);
        Assert.That(order.Minute, Is.EqualTo(5));
        order.SetClockSpeed(WorkshopOrder.ClockSpeed.Paused);
        order.AdvanceTime(20);
        Assert.That(order.Minute, Is.EqualTo(25));
    }

    [Test]
    public void TimeoutAdvancesToPatternOrdersAndEachPayoutIsRecordedOnce()
    {
        order.AdvanceTime(order.MinutesUntilClose);
        order.AdvanceTime(10);
        Assert.That(order.Settlements.Count, Is.EqualTo(1));
        Assert.That((order.Funds, order.XiaoPiFunds, order.XiaoBaiFunds), Is.EqualTo((0, 190, 150)));
        Assert.That(order.CurrentSettlement.Value.Delivered, Is.False);
        Assert.That(order.TryStartNextDay(), Is.True);
        Assert.That(order.TryStartNextDay(), Is.False);
        Assert.That((order.Days, order.Hour, order.Minute), Is.EqualTo((2, 6, 0)));

        ReadNewOrder();
        Assert.That(order.PatternDraft, Is.Null);
        Assert.That(order.CanHarvest, Is.True);
        WorkshopOrderTests.CompleteBaseCurtain(order);
        Assert.That(order.PatternDraft, Is.Null, "仍需玩家选样");
        Assert.That(order.TrySelectPattern(CurtainPatternKind.DoubleThread), Is.True);
        Assert.That(order.TrySelectPattern(CurtainPatternKind.SingleThread), Is.True);
        var draft = order.PatternDraft;
        Assert.That(order.TryConfirmPattern(), Is.False);
        Assert.That(order.Submit(), Is.False);
        order.OpenComputer();
        order.ReadOrder();
        order.CloseComputer();
        Assert.That(order.PatternDraft, Is.SameAs(draft));
        Assert.That(order.CanHarvest, Is.True);
        Assert.That(order.CanPlay(WorkshopOrder.Stage.Weaving), Is.False);
        WeavePattern(draft);
        int before = order.ElapsedGameMinutes;
        Assert.That(order.TryConfirmPattern(), Is.True);
        Assert.That(order.TryConfirmPattern(), Is.False);
        Assert.That(order.PlainCurtains, Is.Zero);
        Assert.That(order.GetStatus(WorkshopOrder.Stage.Pattern), Is.EqualTo(WorkshopOrder.StageStatus.Complete));
        Assert.That(order.ElapsedGameMinutes - before, Is.EqualTo(20));
        Assert.That(order.Submit(), Is.True);
        Assert.That(order.Submit(), Is.False);
        Assert.That(order.FinalGrade, Is.EqualTo(WorkshopOrder.QualityGrade.S));
        Assert.That(order.LastDelivery.Value.Style.Pattern, Is.EqualTo(CurtainPatternKind.SingleThread));
        Assert.That(order.IsSheetDelivered(1), Is.True);
        Assert.That(order.IsSheetDelivered(0), Is.False);
        Assert.That(order.LastDelivery.Value.PatternGrade, Is.EqualTo(WorkshopOrder.QualityGrade.S));
        Assert.That(order.Reward, Is.EqualTo(182));
        Assert.That(order.TryStartNextDay(), Is.False);
        Assert.That(order.PatternDraft, Is.Null);
        WorkshopOrderTests.CompleteBaseCurtain(order, lacquer: LacquerMinigameController.LacquerGrade.A);
        WeaveAndSelect(CurtainPatternKind.Turtle, wrongCrossings: 4);
        before = order.ElapsedGameMinutes;
        Assert.That(order.TryConfirmPattern(), Is.True);
        Assert.That(order.TryConfirmPattern(), Is.False);
        Assert.That(order.ElapsedGameMinutes - before, Is.EqualTo(20));
        Assert.That(order.Submit(), Is.True);
        Assert.That(order.LastDelivery.Value.Style.Pattern, Is.EqualTo(CurtainPatternKind.Turtle));
        Assert.That(order.LastDelivery.Value.PatternGrade, Is.EqualTo(WorkshopOrder.QualityGrade.B));
        Assert.That(order.FinalGrade, Is.EqualTo(WorkshopOrder.QualityGrade.A));
        Assert.That(order.Reward, Is.EqualTo(161));
        Assert.That(order.CloseDay(), Is.True);
        Assert.That(order.TryStartNextDay(), Is.True);
        Assert.That(order.PatternDraft, Is.Null);
        ReadNewOrder();
        for (int sheet = 0; sheet < WorkshopOrder.SheetsPerDay; sheet++)
        {
            Assert.That(order.CanUseTrimStation, Is.False);
            Assert.That(order.TryConfirmVermilionTrim(), Is.False);
            WorkshopOrderTests.CompleteBaseCurtain(order);
            Assert.That(order.PatternKind, Is.EqualTo(CurtainPatternKind.None));
            Assert.That(order.PatternDraft, Is.Null);
            Assert.That(order.CanUsePatternStation, Is.True);
            Assert.That(order.GetStatus(WorkshopOrder.Stage.Pattern), Is.EqualTo(WorkshopOrder.StageStatus.Locked));
            Assert.That(order.Progress, Is.EqualTo((sheet + 5f / 7f) / WorkshopOrder.SheetsPerDay));
            before = order.ElapsedGameMinutes;
            Assert.That(order.CanUseTrimStation, Is.True);
            Assert.That(order.Submit(), Is.False, "阴干后要到上漆边台加工");
            Assert.That(order.TryConfirmVermilionTrim(), Is.False);
            CurtainEdgeStyle style = order.RequestedSheet(WorkshopOrder.SheetsPerDay - 1 - sheet).Edge;
            BindEdges(order.OpenTrimDraft(style));
            Assert.That(order.TryConfirmVermilionTrim(), Is.True);
            Assert.That(order.TryConfirmVermilionTrim(), Is.False);
            Assert.That(order.GetStatus(WorkshopOrder.Stage.VermilionTrim), Is.EqualTo(WorkshopOrder.StageStatus.Complete));
            Assert.That(order.ElapsedGameMinutes - before, Is.EqualTo(30));
            Assert.That(order.Submit(), Is.True);
            Assert.That(order.LastDelivery.Value.Style.Edge, Is.EqualTo(style));
            Assert.That(order.LastDelivery.Value.PatternGrade, Is.Null);
            Assert.That(order.Reward, Is.EqualTo(234), "在包边内拖贴完成即按编帘和刷漆品相结算");
        }
        Assert.That(order.CloseDay(), Is.True);
        Assert.That((order.Funds, order.XiaoPiFunds, order.XiaoBaiFunds), Is.EqualTo((811, 884, 762)));
        Assert.That(order.TryStartNextDay(), Is.False);
    }

    [Test]
    public void ConfirmationNeedsTimeToWorkAndTimeoutRetainsBlank()
    {
        order.AdvanceTime(order.MinutesUntilClose);
        Assert.That(order.TryStartNextDay(), Is.True);
        ReadNewOrder();
        WorkshopOrderTests.CompleteBaseCurtain(order);
        WeaveAndSelect(CurtainPatternKind.Turtle);
        Assert.That(order.TryConfirmPattern(), Is.True);
        Assert.That(order.Submit(), Is.True);
        WorkshopOrderTests.CompleteBaseCurtain(order);
        WeaveAndSelect(CurtainPatternKind.Turtle);
        order.AdvanceTime(order.MinutesUntilClose - order.PatternMinutes + 1);
        Assert.That(order.TryConfirmPattern(), Is.False);
        Assert.That(order.PlainCurtains, Is.EqualTo(1));
        order.AdvanceTime(order.MinutesUntilClose);
        Assert.That(order.CurrentSettlement.Value.Delivered, Is.False);
        Assert.That(order.CanUsePatternStation, Is.False);
        Assert.That(order.PlainCurtains, Is.EqualTo(1));
        Assert.That(order.CurrentSettlement.Value.DeliveredCount, Is.EqualTo(1));
        Assert.That(order.CurrentSettlement.Value.PlayerIncome, Is.EqualTo(182));
        Assert.That((order.Funds, order.XiaoPiFunds, order.XiaoBaiFunds), Is.EqualTo((182, 470, 402)));
    }

    [TestCase(0, WorkshopOrder.QualityGrade.S, 260)]
    [TestCase(4, WorkshopOrder.QualityGrade.A, 230)]
    public void CollectorBuysOnlyHighGradeRibbedCurtainsOutsideTheOrder(int wrongCrossings,
        WorkshopOrder.QualityGrade combinedGrade, int salePrice)
    {
        order.CompleteOpening();
        order.CloseIntroduction(WorkshopOrder.Phase.Village);
        Assert.That(order.TryFinishTeaNotice(), Is.True);
        Assert.That(order.TryFinishTeaNotice(), Is.False);
        Assert.That((order.TodayIncome, order.ElapsedGameMinutes), Is.EqualTo((30, 30)));
        Assert.That(order.CollectorAvailable, Is.False);
        Assert.That(order.CloseDay(), Is.True);
        Assert.That(order.TryStartNextDay(), Is.True);
        Assert.That(order.CollectorAvailable, Is.True);

        WorkshopOrderTests.CompleteBaseCurtain(order);
        WeaveAndSelect(CurtainPatternKind.Turtle);
        Assert.That(order.TryConfirmPattern(), Is.True);
        Assert.That(order.CollectorCurtainGrade, Is.Null);
        Assert.That(order.TrySellToCollector(), Is.False, "S 级龟纹保留给订单");

        WorkshopOrderTests.CompleteBaseCurtain(order,
            WeavingMinigameController.WeavingGrade.A, LacquerMinigameController.LacquerGrade.A);
        WeaveAndSelect(CurtainPatternKind.Ribbed, wrongCrossings: 12);
        Assert.That(order.TryConfirmPattern(), Is.True);
        Assert.That(order.CollectorCurtainGrade, Is.EqualTo(WorkshopOrder.QualityGrade.B));
        Assert.That(order.TrySellToCollector(), Is.False, "综合等级考虑纹样");

        WorkshopOrderTests.CompleteBaseCurtain(order);
        WeaveAndSelect(CurtainPatternKind.Ribbed, wrongCrossings);
        Assert.That(order.TryConfirmPattern(), Is.True);
        Assert.That(order.CollectorCurtainGrade, Is.EqualTo(combinedGrade));
        Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.Home));
        Assert.That(order.TrySellToCollector(), Is.True);
        Assert.That(order.LastDelivery, Is.Null);
        Assert.That((order.DriedCurtains, order.DeliveredCount, order.TodayIncome), Is.EqualTo((2, 0, salePrice)));
        Assert.That(order.CollectorCurtainGrade, Is.EqualTo(WorkshopOrder.QualityGrade.B));
        Assert.That(order.TrySellToCollector(), Is.False);
        ReadNewOrder();
        Assert.That(order.Submit(), Is.True);
        Assert.That(order.Submit(), Is.False, "剩下的罗纹帘不能用于龟纹单");
        Assert.That(order.CloseDay(), Is.True);
        Assert.That(order.CurrentSettlement.Value.DeliveredCount, Is.EqualTo(1));
        Assert.That(order.CurrentSettlement.Value.PlayerIncome, Is.EqualTo(salePrice + 182));
    }

    [TestCase(29, false)]
    [TestCase(30, true)]
    public void TeaNoticeChargesOnceOnlyWhenThirtyMinutesRemain(int remainingMinutes, bool succeeds)
    {
        order.CompleteOpening();
        order.CloseIntroduction(WorkshopOrder.Phase.Village);
        order.AdvanceTime(order.MinutesUntilClose - remainingMinutes);
        int beforeTime = order.ElapsedGameMinutes;
        int beforeFunds = order.Funds;
        Assert.That(order.TryFinishTeaNotice(), Is.EqualTo(succeeds));
        Assert.That(order.TryFinishTeaNotice(), Is.False);
        Assert.That(order.TeaNoticeCompleted, Is.EqualTo(succeeds));
        Assert.That(order.HasCollectorTip, Is.EqualTo(succeeds));
        Assert.That(order.ElapsedGameMinutes - beforeTime, Is.EqualTo(succeeds ? 30 : 0));
        Assert.That(order.Funds - beforeFunds, Is.EqualTo(succeeds ? 30 : 0));
    }

    [Test]
    public void GripReleaseAndRedoPreserveOnlyTheFinalPass()
    {
        var draft = new CurtainPatternDraft(CurtainPatternKind.Turtle);
        Assert.That(draft.TryGrip(draft.GuidePosition(0, .5f)), Is.False);
        Assert.That(draft.TryGrip(draft.Shuttle), Is.True);
        Assert.That(draft.Drag(draft.GuidePosition(0, .25f), under: true), Is.True);
        Assert.That(draft.ResultAt(0, 0), Is.EqualTo(CurtainPatternDraft.CrossingResult.Wrong));
        draft.Release();
        Assert.That(draft.Drag(draft.GuidePosition(0, .45f), under: false), Is.False);
        Assert.That(draft.WorkedFraction(0), Is.EqualTo(.25f));
        Assert.That(draft.TryGrip(draft.Shuttle), Is.True);
        Assert.That(draft.Drag(draft.GuidePosition(0, 1f), under: true), Is.True);
        Assert.That(draft.CompletedRows, Is.EqualTo(1));
        Assert.That(draft.CanConfirm, Is.False);
        Assert.That(draft.RedoRow(), Is.True);
        Assert.That(draft.CompletedRows, Is.Zero);
        Assert.That(draft.WorkedFraction(0), Is.Zero);
        Assert.That(draft.Score, Is.Zero);
        Assert.That(draft.ResultAt(0, 0), Is.EqualTo(CurtainPatternDraft.CrossingResult.Pending));
        WeavePattern(draft);
        Assert.That(draft.Score, Is.EqualTo(100));
    }

    [TestCase(0, 100, WorkshopOrder.QualityGrade.S)]
    [TestCase(1, 95, WorkshopOrder.QualityGrade.A)]
    [TestCase(5, 75, WorkshopOrder.QualityGrade.B)]
    [TestCase(8, 60, WorkshopOrder.QualityGrade.C)]
    [TestCase(12, 40, WorkshopOrder.QualityGrade.D)]
    public void TheSameDragPathScoresTheSameWithDifferentEventCounts(int wrongCrossings,
        int expectedScore, WorkshopOrder.QualityGrade grade)
    {
        var coarse = new CurtainPatternDraft(CurtainPatternKind.Turtle);
        var fine = new CurtainPatternDraft(CurtainPatternKind.Turtle);
        WeavePattern(coarse, wrongCrossings);
        WeavePattern(fine, wrongCrossings, stepsPerSegment: 8);
        Assert.That(coarse.CanConfirm && fine.CanConfirm, Is.True);
        Assert.That(coarse.AlignmentScore, Is.EqualTo(40));
        Assert.That((coarse.Score, coarse.Grade), Is.EqualTo((expectedScore, grade)));
        Assert.That((fine.Score, fine.Grade), Is.EqualTo((expectedScore, grade)));

        var wide = new CurtainPatternDraft(CurtainPatternKind.Turtle);
        WeavePattern(wide, wrongCrossings, offset: .31f);
        Assert.That(wide.AlignmentScore, Is.Zero);
        Assert.That(wide.ThreadingScore, Is.EqualTo(coarse.ThreadingScore));
    }

    [TestCase(CurtainPatternKind.SingleThread)]
    [TestCase(CurtainPatternKind.DoubleThread)]
    [TestCase(CurtainPatternKind.Ribbed)]
    [TestCase(CurtainPatternKind.Turtle)]
    [TestCase(CurtainPatternKind.TiedFlower)]
    public void EachPatternCanFillTheEntireCurtain(CurtainPatternKind kind)
    {
        var draft = new CurtainPatternDraft(kind);
        WeavePattern(draft);
        for (int row = 0; row < CurtainPatternDraft.RowCount; row++)
            Assert.That(draft.WorkedFraction(row), Is.EqualTo(1f));
        Assert.That(draft.CanConfirm, Is.True);
        Assert.That(draft.Score, Is.EqualTo(100));
    }

    [TestCase(CurtainEdgeStyle.VermilionJade)]
    [TestCase(CurtainEdgeStyle.PaleBamboo)]
    [TestCase(CurtainEdgeStyle.LacquerRusset)]
    [TestCase(CurtainEdgeStyle.DarkBrown)]
    public void TrimFollowsTheRoundedCornerAndCanResumeFromTheLastCheckpoint(CurtainEdgeStyle style)
    {
        var draft = new VermilionTrimDraft(style);
        Assert.That(draft.TryGrip(VermilionTrimDraft.EdgePosition(0, .5f)), Is.False, "从边中间起手无效");
        Assert.That(draft.TryGrip(draft.Front), Is.True);
        Assert.That(draft.Drag(VermilionTrimDraft.EdgePosition(0, 1f)), Is.True);
        Assert.That(draft.Turning, Is.True);
        Assert.That(draft.Drag(VermilionTrimDraft.EdgePosition(1, .6f)), Is.False);
        Assert.That(draft.SealedCorners, Is.Zero);
        draft.Release();
        Assert.That(draft.Drag(VermilionTrimDraft.GuidePoint(draft.LaidSteps + 1)), Is.False);
        BindEdges(draft);
        Assert.That(draft.Complete, Is.True);
        Assert.That(draft.Style, Is.EqualTo(style));
        Assert.That(draft.SealedCorners, Is.EqualTo(VermilionTrimDraft.SideCount));
    }

    void ReadNewOrder()
    {
        order.CloseIntroduction(WorkshopOrder.Phase.Home);
        order.OpenComputer();
        order.CloseComputer();
        Assert.That(order.PlainCurtains, Is.Zero);
        order.ReadOrder();
        order.CloseComputer();
        Assert.That(order.PlainCurtains, Is.Zero);
    }

    void WeaveAndSelect(CurtainPatternKind kind, int wrongCrossings = 0)
    {
        Assert.That(order.TrySelectPattern(kind), Is.True);
        WeavePattern(order.PatternDraft, wrongCrossings);
    }

    internal static void WeavePattern(CurtainPatternDraft draft, int wrongCrossings = 0,
        int stepsPerSegment = 1, float offset = 0f)
    {
        for (int row = 0; row < CurtainPatternDraft.RowCount; row++)
        {
            const int crossings = CurtainPatternDraft.CrossingsPerRow;
            Assert.That(draft.TryGrip(draft.Shuttle), Is.True);
            if (offset != 0f)
                Assert.That(draft.Drag(draft.Shuttle + Vector2.up * offset, under: false), Is.True);
            for (int segment = 0; segment <= crossings; segment++)
            {
                Vector2 from = draft.Shuttle;
                float end = segment < crossings ? CurtainPatternDraft.CrossingProgress(segment) + .008f : 1f;
                Vector2 to = draft.GuidePosition(row, end) + Vector2.up * offset;
                bool under = segment < crossings &&
                    (draft.GoesUnder(row, segment) ^ (row * crossings + segment < wrongCrossings));
                for (int step = 1; step <= stepsPerSegment; step++)
                    Assert.That(draft.Drag(Vector2.Lerp(from, to, step / (float)stepsPerSegment), under), Is.True);
            }
            Assert.That(draft.CompletedRows, Is.EqualTo(row + 1));
        }
    }

    static void BindEdges(VermilionTrimDraft draft)
    {
        Assert.That(draft.TryGrip(draft.Front), Is.True);
        while (!draft.Complete)
            Assert.That(draft.Drag(VermilionTrimDraft.GuidePoint(draft.LaidSteps + 1)), Is.True);
    }
}
