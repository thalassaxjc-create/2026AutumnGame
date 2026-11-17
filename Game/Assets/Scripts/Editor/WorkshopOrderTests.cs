using NUnit.Framework;
using UnityEngine;

public sealed class WorkshopOrderTests
{
    [SetUp]
    public void ResetInventory() => WorkshopInventory.StartFirstDay();

    [Test]
    public void FirstDayFlowUsesExactTimeDryingAndOnePayment()
    {
        WorkshopOrderSettings settings = ScriptableObject.CreateInstance<WorkshopOrderSettings>();
        try
        {
            var order = new WorkshopOrder(settings);
            StartProduction(order);
            BambooCraftProgress craft = CompleteBamboo(order, false);

            Assert.That(WorkshopInventory.BambooThreads, Is.EqualTo(100));
            Assert.That(WorkshopInventory.DefectiveThreads, Is.Zero);
            Assert.That(order.Progress, Is.EqualTo(.4f));

            Assert.That(order.Begin(WorkshopOrder.Stage.Weaving), Is.True);
            order.ConfirmWeaving(new WeavingMinigameController.WeavingResult
            {
                grade = WeavingMinigameController.WeavingGrade.S,
                gameMinutes = 60,
                score = 9500
            });
            Assert.That(WorkshopInventory.TotalThreads, Is.Zero);
            Assert.That(order.Progress, Is.EqualTo(.6f));

            Assert.That(order.Begin(WorkshopOrder.Stage.Lacquer), Is.True);
            order.ConfirmLacquer(new LacquerMinigameController.LacquerResult
            {
                grade = LacquerMinigameController.LacquerGrade.A,
                gameMinutes = 20,
                score = 8200
            });
            Assert.That(order.Progress, Is.EqualTo(.8f));
            Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.Production));
            Assert.That(order.BeginDrying(), Is.True);
            Assert.That(order.RemainingDryingMinutes, Is.EqualTo(120));

            order.AdvanceTime(119);
            Assert.That(order.IsDrying, Is.True);
            Assert.That(order.RemainingDryingMinutes, Is.EqualTo(1));
            Assert.That(order.Submit(), Is.False);
            order.AdvanceTime(1);

