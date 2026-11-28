using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class WorkshopOrderTests
{
    [SetUp]
    public void ResetInventory() => WorkshopInventory.StartFirstDay();

    [TestCase(1f, 0, false, WeavingMinigameController.WeavingGrade.S)]
    [TestCase(.979f, 0, true, WeavingMinigameController.WeavingGrade.S)]
    [TestCase(.99f, 1, true, WeavingMinigameController.WeavingGrade.S)]
    [TestCase(.98f, 0, true, WeavingMinigameController.WeavingGrade.SPlus)]
    public void OnlyTheEquippedShuttleCanEarnSPlusWithoutMisses(float accuracy, int misses, bool upgraded,
        WeavingMinigameController.WeavingGrade expected)
    {
        var calculate = typeof(WeavingMinigameController).GetMethod("CalculateGrade", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(calculate.Invoke(null, new object[] { accuracy, misses, upgraded }), Is.EqualTo(expected));
    }

    [TestCase(false, LacquerMinigameController.LacquerGrade.S, WorkshopOrder.QualityGrade.S, 130)]
    [TestCase(true, LacquerMinigameController.LacquerGrade.S, WorkshopOrder.QualityGrade.SPlus, 140)]
    [TestCase(true, LacquerMinigameController.LacquerGrade.A, WorkshopOrder.QualityGrade.A, 115)]
    public void SPlusPremiumUsesTheFinishedCurtainGrade(bool purchased, LacquerMinigameController.LacquerGrade lacquer,
        WorkshopOrder.QualityGrade expectedGrade, int expectedPrice)
    {
        var settings = ScriptableObject.CreateInstance<WorkshopOrderSettings>();
        settings.initialFunds = WorkshopOrder.ShuttleUpgradePrice;
        try
        {
            var order = new WorkshopOrder(settings);
            StartProduction(order);
            if (purchased)
                Assert.That(order.TryBuyShuttleUpgrade(), Is.True);
            CompleteBaseCurtain(order, WeavingMinigameController.WeavingGrade.SPlus, lacquer);
            Assert.That(order.Submit(), Is.True);
            Assert.That(order.LastDelivery.Value.Weaving,
                Is.EqualTo(purchased ? WorkshopOrder.QualityGrade.SPlus : WorkshopOrder.QualityGrade.S));
            Assert.That(order.FinalGrade, Is.EqualTo(expectedGrade));
            Assert.That(order.Reward, Is.EqualTo(expectedPrice));
        }
        finally { Object.DestroyImmediate(settings); }
    }

    [Test]
    public void FirstDayFlowUsesExactTimeAndSettlesOnlyWhenDayCloses()
    {
        WorkshopOrderSettings settings = ScriptableObject.CreateInstance<WorkshopOrderSettings>();
        try
        {
            var order = new WorkshopOrder(settings);
            StartProduction(order);
            BambooCraftProgress craft = CompleteBamboo(order, false);

            Assert.That(WorkshopInventory.BambooThreads, Is.EqualTo(100));
            Assert.That(WorkshopInventory.DefectiveThreads, Is.Zero);
            Assert.That(order.Progress, Is.EqualTo(2f / 12f));

            Assert.That(order.Begin(WorkshopOrder.Stage.Weaving), Is.True);
            order.ConfirmWeaving(new WeavingMinigameController.WeavingResult
            {
                grade = WeavingMinigameController.WeavingGrade.S,
                gameMinutes = 60,
                score = 9500
            });
            Assert.That(WorkshopInventory.TotalThreads, Is.Zero);
            Assert.That(craft.TotalThreads, Is.Zero, "编帘必须同时从工位的实际库存里消耗竹丝");
            Assert.That(order.Progress, Is.EqualTo(3f / 12f));

            Assert.That(order.Begin(WorkshopOrder.Stage.Lacquer), Is.True);
            order.ConfirmLacquer(new LacquerMinigameController.LacquerResult
            {
                grade = LacquerMinigameController.LacquerGrade.A,
                gameMinutes = 20,
                score = 8200
            });
            Assert.That(order.Progress, Is.EqualTo(4f / 12f));
            Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.Production));
            Assert.That(order.BeginDrying(), Is.True);
            Assert.That(order.RemainingDryingMinutes, Is.EqualTo(120));

            order.AdvanceTime(119);
            Assert.That(order.IsDrying, Is.True);
            Assert.That(order.RemainingDryingMinutes, Is.EqualTo(1));
            Assert.That(order.Submit(), Is.False);
            order.AdvanceTime(1);

            Assert.That(order.IsDrying, Is.False);
            Assert.That(order.Progress, Is.EqualTo(5f / 12f));
            Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.ReadyToSubmit));
            Assert.That((order.Days, order.Hour, order.Minute), Is.EqualTo((1, 10, 15)));
            Assert.That(order.Submit(), Is.True);
            Assert.That(order.FinalGrade, Is.EqualTo(WorkshopOrder.QualityGrade.A));
            Assert.That(order.Reward, Is.EqualTo(115));
            Assert.That(order.Funds, Is.EqualTo(115));
            Assert.That(order.DeliveredCount, Is.EqualTo(1));
            Assert.That(order.CurrentSettlement, Is.Null);
            Assert.That(order.TryStartNextDay(), Is.False);
            Assert.That(order.Submit(), Is.False);
            Assert.That(order.Funds, Is.EqualTo(115));
            Assert.That(craft.Stage, Is.EqualTo(BambooCraftProgress.Step.PlaceBamboo));
            Assert.That(order.CanHarvest, Is.True);
            CompleteBaseCurtain(order);
            Assert.That(order.Submit(), Is.True);
            Assert.That(order.Submit(), Is.False);
            Assert.That(order.Progress, Is.EqualTo(1f));
            Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.OrderComplete));
            Assert.That(order.CurrentSettlement, Is.Null);
            Assert.That(order.CanHarvest, Is.True);
            Assert.That(order.TryStartNextDay(), Is.False);
            int beforeClose = order.ElapsedGameMinutes;
            order.AdvanceTime(10);
            Assert.That(order.ElapsedGameMinutes, Is.EqualTo(beforeClose + 10));
            Assert.That(order.CloseDay(), Is.True);
            Assert.That(order.CloseDay(), Is.False);
            Assert.That(order.CurrentSettlement.Value.DeliveredCount, Is.EqualTo(2));
            Assert.That((order.Funds, order.XiaoPiFunds, order.XiaoBaiFunds), Is.EqualTo((245, 190, 150)));
        }
        finally
        {
            Object.DestroyImmediate(settings);
        }
    }

    internal static void CompleteBaseCurtain(WorkshopOrder order,
        WeavingMinigameController.WeavingGrade weaving = WeavingMinigameController.WeavingGrade.S,
        LacquerMinigameController.LacquerGrade lacquer = LacquerMinigameController.LacquerGrade.S)
    {
        WeaveCurtain(order, weaving);
        LacquerCurtain(order, lacquer);
        Assert.That(order.BeginDrying(), Is.True);
        Assert.That(order.CanUsePatternStation, Is.False);
        Assert.That(order.Submit(), Is.False);
        order.AdvanceTime(order.RemainingDryingMinutes);
    }

    static void WeaveCurtain(WorkshopOrder order, WeavingMinigameController.WeavingGrade grade)
    {
        CompleteBamboo(order, false);
        Assert.That(order.Begin(WorkshopOrder.Stage.Weaving), Is.True);
        order.ConfirmWeaving(new WeavingMinigameController.WeavingResult
        {
            grade = grade, gameMinutes = 60
        });
    }

    static void LacquerCurtain(WorkshopOrder order, LacquerMinigameController.LacquerGrade grade)
    {
        Assert.That(order.Begin(WorkshopOrder.Stage.Lacquer), Is.True);
        order.ConfirmLacquer(new LacquerMinigameController.LacquerResult
        {
            grade = grade, gameMinutes = 20
        });
    }

    [Test]
    public void WorkstationsUseInventoryBeforeOrdersAndKeepMultipleCurtainsOvernight()
    {
        var settings = ScriptableObject.CreateInstance<WorkshopOrderSettings>();
        try
        {
            var order = new WorkshopOrder(settings);
            order.CompleteOpening();
            order.CloseIntroduction(WorkshopOrder.Phase.Village);
            Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.Home));
            Assert.That(order.CanPlay(WorkshopOrder.Stage.Weaving), Is.False);
            Assert.That(order.CanPlay(WorkshopOrder.Stage.Lacquer), Is.False);

            WeaveCurtain(order, WeavingMinigameController.WeavingGrade.S);
            WeaveCurtain(order, WeavingMinigameController.WeavingGrade.D);
            Assert.That(order.WovenCurtains, Is.EqualTo(2));
            LacquerCurtain(order, LacquerMinigameController.LacquerGrade.S);
            Assert.That(order.BeginDrying(), Is.True);
            LacquerCurtain(order, LacquerMinigameController.LacquerGrade.D);
            Assert.That(order.BeginDrying(), Is.True);
            Assert.That(order.DryingCurtains, Is.EqualTo(2));
            Assert.That(order.CanStartDrying, Is.False);
            Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.Home));
            Assert.That(order.Submit(), Is.False);

            Assert.That(order.CloseDay() && order.TryStartNextDay(), Is.True);
            Assert.That(order.PlainCurtains, Is.EqualTo(2));
            Assert.That(order.DryingCurtains, Is.Zero);
            Assert.That(order.CanUseTrimStation, Is.True);
            Assert.That(order.TrySelectPattern(CurtainPatternKind.Turtle), Is.True);
            WorkshopPatternOrderTests.WeavePattern(order.PatternDraft);
            Assert.That(order.TryConfirmPattern(), Is.True);
            Assert.That(order.PlainCurtains, Is.EqualTo(1));
            Assert.That(order.FinishedCurtains, Is.EqualTo(1));
            Assert.That(order.Submit(), Is.False);

            order.CloseIntroduction(WorkshopOrder.Phase.Home);
            order.ReadOrder();
            order.CloseComputer();
            Assert.That(order.Submit(), Is.True);
            Assert.That(order.Reward, Is.EqualTo(182));
            Assert.That(order.PlainCurtains, Is.EqualTo(1));
            Assert.That(order.Submit(), Is.False);
            Assert.That(order.CanUsePatternStation && order.CanUseTrimStation, Is.True);
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
            Assert.That(order.WovenCurtains, Is.EqualTo(1));
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
        Assert.That(craft.Place(), Is.True);
        craft.BeginStroke(new Vector2(0f, .5f));
        craft.Drag(new Vector2(1f, .5f), 1f);
        Assert.That(craft.EndStroke(), Is.True);
        for (int round = 0; round < BambooCraftProgress.ScrapingRounds; round++)
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
        for (int group = 0; group < BambooCraftProgress.FinishingGroups; group++)
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

    [Test]
    public void BlackMarketSubtractsSoldThreadsFromRealInventoryAndAllowsRebuildingStock()
    {
        var settings = ScriptableObject.CreateInstance<WorkshopOrderSettings>();
        try
        {
            var order = new WorkshopOrder(settings);
            StartProduction(order);
            var craft = CompleteBamboo(order, true);
            Assert.That(order.TrySellThreads(true), Is.False);
            order.AdvanceTime(18 * 60 - order.Hour * 60 - order.Minute);
            Assert.That(order.TrySellThreads(true), Is.True);
            Assert.That((WorkshopInventory.DefectiveThreads, craft.DefectiveThreadCount, order.TodayIncome),
                Is.EqualTo((75, 75, 5)));
            Assert.That(order.CanPlay(WorkshopOrder.Stage.Weaving), Is.False);
            Assert.That(order.TrySellThreads(false), Is.False);
            for (int i = 0; i < 3; i++)
                Assert.That(order.TrySellThreads(true), Is.True);
            Assert.That(order.TrySellThreads(true), Is.False);
            Assert.That((craft.TotalThreads, order.TodayIncome), Is.EqualTo((0, 20)));
            Assert.That(craft.Stage, Is.EqualTo(BambooCraftProgress.Step.PlaceBamboo));
            Assert.That(craft.Place(), Is.True);
            Assert.That(order.CloseDay(), Is.True);
            Assert.That(order.CurrentSettlement.Value.PlayerIncome, Is.EqualTo(20));
        }
        finally
        {
            Object.DestroyImmediate(settings);
        }
    }

    [Test]
    public void BlackMarketOpensAt1800EvenBeforeIntroductionsAndRequiresExactFunds()
    {
        var settings = ScriptableObject.CreateInstance<WorkshopOrderSettings>();
        settings.initialFunds = 20;
        try
        {
            var order = new WorkshopOrder(settings);
            order.CompleteOpening();
            order.AdvanceTime(17 * 60 + 59 - order.Hour * 60 - order.Minute);
            Assert.That(order.BlackMarketOpen, Is.False);
            Assert.That(order.TryBuyBrightener(), Is.False);
            order.AdvanceTime(1);
            Assert.That(order.BlackMarketOpen, Is.True, "支线出现只要求傍晚，不要求先完成主线对话。");
            Assert.That(order.TryBuyBrightener(), Is.True);
            Assert.That((order.Funds, order.Brightener), Is.EqualTo((0, 1)));
            Assert.That(order.TryBuyBrightener(), Is.False, "余额不足不得产生瓶子或负债。");
            Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.Village));
            order.AdvanceTime(order.MinutesUntilClose);
            Assert.That(order.BlackMarketOpen, Is.False);
            Assert.That(order.TrySabotageXiaoPi(), Is.False);
            Assert.That(order.Brightener, Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(settings); }
    }

    [Test]
    public void SabotageConsumesOneBottleAndDeductsExactly100OnceAtSettlement()
    {
        var settings = ScriptableObject.CreateInstance<WorkshopOrderSettings>();
        settings.initialFunds = 40;
        settings.initialXiaoPiFunds = 300;
        settings.xiaoPiDayOneIncome = 50;
        try
        {
            var order = new WorkshopOrder(settings);
            order.CompleteOpening();
            Assert.That(order.TrySabotageXiaoPi(), Is.False);
            order.AdvanceTime(18 * 60 - order.Hour * 60 - order.Minute);
            Assert.That(order.TryBuyBrightener(), Is.True);
            Assert.That(order.TryBuyBrightener(), Is.True);
            Assert.That(order.TrySabotageXiaoPi(), Is.True);
            Assert.That(order.TrySabotageXiaoPi(), Is.False);
            Assert.That((order.Brightener, order.Funds, order.XiaoPiFunds), Is.EqualTo((1, 0, 300)));
            order.AdvanceTime(order.MinutesUntilClose);
            Assert.That(order.CurrentSettlement.Value.XiaoPiPenalized, Is.True);
            Assert.That(order.CurrentSettlement.Value.XiaoPiIncome, Is.EqualTo(-50), "固定扣100，不以零为下限截断。");
            Assert.That(order.XiaoPiFunds, Is.EqualTo(250));
            order.AdvanceTime(20);
            Assert.That(order.CloseDay(), Is.False);
            Assert.That(order.XiaoPiFunds, Is.EqualTo(250));
            Assert.That(order.TryStartNextDay(), Is.True);
            Assert.That(order.XiaoPiSabotaged, Is.False);
            Assert.That(order.Brightener, Is.EqualTo(1), "剩余瓶子可带到次日，但前一天的破坏状态不可继承。");
            Assert.That(order.CloseDay(), Is.True);
            Assert.That(order.CurrentSettlement.Value.XiaoPiPenalized, Is.False);
            Assert.That(order.CurrentSettlement.Value.XiaoPiIncome, Is.EqualTo(settings.xiaoPiDayTwoIncome));
        }
        finally { Object.DestroyImmediate(settings); }
    }
}
