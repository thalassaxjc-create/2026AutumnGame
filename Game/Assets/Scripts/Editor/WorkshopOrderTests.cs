using NUnit.Framework;
using UnityEngine;

public sealed class WorkshopOrderTests
{
    [TestCase(WeavingMinigameController.WeavingGrade.A, 850)]
    [TestCase(WeavingMinigameController.WeavingGrade.D, 600)]
    public void SequenceTimeReplaysAndOnePayment(WeavingMinigameController.WeavingGrade grade, int reward)
    {
        var settings = ScriptableObject.CreateInstance<WorkshopOrderSettings>();
        try
        {
            settings.initialHour = 23;
            settings.initialMinute = 50;
            var order = new WorkshopOrder(settings);
            var craft = new BambooCraftProgress(() => .5f, () => true);
            order.CloseIntroduction(WorkshopOrder.Phase.Home);
            Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.Village));
            Assert.That(order.Begin(WorkshopOrder.Stage.Lacquer), Is.False);
            order.CloseIntroduction(WorkshopOrder.Phase.Village);
            order.CloseIntroduction(WorkshopOrder.Phase.Home);
            order.CloseComputer();
            Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.NewOrder));
            order.ReadOrder();
            order.CloseComputer();
            Assert.That(order.CanHarvest, Is.True);
            Assert.That(order.Submit(), Is.False);
            order.PickUpBamboo();
            Assert.That(order.Begin(WorkshopOrder.Stage.Preparation), Is.True);
            Assert.That(craft.Place(), Is.True);
            craft.BeginStroke(new Vector2(0, .5f));
            craft.Drag(new Vector2(1, .5f), 1);
            craft.EndStroke();
            order.ObserveBamboo(craft);
            Assert.That(order.Progress, Is.Zero);

            for (int piece = 0; piece < 4; piece++)
            {
                Assert.That(craft.StartNextStrip(), Is.True);
                for (int row = 1; row < 12; row += 3)
                {
                    var from = new Vector2(0, row / 11f);
                    craft.BeginStroke(from);
                    craft.Drag(new Vector2(1, from.y), 2);
                    craft.EndStroke();
                }
                order.ObserveBamboo(craft);
                Assert.That(order.Progress, Is.EqualTo(piece == 3 ? .25f : 0));
            }

            Assert.That(craft.BeginGates(), Is.True);
            for (int tick = 0; tick < 1600 && craft.IsGateStage; tick++)
                craft.TickFinishing(.05f, Mathf.Abs(craft.GateHeight - craft.PieceY) <= .025f
                    ? 0 : Mathf.Sign(craft.GateHeight - craft.PieceY), false);
            for (int group = 0; group < 4; group++)
            {
                Assert.That(craft.BeginDrawingGroup(), Is.True);
                bool pulling = true;
                for (int tick = 0; tick < 1200 && craft.Stage == BambooCraftProgress.Step.Drawing; tick++)
                {
                    if (craft.Tension >= .44f) pulling = false;
                    if (craft.Tension <= .08f) pulling = true;
                    craft.TickFinishing(.05f, 0, pulling);
                }
                order.ObserveBamboo(craft);
                Assert.That(order.Progress, Is.EqualTo(group == 3 ? .50f : .25f));
            }
            Assert.That(order.Begin(WorkshopOrder.Stage.Weaving), Is.True);
            order.Leave(WorkshopOrder.Stage.Weaving);
            order.ConfirmWeaving(default);
            Assert.That(order.Progress, Is.EqualTo(.50f));
            Assert.That(order.Begin(WorkshopOrder.Stage.Weaving), Is.True);
            var weaving = new WeavingMinigameController.WeavingResult
                { grade = WeavingMinigameController.WeavingGrade.S, gameMinutes = 60, score = 9500 };
            order.ConfirmWeaving(weaving);
            Assert.That(order.Progress, Is.EqualTo(.75f));
            Assert.That(order.Begin(WorkshopOrder.Stage.Weaving), Is.True);
            weaving.grade = grade;
            weaving.score = 4200;
            order.ConfirmWeaving(weaving);
            order.ConfirmWeaving(default);
            Assert.That(order.WeavingResult.Value.score, Is.EqualTo(4200));
            Assert.That(order.Begin(WorkshopOrder.Stage.Lacquer), Is.True);
            order.ConfirmLacquer(new LacquerMinigameController.LacquerResult
                { grade = LacquerMinigameController.LacquerGrade.B, gameMinutes = 20 });
            Assert.That(order.Progress, Is.EqualTo(1f));
            Assert.That((order.Days, order.Hour, order.Minute), Is.EqualTo((2, 2, 10)));
            Assert.That(order.Funds, Is.EqualTo(102));
            Assert.That(order.Submit(), Is.True);
            Assert.That(order.Reward, Is.EqualTo(reward));
            Assert.That(order.Funds, Is.EqualTo(102 + reward));
            Assert.That(order.Submit(), Is.False);
            Assert.That(order.Funds, Is.EqualTo(102 + reward));
        }
        finally
        {
            Object.DestroyImmediate(settings);
        }
    }
}
