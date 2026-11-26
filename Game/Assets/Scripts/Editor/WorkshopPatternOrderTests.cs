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
        var draft = order.PatternDraft;
        order.OpenComputer();
        order.ReadOrder();
        order.CloseComputer();
        Assert.That(order.PatternDraft, Is.SameAs(draft));
        Assert.That(order.CanHarvest, Is.True);
        Assert.That(order.CanPlay(WorkshopOrder.Stage.Weaving), Is.False);
        Loop(draft, 1);
        int before = order.ElapsedGameMinutes;
        Assert.That(order.TryConfirmPattern(), Is.True);
        Assert.That(order.TryConfirmPattern(), Is.False);
        Assert.That(order.HasPatternBlank, Is.False);
        Assert.That(order.ElapsedGameMinutes - before, Is.EqualTo(20));
        Assert.That(order.Submit(), Is.True);
        Assert.That(order.Submit(), Is.False);
        Assert.That(order.FinalGrade, Is.EqualTo(WorkshopOrder.QualityGrade.B));
        Assert.That(order.Reward, Is.EqualTo(140));
        Assert.That(order.TryStartNextDay(), Is.False);
        Assert.That(order.PatternDraft, Is.Null);
        WorkshopOrderTests.CompleteBaseCurtain(order);
        Loop(order.PatternDraft, 1);
        Loop(order.PatternDraft, 3);
        Assert.That(order.TryConfirmPattern(), Is.True);
        Assert.That(order.Submit(), Is.True);
        Assert.That(order.Reward, Is.EqualTo(182));
        Assert.That(order.CloseDay(), Is.True);
        Assert.That(order.TryStartNextDay(), Is.True);
        Assert.That(order.PatternResult, Is.Null);
        ReadNewOrder();
        for (int sheet = 0; sheet < WorkshopOrder.SheetsPerDay; sheet++)
        {
            WorkshopOrderTests.CompleteBaseCurtain(order);
            draft = order.PatternDraft;
            for (int i = 0; i < CurtainPatternDraft.TurtleCount; i++)
                Loop(draft, i);
            Assert.That(draft.TrySwitchThread(), Is.True);
            Loop(draft, CurtainPatternDraft.Frame, true);
            before = order.ElapsedGameMinutes;
            Assert.That(order.TryConfirmPattern(), Is.True);
            Assert.That(order.ElapsedGameMinutes - before, Is.EqualTo(30));
            Assert.That(order.Submit(), Is.True);
            Assert.That(order.Reward, Is.EqualTo(234));
        }
        Assert.That(order.CloseDay(), Is.True);
        Assert.That((order.Funds, order.XiaoPiFunds, order.XiaoBaiFunds), Is.EqualTo((790, 884, 762)));
        Assert.That(order.TryStartNextDay(), Is.True);
        Assert.That(order.RequiredSheets, Is.EqualTo(3));
        order.CloseIntroduction(WorkshopOrder.Phase.Home, 1);
        order.OpenComputer();
        order.ReadOrder();
        order.CloseComputer();
        Assert.That(order.BasePrice, Is.EqualTo(130));
        for (int sheet = 1; sheet <= order.RequiredSheets; sheet++)
        {
            if (sheet == 1)
            {
                WorkshopOrderTests.CompleteBaseCurtain(order);
                Assert.That(order.Submit(), Is.False);
                Assert.That(order.TryTurnTrial(), Is.True);
                Assert.That(order.TryTurnTrial(), Is.False);
                Assert.That(order.TryRepairTrial(true), Is.False);
                Assert.That(order.TryRepairTrial(false), Is.True);
            }
            else
            {
                Assert.That(order.AwaitingReturnedCurtain, Is.True);
                Assert.That(order.Begin(WorkshopOrder.Stage.Lacquer), Is.False);
                Assert.That(order.TryFinishReturnedCurtain(false), Is.True);
                Assert.That(order.TryFinishReturnedCurtain(false), Is.False);
                Assert.That(order.FinishedCurtainCount, Is.EqualTo(1));
            }
            Assert.That(order.Submit(), Is.True);
            Assert.That(order.BatchBonus, Is.EqualTo(sheet == 3 ? 120 : 0));
        }
        Assert.That(order.CloseDay(), Is.True);
        Assert.That(order.CurrentSettlement.Value.Delivered, Is.True);
        Assert.That(order.CurrentSettlement.Value.RequiredSheets, Is.EqualTo(3));
        Assert.That(order.TryStartNextDay(), Is.True);
        order.CloseIntroduction(WorkshopOrder.Phase.Home);
        Assert.That(order.Begin(WorkshopOrder.Stage.Weaving), Is.False);
        Assert.That(order.CanUseTurtleSample, Is.True);
        Assert.That(order.CloseDay(), Is.False);
        Assert.That(order.TryFinishLesson(WorkshopOrder.LessonTopic.ThinCorner, true,
            WorkshopOrder.PaperDestination.Visitor), Is.True);
        Assert.That(order.TryFinishLesson(WorkshopOrder.LessonTopic.LevelFrame, true,
            WorkshopOrder.PaperDestination.Visitor), Is.False);
        Assert.That(order.FirstPaperDestination, Is.EqualTo(WorkshopOrder.PaperDestination.Visitor));
        Assert.That(order.DeliveredCount, Is.EqualTo(1));
        Assert.That(order.LastDelivery, Is.Null);
        Assert.That(order.TodayIncome, Is.EqualTo(240));
        Assert.That(order.CloseDay(), Is.True);
        Assert.That(order.CurrentSettlement.Value.Delivered, Is.True);
        Assert.That(order.TryStartNextDay(), Is.False);
    }

    [Test]
    public void PartialBatchKeepsItsPaymentAndStillOpensDayFive()
    {
        for (int day = 1; day <= 3; day++)
        {
            order.AdvanceTime(order.MinutesUntilClose);
            Assert.That(order.TryStartNextDay(), Is.True);
        }

        order.CloseIntroduction(WorkshopOrder.Phase.Home, 1);
        order.OpenComputer();
        order.ReadOrder();
        order.CloseComputer();
        WorkshopOrderTests.CompleteBaseCurtain(order);
        Assert.That(order.TryTurnTrial(), Is.True);
        Assert.That(order.TryRepairTrial(false), Is.True);
        Assert.That(order.Submit(), Is.True);
        Assert.That(order.CloseDay(), Is.True);
        var report = order.CurrentSettlement.Value;
        Assert.That(report.Delivered, Is.False);
        Assert.That(report.DeliveredCount, Is.EqualTo(1));
        Assert.That(report.BatchBonus, Is.Zero);
        Assert.That(report.PlayerIncome, Is.EqualTo(169));
        Assert.That(order.TryStartNextDay(), Is.True);
        Assert.That(order.Days, Is.EqualTo(5));
    }

    [Test]
    public void ConfirmationRequiresTimeToDeliverAndTimeoutClearsBlank()
    {
        order.AdvanceTime(order.MinutesUntilClose);
        Assert.That(order.TryStartNextDay(), Is.True);
        ReadNewOrder();
        WorkshopOrderTests.CompleteBaseCurtain(order);
        Loop(order.PatternDraft, 1);
        Assert.That(order.TryConfirmPattern(), Is.True);
        Assert.That(order.Submit(), Is.True);
        WorkshopOrderTests.CompleteBaseCurtain(order);
        Loop(order.PatternDraft, 1);
        order.AdvanceTime(order.MinutesUntilClose - order.PatternMinutes);
        Assert.That(order.TryConfirmPattern(), Is.False);
        Assert.That(order.HasPatternBlank, Is.True);
        order.AdvanceTime(order.MinutesUntilClose);
        Assert.That(order.CurrentSettlement.Value.Delivered, Is.False);
        Assert.That(order.CanUsePatternStation, Is.False);
        Assert.That(order.HasPatternBlank, Is.False);
        Assert.That(order.CurrentSettlement.Value.DeliveredCount, Is.EqualTo(1));
        Assert.That(order.CurrentSettlement.Value.PlayerIncome, Is.EqualTo(140));
        Assert.That((order.Funds, order.XiaoPiFunds, order.XiaoBaiFunds), Is.EqualTo((140, 470, 402)));
    }

    [Test]
    public void TeaTipUnlocksOnePremiumSaleOnDayTwo()
    {
        order.CompleteOpening();
        order.CloseIntroduction(WorkshopOrder.Phase.Village);
        Assert.That(order.TryFinishTeaNotice(), Is.True);
        Assert.That(order.TryFinishTeaNotice(), Is.False);
        Assert.That((order.TodayIncome, order.ElapsedGameMinutes), Is.EqualTo((30, 30)));
        Assert.That(order.CollectorAvailable, Is.False);
        Assert.That(order.CloseDay(), Is.True);
        Assert.That(order.TryStartNextDay(), Is.True);
        ReadNewOrder();
        Assert.That(order.CollectorAvailable, Is.True);

        WorkshopOrderTests.CompleteBaseCurtain(order);
        Loop(order.PatternDraft, 1);
        Assert.That(order.TryConfirmPattern(), Is.True);
        Assert.That(order.TrySellToCollector(), Is.False, "富商不收 B 级");
        Assert.That(order.Submit(), Is.True);

        WorkshopOrderTests.CompleteBaseCurtain(order);
        Loop(order.PatternDraft, 1);
        Loop(order.PatternDraft, 3);
        Assert.That(order.TryConfirmPattern(), Is.True);
        Assert.That(order.TrySellToCollector(), Is.True);
        Assert.That(order.LastDelivery.Value.SoldToCollector, Is.True);
        Assert.That((order.Reward, order.DeliveredCount, order.TodayIncome), Is.EqualTo((260, 2, 400)));
        Assert.That(order.TrySellToCollector(), Is.False);
        Assert.That(order.CurrentSettlement, Is.Null);
    }

    [Test]
    public void DetourLosesOneTargetStitchAndUndoRestoresOriginalPath()
    {
        var draft = new CurtainPatternDraft(CurtainPatternKind.Turtle);
        Assert.That(draft.TryStitch(0, 0), Is.False);
        Assert.That(draft.TryStitch(1, 0), Is.True);
        Assert.That(draft.TryStitch(1, 3), Is.False);
        Assert.That(draft.TryStitch(1, 1), Is.True);
        Assert.That(draft.TryStitch(1, CurtainPatternDraft.DetourPin), Is.True);
        Assert.That(draft.Undo(), Is.True);
        Assert.That(draft.CurrentPin, Is.EqualTo(1));
        for (int step = 2; step <= 6; step++)
            Assert.That(draft.TryStitch(1, step % 6), Is.True);
        foreach (int pin in new[] { 0, 1, CurtainPatternDraft.DetourPin, 2, 3, 4, 5, 0 })
            Assert.That(draft.TryStitch(3, pin), Is.True);
        Assert.That(draft.Evaluate().Correct, Is.EqualTo(11));
        Assert.That(draft.Evaluate().Grade, Is.EqualTo(WorkshopOrder.QualityGrade.A));
    }

    void ReadNewOrder()
    {
        order.CloseIntroduction(WorkshopOrder.Phase.Home);
        order.OpenComputer();
        order.CloseComputer();
        Assert.That(order.HasPatternBlank, Is.False);
        order.ReadOrder();
        order.CloseComputer();
        Assert.That(order.HasPatternBlank, Is.False);
    }

    static void Loop(CurtainPatternDraft draft, int loop, bool attach = false)
    {
        for (int step = 0; step <= CurtainPatternDraft.MainPins; step++)
        {
            int pin = step % CurtainPatternDraft.MainPins;
            Assert.That(draft.TryStitch(loop, pin), Is.True);
            if (attach && CurtainPatternDraft.IsJoin(pin))
            {
                Assert.That(draft.TryPress(), Is.True);
                Assert.That(draft.TryPress(), Is.False);
            }
        }
    }
}
