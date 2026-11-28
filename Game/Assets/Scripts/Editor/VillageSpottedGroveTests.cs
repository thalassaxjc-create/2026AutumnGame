using NUnit.Framework;
using UnityEngine;

public sealed class VillageSpottedGroveTests
{
    [TestCase(0, 4, true)]
    [TestCase(0, 5.5f, true)]
    [TestCase(0, 6, false)]
    [TestCase(4, 0, false)]
    [TestCase(0, -3, false)]
    [TestCase(0, -.1f, true)]
    public void DogVisionUsesFrontConeAndCloseContact(float x, float z, bool expected) =>
        Assert.That(VillageSpottedGrove.SeesPlayer(Vector3.zero, Vector3.forward, new Vector3(x, 1, z)), Is.EqualTo(expected));

    [Test]
    public void SuccessfulTheftAwardsTwoAndSellingBothPays120WithoutRepeatingReward()
    {
        var settings = ScriptableObject.CreateInstance<WorkshopOrderSettings>();
        try
        {
            var order = new WorkshopOrder(settings);
            Assert.That(order.TryResolveSpottedBambooTheft(false), Is.False, "序章中不能领奖。");
            order.CompleteOpening();
            Assert.That(order.TryResolveSpottedBambooTheft(false), Is.True);
            Assert.That(order.SpottedBamboo, Is.EqualTo(2));
            Assert.That(order.TryResolveSpottedBambooTheft(false), Is.False);
            order.CloseIntroduction(WorkshopOrder.Phase.Village);
            Assert.That(order.TrySellSpottedBamboo(), Is.True);
            Assert.That(order.TrySellSpottedBamboo(), Is.True);
            Assert.That(order.TrySellSpottedBamboo(), Is.False);
            Assert.That((order.SpottedBamboo, order.Funds), Is.EqualTo((0, 120)));
            Assert.That(order.TryBuySpottedBamboo(), Is.True);
            Assert.That((order.SpottedBamboo, order.Funds), Is.EqualTo((1, 20)));
            Assert.That(order.TryBuySpottedBamboo(), Is.False);
            order.AdvanceTime(order.MinutesUntilClose);
            Assert.That(order.TryStartNextDay(), Is.True);
            Assert.That(order.CanStealSpottedBamboo, Is.False, "已经采完的竹丛不能跨天重复领奖。");
        }
        finally { Object.DestroyImmediate(settings); }
    }

    [Test]
    public void BeingCaughtDeducts100OnceAndAllowsRetryNextDay()
    {
        var settings = ScriptableObject.CreateInstance<WorkshopOrderSettings>();
        try
        {
            var order = new WorkshopOrder(settings);
            order.CompleteOpening();
            Assert.That(order.TryResolveSpottedBambooTheft(true), Is.True);
            Assert.That(order.TryResolveSpottedBambooTheft(true), Is.False);
            Assert.That(order.TryResolveSpottedBambooTheft(false), Is.False);
            Assert.That((order.Funds, order.SpottedBamboo), Is.EqualTo((-100, 0)));
            order.AdvanceTime(order.MinutesUntilClose);
            Assert.That(order.TryStartNextDay(), Is.True);
            Assert.That(order.CanStealSpottedBamboo, Is.True);
            Assert.That(order.TryResolveSpottedBambooTheft(false), Is.True);
            Assert.That((order.Funds, order.SpottedBamboo), Is.EqualTo((-100, 2)));
        }
        finally { Object.DestroyImmediate(settings); }
    }
}