            Assert.That(order.IsDrying, Is.False);
            Assert.That(order.Progress, Is.EqualTo(1f));
            Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.ReadyToSubmit));
            Assert.That((order.Days, order.Hour, order.Minute), Is.EqualTo((1, 10, 45)));
            Assert.That(order.Submit(), Is.True);
            Assert.That(order.FinalGrade, Is.EqualTo(WorkshopOrder.QualityGrade.A));
            Assert.That(order.Reward, Is.EqualTo(115));
            Assert.That(order.Funds, Is.EqualTo(115));
            Assert.That(order.XiaoPiFunds, Is.EqualTo(95));
            Assert.That(order.XiaoBaiFunds, Is.EqualTo(75));
            Assert.That(order.Submit(), Is.False);
            Assert.That(order.Funds, Is.EqualTo(115));
            Assert.That(craft.Stage, Is.EqualTo(BambooCraftProgress.Step.CraftFinished));
        }
        finally
        {
            Object.DestroyImmediate(settings);
        }
    }

    [Test]
    public void DefectiveThreadsApplyMaterialMultiplierAndGradeAverageRoundsDownInQuality()
    {
        WorkshopOrderSettings settings = ScriptableObject.CreateInstance<WorkshopOrderSettings>();
        try
        {
            var order = new WorkshopOrder(settings);
            StartProduction(order);
            CompleteBamboo(order, true);
            Assert.That(WorkshopInventory.DefectiveThreads, Is.EqualTo(100));

            Assert.That(order.Begin(WorkshopOrder.Stage.Weaving), Is.True);
            order.ConfirmWeaving(new WeavingMinigameController.WeavingResult
            {
                grade = WeavingMinigameController.WeavingGrade.S,
                gameMinutes = 60
            });
            Assert.That(order.UsedDefectiveMaterials, Is.True);
            Assert.That(order.Begin(WorkshopOrder.Stage.Lacquer), Is.True);
            order.ConfirmLacquer(new LacquerMinigameController.LacquerResult
            {
                grade = LacquerMinigameController.LacquerGrade.C,
                gameMinutes = 20
            });
            Assert.That(order.BeginDrying(), Is.True);
            order.AdvanceTime(120);
            Assert.That(order.Submit(), Is.True);

            Assert.That(order.FinalGrade, Is.EqualTo(WorkshopOrder.QualityGrade.B), "S 与 C 折中为 B");
            Assert.That(order.QualityMultiplier, Is.EqualTo(1m));
            Assert.That(order.MaterialMultiplier, Is.EqualTo(.8m));
            Assert.That(order.Reward, Is.EqualTo(80));
        }
        finally
        {
            Object.DestroyImmediate(settings);
        }
    }

    [Test]
    public void RealTimeClockUsesTwoSecondsPerMinutePausesAndStopsAtMidnight()
    {
        WorkshopOrderSettings settings = ScriptableObject.CreateInstance<WorkshopOrderSettings>();
        try
        {
            var order = new WorkshopOrder(settings);
            order.Tick(20f, true);
            Assert.That((order.Hour, order.Minute), Is.EqualTo((6, 0)), "序章期间不走时");
            order.CompleteOpening();
            order.Tick(1.99f, true);
            Assert.That((order.Hour, order.Minute), Is.EqualTo((6, 0)));
            order.Tick(.01f, true);
            Assert.That((order.Hour, order.Minute), Is.EqualTo((6, 1)));
            order.Tick(100f, false);
            Assert.That((order.Hour, order.Minute), Is.EqualTo((6, 1)), "模态界面暂停时间");
            order.AdvanceTime(24 * 60);
            Assert.That((order.Hour, order.Minute), Is.EqualTo((24, 0)));
            Assert.That(order.DayEnded, Is.True);
            order.AdvanceTime(30);
            Assert.That((order.Hour, order.Minute), Is.EqualTo((24, 0)));
        }
        finally
        {
            Object.DestroyImmediate(settings);
        }
    }

    static void StartProduction(WorkshopOrder order)
    {
        order.CompleteOpening();
        order.CloseIntroduction(WorkshopOrder.Phase.Village);
        order.CloseIntroduction(WorkshopOrder.Phase.Home);
        order.ReadOrder();
        order.CloseComputer();
        Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.Production));
    }

    static BambooCraftProgress CompleteBamboo(WorkshopOrder order, bool forceDefects)
    {
        var craft = new BambooCraftProgress(() => forceDefects ? 0f : .99f, () => true);
        order.PickUpBamboo();
        Assert.That(order.Begin(WorkshopOrder.Stage.Preparation), Is.True);
        Assert.That(craft.Place(), Is.True);
        craft.BeginStroke(new Vector2(0f, .5f));
        craft.Drag(new Vector2(1f, .5f), 1f);
        Assert.That(craft.EndStroke(), Is.True);
        for (int piece = 0; piece < 4; piece++)
        {
            Assert.That(craft.StartNextStrip(), Is.True);
            for (int row = 1; row < 12; row += 3)
            {
                var from = new Vector2(0f, row / 11f);
                craft.BeginStroke(from);
                craft.Drag(new Vector2(1f, from.y), forceDefects ? .001f : 2f);
                craft.EndStroke();
            }
        }
        Assert.That(craft.Stage, Is.EqualTo(BambooCraftProgress.Step.BatchFinished));
        order.ObserveBamboo(craft);

        Assert.That(craft.BeginGates(), Is.True);
        for (int tick = 0; tick < 1600 && craft.IsGateStage; tick++)
            craft.TickFinishing(.05f, Mathf.Abs(craft.GateHeight - craft.PieceY) <= .025f
                ? 0f : Mathf.Sign(craft.GateHeight - craft.PieceY), false);
        Assert.That(craft.IsGateStage, Is.False);
        for (int group = 0; group < 4; group++)
        {
            Assert.That(craft.BeginDrawingGroup(), Is.True);
            bool pulling = true;
            for (int tick = 0; tick < 1200 && craft.Stage == BambooCraftProgress.Step.Drawing; tick++)
            {
                if (craft.Tension >= .44f) pulling = false;
                if (craft.Tension <= .08f) pulling = true;
                craft.TickFinishing(.05f, 0f, pulling);
            }
        }
        Assert.That(craft.Stage, Is.EqualTo(BambooCraftProgress.Step.CraftFinished));
        order.ObserveBamboo(craft);
        return craft;
    }
}
