using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class WorkshopOrderSceneTests
{
    InputSettings.BackgroundBehavior previousBackground;
    InputSettings.EditorInputBehaviorInPlayMode previousEditorBehavior;
    bool restoreInput;

    [UnityTearDown]
    public IEnumerator Restore()
    {
        if (restoreInput)
        {
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorBehavior;
        }
        if (Application.isPlaying)
            yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator StoryOrderAndSettlementUseExistingSceneObjects()
    {
        EditorSceneManager.OpenScene(SceneTransitionState.WorldScenePath);
        yield return new EnterPlayMode();
        previousBackground = InputSystem.settings.backgroundBehavior;
        previousEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        restoreInput = true;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var mouse = InputSystem.AddDevice<Mouse>();
        var order = WorkshopOrder.Current;
        Assert.That(order, Is.Not.Null);
        // A full-suite Play Mode reload can finish the opening before the runner resumes.
        Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.Opening).Or.EqualTo(WorkshopOrder.Phase.Village));
        var opening = Object.FindFirstObjectByType<OpeningSequence>();
        Assert.That(opening, Is.Not.Null);
        opening.CompleteImmediately();
        yield return Frames(2);
        Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.Village));
        Click(GameObject.Find("时间 · 暂停"));
        Assert.That(order.Speed, Is.EqualTo(WorkshopOrder.ClockSpeed.Paused));
        Click(GameObject.Find("时间 · 2×"));
        Assert.That(order.Speed, Is.EqualTo(WorkshopOrder.ClockSpeed.Double));
        Click(GameObject.Find("时间 · 4×"));
        Assert.That(order.Speed, Is.EqualTo(WorkshopOrder.ClockSpeed.Quadruple));
        Click(GameObject.Find("时间 · 正常"));
        Assert.That(order.Speed, Is.EqualTo(WorkshopOrder.ClockSpeed.Normal));
        Assert.That(GameObject.Find("小皮"), Is.Not.Null);
        Assert.That(GameObject.Find("小白"), Is.Not.Null);
        var task = GameObject.Find("任务提示").GetComponent<RectTransform>();
        Assert.That(task.anchorMin, Is.EqualTo(Vector2.one));
        Assert.That(task.anchorMax, Is.EqualTo(Vector2.one));
        Assert.That(task.pivot, Is.EqualTo(Vector2.one));
        Assert.That(task.anchoredPosition, Is.EqualTo(new Vector2(-32f, -48f)),
            "订单任务应独占右上区域，不能与左上角竹子背包重叠");
        var elder = GameObject.Find("村口大爷").GetComponent<TemporaryDialogueEntryPoint>();
        var uncle = GameObject.Find("舅舅").GetComponent<TemporaryDialogueEntryPoint>();
        MoveNear(uncle.transform);
        yield return Press(keyboard, Key.E);
        Assert.That(uncle.IsOpen, Is.False);
        MoveNear(elder.transform);
        yield return Press(keyboard, Key.E);
        Assert.That(elder.IsOpen, Is.True);
        yield return Press(keyboard, Key.Enter);
        Assert.That(elder.IsAwaitingChoice, Is.True);
        yield return Press(keyboard, Key.Digit1);
        Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.Home));
        MoveNear(uncle.transform);
        yield return Press(keyboard, Key.E);
        Assert.That(uncle.IsOpen, Is.True);
        yield return Press(keyboard, Key.Enter);
        yield return Press(keyboard, Key.Enter);
        yield return Press(keyboard, Key.Enter);
        Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.NewOrder));
        string[] dailyStalkNames = CheckFreshBamboo();
        var seenStalkNames = new HashSet<string>(dailyStalkNames);

        yield return SceneManager.LoadSceneAsync(SceneTransitionState.WorkshopSceneName);
        var weaving = Object.FindFirstObjectByType<WeavingStationTrigger>();
        MoveTo(weaving.transform.position + Vector3.up * .25f);
        yield return new WaitForFixedUpdate();
        yield return Press(keyboard, Key.E);
        Assert.That(weaving.IsMinigameOpen, Is.False);
        var computer = Object.FindFirstObjectByType<WorkshopComputer>();
        MoveNear(computer.transform);
        yield return Press(keyboard, Key.E);
        Assert.That(computer.Screen, Is.Not.Null);
        Assert.That(Find(computer.Screen, "进度数值").GetComponent<TMP_Text>().text, Does.EndWith("0%"));
        yield return Press(keyboard, Key.Escape);
        Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.NewOrder));
        yield return Press(keyboard, Key.E);
        Click(computer.Screen, "订单");
        Assert.That(Find(computer.Screen, "提交订单").activeInHierarchy, Is.False);
        yield return Press(keyboard, Key.Escape);
        Assert.That(order.CanHarvest, Is.True);

        int[] pricesAtTestGrade = { 80, 126, 162 };
        for (int day = 1; day <= WorkshopOrder.LastStoryDay; day++)
        {
            if (day > 1)
            {
                yield return WaitForMorning(order, day);
                dailyStalkNames = CheckFreshBamboo();
                Assert.That(dailyStalkNames.All(seenStalkNames.Add), Is.True, "每天从不同位置的竹竿中采集");
                yield return ReceivePatternOrder(order, keyboard);
            }
            for (int sheet = 1; sheet <= WorkshopOrder.SheetsPerDay; sheet++)
            {
                Assert.That(order.CurrentSheetNumber, Is.EqualTo(sheet));
                Assert.That(order.PatternDraft, Is.Null);
                Assert.That(order.CanHarvest, Is.True);
                yield return ProduceAndDry(order, keyboard, dailyStalkNames);
                if (order.IsPatternOrder)
                    yield return FinishPattern(order, keyboard, mouse, sheet == 1);
                yield return SubmitSheet(keyboard, "+" + pricesAtTestGrade[day - 1]);
                Assert.That(order.DeliveredCount, Is.EqualTo(sheet));
                Assert.That(order.TodayIncome, Is.EqualTo(pricesAtTestGrade[day - 1] * sheet));
                Assert.That(order.Submit(), Is.False);
                Assert.That(order.CanAdvanceDay, Is.False);
                Assert.That(order.Settlements.Count, Is.EqualTo(day - 1));
                var reportComputer = Object.FindFirstObjectByType<WorkshopComputer>();
                if (sheet == 1)
                    Assert.That(Find(reportComputer.Screen, "结算标题").GetComponent<TMP_Text>().text, Is.EqualTo("第 1 张已交付"));
                Click(reportComputer.Screen, "确认");
                yield return Frames(2);
            }
            Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.OrderComplete));
            Assert.That(order.CanExplore, Is.True);
            Click(GameObject.Find("进入下一天"));
            if (day == WorkshopOrder.LastStoryDay)
            {
                // Let the newly opened panel join the raycast canvas before clicking it.
                yield return Frames(2);
                var results = GameObject.Find("每日结算").GetComponentsInChildren<Text>()
                    .Single(label => label.name == "明细");
                Assert.That(results.text, Does.Contain("三日名次"));
                Click(GameObject.Find("收工"));
                yield return Frames(2);
            }
        }
        Assert.That((order.Funds, order.XiaoPiFunds, order.XiaoBaiFunds), Is.EqualTo((736, 884, 762)));
        Assert.That(order.Days, Is.EqualTo(3));
        Assert.That(order.Settlements.Count, Is.EqualTo(3));
        Assert.That(order.Settlements.All(record => record.DeliveredCount == 2), Is.True);
        Assert.That(Object.FindFirstObjectByType<PlayerController>().enabled, Is.True);
        InputSystem.RemoveDevice(mouse);
        InputSystem.RemoveDevice(keyboard);
    }

    [UnityTest]
    public IEnumerator VillageMarketAndSpottedBambooWorkAcrossDays()
    {
        EditorSceneManager.OpenScene(SceneTransitionState.WorldScenePath);
        yield return new EnterPlayMode();
        previousBackground = InputSystem.settings.backgroundBehavior;
        previousEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        restoreInput = true;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var mouse = InputSystem.AddDevice<Mouse>();
        var order = WorkshopOrder.Current;
        Assert.That(order, Is.Not.Null);
        Object.FindFirstObjectByType<OpeningSequence>().CompleteImmediately();
        order.CloseIntroduction(WorkshopOrder.Phase.Village);
        order.CloseIntroduction(WorkshopOrder.Phase.Home);
        order.SetClockSpeed(WorkshopOrder.ClockSpeed.Paused);
        int initialMinutes = order.ElapsedGameMinutes;
        yield return Frames(2);

        var teaHint = GameObject.Find("茶摊支线提示").GetComponent<Text>();
        var task = GameObject.Find("任务提示").GetComponent<RectTransform>();
        Assert.That(teaHint.enabled, Is.True);
        Assert.That(teaHint.text, Does.Contain("村口过桥右转").And.Contain("王老板"));
        Assert.That(teaHint.rectTransform.anchoredPosition.y,
            Is.LessThan(task.anchoredPosition.y - task.sizeDelta.y), "支线提示显示在主线提示下方");

        var stalks = Object.FindObjectsByType<ChopableBamboo>(FindObjectsSortMode.None)
            .Where(stalk => stalk.CanChop).OrderBy(stalk => (stalk.transform.position - BambooHarvest.PathAnchor).sqrMagnitude)
            .Take(2).ToArray();
        Assert.That(stalks.Length, Is.EqualTo(2));
        foreach (var stalk in stalks)
            yield return HarvestStalk(stalk, keyboard);
        Assert.That((GreenBambooBag.Count, order.ElapsedGameMinutes - initialMinutes), Is.EqualTo((2, 20)));
        Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.NewOrder));

        yield return TalkToMarketNpc(VillageMarketDialogue.Role.Blacksmith, keyboard);
        Click(GameObject.Find("卖 1 根青竹"));
        Click(GameObject.Find("卖 1 根青竹"));
        Assert.That((GreenBambooBag.Count, order.Funds), Is.EqualTo((0, 30)));
        Assert.That(GameObject.Find("卖 1 根青竹").GetComponent<Button>().interactable, Is.False);
        yield return Press(keyboard, Key.Escape);

        yield return TalkToMarketNpc(VillageMarketDialogue.Role.TeaOwner, keyboard);
        foreach (string word in new[] { "今晚", "留意", "以鉴宝为由", "骗走旧物的人" })
            Click(GameObject.Find(word));
        Click(GameObject.Find("贴告示"));
        Assert.That(order.TeaNoticeCompleted, Is.False);
        Assert.That(order.ElapsedGameMinutes - initialMinutes, Is.EqualTo(20));
        Click(GameObject.Find("清空"));
        foreach (string word in new[] { "请乡亲们", "留意", "以鉴宝为由", "骗走旧物的人" })
            Click(GameObject.Find(word));
        Click(GameObject.Find("贴告示"));
        Assert.That(order.TeaNoticeCompleted, Is.True);
        Assert.That(order.HasCollectorTip, Is.True);
        Assert.That((order.Funds, order.ElapsedGameMinutes - initialMinutes), Is.EqualTo((60, 50)));
        yield return Press(keyboard, Key.Escape);
        Assert.That(teaHint.enabled, Is.False, "完成茶摊支线后清除指路文字");

        var trader = MarketNpc(VillageMarketDialogue.Role.NightTrader);
        Assert.That(trader.IsDialogueAvailable, Is.False);
        Assert.That(trader.transform.Find("人物模型").gameObject.activeSelf, Is.False);
        order.AdvanceTime(17 * 60 + 59 - order.Hour * 60 - order.Minute);
        yield return Frames(2);
        Assert.That(GameObject.Find("夜间小贩提示"), Is.Null);
        order.AdvanceTime(1);
        yield return Frames(2);
        var arrivalHint = GameObject.Find("夜间小贩提示").GetComponent<Image>();
        Assert.That(arrivalHint.GetComponentInChildren<Text>().text, Does.Contain("18:00").And.Contain("村口牌楼旁"));
        Color arrivalColor = arrivalHint.color;
        yield return TalkToMarketNpc(VillageMarketDialogue.Role.NightTrader, keyboard);
        Assert.That(arrivalHint.gameObject.activeSelf, Is.False, "提示在交易中隐藏");
        Click(GameObject.Find("买特效增白剂  ¥20"));
        Assert.That((order.Brightener, order.Funds), Is.EqualTo((1, 40)));
        yield return Press(keyboard, Key.Escape);

        yield return TalkToMarketNpc(VillageMarketDialogue.Role.LiAunt, keyboard);
        Assert.That(MarketNpc(VillageMarketDialogue.Role.LiAunt).transform.parent.parent.name, Is.EqualTo("StreetHouse_CEntranceMarker"));
        yield return Press(keyboard, Key.Escape);

        var start = GameObject.Find("斑竹地入口").transform;
        MoveTo(start.position + Vector3.up * .12f);
        yield return Press(keyboard, Key.E);
        Assert.That(VillageSpottedGrove.IsSneaking, Is.True);
        var dog = GameObject.Find("黄狗").transform;
        MoveTo(dog.position + dog.forward * 1.5f + Vector3.up * .12f);
        yield return Frames(2);
        Assert.That(VillageSpottedGrove.IsSneaking, Is.False);
        Assert.That(order.Funds, Is.EqualTo(-60));
        Assert.That(order.CanStealSpottedBamboo, Is.False);
        Click(GameObject.Find("回到村里"));
        yield return Frames(2);

        var bambooToCarry = Object.FindObjectsByType<ChopableBamboo>(FindObjectsSortMode.None)
            .Where(stalk => stalk.CanChop).OrderBy(stalk => (stalk.transform.position - BambooHarvest.PathAnchor).sqrMagnitude)
            .First();
        yield return HarvestStalk(bambooToCarry, keyboard);

        yield return LoadSceneAndWait(SceneTransitionState.WorkshopSceneName);
        yield return Frames(2);
        var eveningHint = GameObject.Find("夜间小贩提示").GetComponent<Image>();
        Assert.That(eveningHint.color, Is.Not.EqualTo(arrivalColor), "同一天切场景只保留指路文字");
        yield return TalkToMarketNpc(VillageMarketDialogue.Role.XiaoPiBucket, keyboard);
        Click(GameObject.Find("偷偷倒入 1 瓶"));
        Assert.That(order.XiaoPiSabotaged, Is.True);
        Assert.That(order.Brightener, Is.Zero);
        Assert.That(order.XiaoPiFunds, Is.Zero, "扣款在收工结算时生效");
        yield return Press(keyboard, Key.Escape);
        Click(GameObject.Find("进入下一天"));
        yield return WaitForMorning(order, 2);
        Assert.That((order.Funds, order.XiaoPiFunds, order.XiaoBaiFunds), Is.EqualTo((-60, 90, 150)));
        Assert.That(GreenBambooBag.Count, Is.EqualTo(1), "竹林刷新时保留背包中的青竹");
        Assert.That(GameObject.Find("夜间小贩提示"), Is.Null);
        Assert.That(order.PreviousSettlement.Value.XiaoPiPenalized, Is.True);
        Assert.That(order.PreviousSettlement.Value.DeliveredCount, Is.Zero);
        Assert.That(MarketNpc(VillageMarketDialogue.Role.Collector).IsDialogueAvailable, Is.True);
        Assert.That(MarketNpc(VillageMarketDialogue.Role.NightTrader).IsDialogueAvailable, Is.False);

        start = GameObject.Find("斑竹地入口").transform;
        MoveTo(start.position + Vector3.up * .12f);
        yield return Press(keyboard, Key.E);
        var grove = Object.FindFirstObjectByType<VillageSpottedGrove>();
        Assert.That(VillageSpottedGrove.IsSneaking, Is.True);
        dog = GameObject.Find("黄狗").transform;
        Vector3 hidden = GameObject.Find("紫斑竹").transform.position;
        float patrolDeadline = Time.unscaledTime + 12f;
        while (VillageSpottedGrove.SeesPlayer(dog.position, dog.forward, hidden))
        {
            Assert.That(Time.unscaledTime, Is.LessThan(patrolDeadline));
            yield return null;
        }
        MoveTo(hidden + Vector3.up * .12f);
        yield return Press(keyboard, Key.Space);
        Assert.That(grove.IsTiming, Is.True);
        for (int i = 0; i < VillageSpottedGrove.QtePresses; i++)
            yield return Press(keyboard, Key.Space);
        Assert.That(order.SpottedBamboo, Is.EqualTo(2));
        Assert.That(order.CanStealSpottedBamboo, Is.False);
        Assert.That(VillageSpottedGrove.IsSneaking, Is.False);
        Click(GameObject.Find("回到村里"));

        yield return TalkToMarketNpc(VillageMarketDialogue.Role.GoodsMerchant, keyboard);
        Click(GameObject.Find("卖 1 根斑竹  +¥60"));
        Assert.That((order.SpottedBamboo, order.Funds), Is.EqualTo((1, 0)));
        yield return Press(keyboard, Key.Escape);
        yield return ReceivePatternOrder(order, keyboard);

        var workbench = Object.FindFirstObjectByType<BambooWorkbench>();
        MoveTo(new Vector3(.4f, .1f, 4.6f));
        yield return Press(keyboard, Key.E);
        Assert.That(workbench.IsOpen, Is.True);
        yield return Press(keyboard, Key.V);
        Assert.That(order.SpottedBamboo, Is.Zero);
        CompletePreparation(workbench.Progress, placeBamboo: false);
        yield return Frames(2);
        CompleteFinishing(workbench.Progress);
        yield return Frames(2);
        Assert.That(WorkshopInventory.SpottedThreads, Is.EqualTo(100));
        yield return Press(keyboard, Key.Escape);
        Assert.That(order.Begin(WorkshopOrder.Stage.Weaving), Is.True);
        order.ConfirmWeaving(new WeavingMinigameController.WeavingResult
            { grade = WeavingMinigameController.WeavingGrade.S, gameMinutes = 60 });
        Assert.That(order.UsedSpottedBamboo, Is.True);
        Assert.That(WorkshopInventory.SpottedThreads, Is.Zero);
        Assert.That(order.Begin(WorkshopOrder.Stage.Lacquer), Is.True);
        order.ConfirmLacquer(new LacquerMinigameController.LacquerResult
            { grade = LacquerMinigameController.LacquerGrade.S, gameMinutes = 20 });
        Assert.That(order.BeginDrying(), Is.True);
        order.AdvanceTime(order.RemainingDryingMinutes);
        yield return FinishPattern(order, keyboard, mouse, verifyPreview: false);
        yield return SubmitSheet(keyboard, "+364");
        Assert.That(order.LastDelivery.Value.UsedSpottedBamboo, Is.True);
        Assert.That(order.XiaoPiSabotaged, Is.False);
        Assert.That(order.CurrentSettlement, Is.Null);
        Click(Object.FindFirstObjectByType<WorkshopComputer>().Screen, "确认");
        Assert.That(Object.FindFirstObjectByType<PlayerController>().enabled, Is.True);
        order.AdvanceTime(18 * 60 - order.Hour * 60 - order.Minute);
        yield return Frames(2);
        Assert.That(GameObject.Find("夜间小贩提示").GetComponent<Image>().color, Is.EqualTo(arrivalColor),
            "第二天傍晚再次高亮提醒");
        InputSystem.RemoveDevice(mouse);
        InputSystem.RemoveDevice(keyboard);
    }

    static VillageMarketDialogue MarketNpc(VillageMarketDialogue.Role role) =>
        Object.FindObjectsByType<VillageMarketDialogue>(FindObjectsSortMode.None).Single(npc => npc.MerchantRole == role);

    static IEnumerator TalkToMarketNpc(VillageMarketDialogue.Role role, Keyboard keyboard)
    {
        var npc = MarketNpc(role);
        Assert.That(npc.IsDialogueAvailable, Is.True, role.ToString());
        MoveNear(npc.transform);
        yield return Frames(2);
        yield return Press(keyboard, Key.E);
        Assert.That(npc.GetComponent<NpcDialogueInteraction>().IsDialogueOpen, Is.True, role.ToString());
        Assert.That(Object.FindFirstObjectByType<PlayerController>().enabled, Is.False);
    }

    static IEnumerator HarvestStalk(ChopableBamboo stalk, Keyboard keyboard)
    {
        var harvest = Object.FindFirstObjectByType<BambooHarvest>();
        MoveTo(stalk.transform.position - Vector3.forward * 1.5f + Vector3.up * .15f);
        yield return Frames(2);
        Assert.That(harvest.CurrentTarget, Is.SameAs(stalk), "砍伐目标 " + stalk.name);
        for (int i = 0; i < stalk.ChopsToFell; i++)
            yield return Press(keyboard, Key.Space);
        double deadline = EditorApplication.timeSinceStartup + 15;
        while (!stalk.CanPickup)
        {
            Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline), "等待青竹掉落");
            yield return null;
        }
        MoveTo(stalk.InteractPoint - Vector3.forward * .8f + Vector3.up * .15f);
        yield return Frames(2);
        Assert.That(harvest.CurrentTarget, Is.SameAs(stalk), "拾取目标 " + stalk.name);
        yield return Press(keyboard, Key.Space);
        Assert.That(stalk.CurrentStage, Is.EqualTo(ChopableBamboo.Stage.Collected), stalk.name);
    }

    static string[] CheckFreshBamboo()
    {
        var stalks = Object.FindObjectsByType<ChopableBamboo>(FindObjectsSortMode.None);
        var standing = stalks.Where(stalk => stalk.CanChop).ToArray();
        Assert.That(standing.Length, Is.EqualTo(BambooHarvest.HarvestCount));
        var marked = stalks.Where(stalk => stalk.transform.Find("HarvestMark") != null);
        Assert.That(marked, Is.EquivalentTo(standing), "新一天只有今天的六根竹子会发光");
        return standing.Select(stalk => stalk.name).ToArray();
    }

    static IEnumerator ProduceAndDry(WorkshopOrder order, Keyboard keyboard, string[] dailyStalkNames)
    {
        yield return LoadSceneAndWait(SceneTransitionState.WorldSceneName);
        var stalks = Object.FindObjectsByType<ChopableBamboo>(FindObjectsSortMode.None)
            .Where(stalk => stalk.isActiveAndEnabled).ToArray();
        Assert.That(stalks.Select(stalk => stalk.name), Is.EquivalentTo(dailyStalkNames), "当天重新进竹林位置不变");
        Assert.That(stalks.Count(bamboo => bamboo.CurrentStage == ChopableBamboo.Stage.Collected),
            Is.EqualTo(order.DeliveredCount), "当天已拾取的青竹不会长回来");
        var stalk = stalks.Where(bamboo => bamboo.CanChop)
            .OrderBy(bamboo => (bamboo.transform.position - BambooHarvest.PathAnchor).sqrMagnitude).First();
        yield return HarvestStalk(stalk, keyboard);
        Assert.That(order.HasBamboo, Is.True);
        Assert.That(GreenBambooBag.Count, Is.EqualTo(1));

        yield return LoadSceneAndWait(SceneTransitionState.WorkshopSceneName);
        var workbench = Object.FindFirstObjectByType<BambooWorkbench>();
        MoveTo(new Vector3(.4f, .1f, 4.6f));
        yield return Press(keyboard, Key.E);
        Assert.That(workbench.IsOpen, Is.True);
        Assert.That(workbench.Progress.Stage, Is.EqualTo(BambooCraftProgress.Step.PlaceBamboo));
        CompletePreparation(workbench.Progress);
        Assert.That(workbench.Progress.Stage, Is.EqualTo(BambooCraftProgress.Step.BatchFinished));
        Assert.That(workbench.Progress.ScrapingRoundsCompleted, Is.EqualTo(2));
        yield return Frames(2);
        CompleteFinishing(workbench.Progress);
        Assert.That(workbench.Progress.Stage, Is.EqualTo(BambooCraftProgress.Step.CraftFinished));
        Assert.That(workbench.Progress.GatesCompleted, Is.EqualTo(2));
        Assert.That(workbench.Progress.DrawingGroupsCompleted, Is.EqualTo(2));
        yield return Frames(2);
        Assert.That(WorkshopInventory.TotalThreads, Is.EqualTo(100));
        yield return Press(keyboard, Key.Escape);
        Assert.That(workbench.IsOpen, Is.False);
        Assert.That(order.CanPlay(WorkshopOrder.Stage.Weaving), Is.True);
        Assert.That(GameFlowModalState.IsInteractionBlocked, Is.False);

        var weaving = Object.FindFirstObjectByType<WeavingStationTrigger>();
        yield return MoveIntoStation(weaving, "按 E 开始编帘");
        yield return Press(keyboard, Key.E);
        Assert.That(weaving.IsMinigameOpen, Is.True);
        ShowResult(weaving.GetComponent<WeavingMinigameController>());
        if (order.Days == 1 && order.DeliveredCount == 0)
        {
            yield return Press(keyboard, Key.Escape);
            Assert.That(WorkshopInventory.TotalThreads, Is.EqualTo(100));
            yield return Press(keyboard, Key.E);
            ShowResult(weaving.GetComponent<WeavingMinigameController>());
        }
        yield return Press(keyboard, Key.Enter);
        Assert.That(order.WeavingResult.Value.grade, Is.EqualTo(WeavingMinigameController.WeavingGrade.D));
        Assert.That(WorkshopInventory.TotalThreads, Is.Zero);
        Assert.That(workbench.Progress.TotalThreads, Is.Zero);

        var lacquer = Object.FindFirstObjectByType<LacquerStationTrigger>();
        yield return MoveIntoStation(lacquer, "按 E 开始刷大漆", .6f);
        yield return Press(keyboard, Key.E);
        Assert.That(lacquer.IsMinigameOpen, Is.True);
        ShowResult(lacquer.GetComponent<LacquerMinigameController>());
        yield return Press(keyboard, Key.Enter);
        Assert.That(order.LacquerResult.Value.grade, Is.EqualTo(LacquerMinigameController.LacquerGrade.D));

        var drying = Object.FindFirstObjectByType<DryingStationTrigger>();
        MoveNear(drying.transform);
        yield return Frames(2);
        yield return Press(keyboard, Key.E);
        Assert.That(order.IsDrying, Is.True);
        Assert.That(order.RemainingDryingMinutes, Is.InRange(119, 120));
        Assert.That(order.CanUsePatternStation, Is.False);
        Assert.That(order.Submit(), Is.False);
        order.SetClockSpeed(WorkshopOrder.ClockSpeed.Quadruple);
        order.Tick(30f, true);
        Assert.That(order.RemainingDryingMinutes, Is.InRange(59, 60));
        order.Tick(30f, true);
        order.SetClockSpeed(WorkshopOrder.ClockSpeed.Normal);
        Assert.That(order.IsDrying, Is.False);
        Assert.That(order.CanUsePatternStation, Is.EqualTo(order.IsPatternOrder));
    }

    static IEnumerator FinishPattern(WorkshopOrder order, Keyboard keyboard, Mouse mouse, bool verifyPreview)
    {
        var station = Object.FindFirstObjectByType<PatternStationTrigger>();
        Assert.That(station, Is.Not.Null);
        MoveNear(station.transform);
        yield return Press(keyboard, Key.E);
        Assert.That(station.IsOpen, Is.True);
        var board = Object.FindFirstObjectByType<CurtainPatternView>().Board;
        if (order.PatternKind == CurtainPatternKind.Turtle)
        {
            if (verifyPreview)
            {
                yield return AssertPatternRendered(board, "pattern-day2-stitching.png");
                Click(GameObject.Find("对样"));
                yield return AssertPatternRendered(board, "pattern-day2-inspection.png");
                Click(GameObject.Find("返回修补"));
                yield return Stitch(mouse, board, 1, 0);
                yield return Stitch(mouse, board, 1, 1);
                yield return Stitch(mouse, board, 1, CurtainPatternDraft.DetourPin);
                Assert.That(order.PatternDraft.CurrentPin, Is.EqualTo(CurtainPatternDraft.DetourPin));
                yield return MouseClick(mouse, Vector2.zero, MouseButton.Right);
                Assert.That(order.PatternDraft.CurrentPin, Is.EqualTo(1));
                yield return Press(keyboard, Key.Escape);
                Assert.That(station.IsOpen, Is.False);
                Assert.That(Object.FindFirstObjectByType<PlayerController>().enabled, Is.True);
                yield return LoadSceneAndWait(SceneTransitionState.WorldSceneName);
                yield return LoadSceneAndWait(SceneTransitionState.WorkshopSceneName);
                station = Object.FindFirstObjectByType<PatternStationTrigger>();
                MoveNear(station.transform);
                yield return Press(keyboard, Key.E);
                Assert.That(station.IsOpen, Is.True);
                Assert.That(order.PatternDraft.CurrentPin, Is.EqualTo(1));
                board = Object.FindFirstObjectByType<CurtainPatternView>().Board;
                for (int pin = 2; pin <= CurtainPatternDraft.MainPins; pin++)
                    yield return Stitch(mouse, board, 1, pin % CurtainPatternDraft.MainPins);
            }
            else
                yield return StitchLoop(mouse, board, 1);
            yield return StitchLoop(mouse, board, 3);
        }
        else
        {
            for (int loop = 0; loop < CurtainPatternDraft.TurtleCount; loop++)
                yield return StitchLoop(mouse, board, loop);
            Click(GameObject.Find("换朱线"));
            Assert.That(order.PatternDraft.UsesVermilion, Is.True);
            for (int pin = 0; pin <= CurtainPatternDraft.MainPins; pin++)
            {
                int index = pin % CurtainPatternDraft.MainPins;
                yield return Stitch(mouse, board, CurtainPatternDraft.Frame, index);
                if (CurtainPatternDraft.IsJoin(index))
                    Click(GameObject.Find("压线"));
            }
        }

        int before = order.ElapsedGameMinutes;
        Click(GameObject.Find("对样"));
        if (verifyPreview && order.PatternKind == CurtainPatternKind.VermilionJade)
            yield return AssertPatternRendered(board, "pattern-day3-inspection.png", colored: true);
        Click(GameObject.Find("对样"));
        Assert.That(order.PatternResult.Value.Grade, Is.EqualTo(WorkshopOrder.QualityGrade.S));
        Assert.That(order.ElapsedGameMinutes - before, Is.EqualTo(order.PatternMinutes));
        Assert.That(order.HasPatternBlank, Is.False);
        Assert.That(station.IsOpen, Is.False);
    }

    static IEnumerator WaitForMorning(WorkshopOrder order, int day)
    {
        double deadline = EditorApplication.timeSinceStartup + 25d;
        while (SceneManager.GetActiveScene().name != SceneTransitionState.WorldSceneName ||
            GameFlowModalState.IsInteractionBlocked || SceneTransitionState.IsLoading)
        {
            Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline), "等待换日转场");
            yield return null;
        }
        Assert.That(order.Days, Is.EqualTo(day));
        Assert.That((order.Hour, order.Minute), Is.EqualTo((6, 0)));
        Assert.That(order.PatternDraft, Is.Null);
    }

    static IEnumerator ReceivePatternOrder(WorkshopOrder order, Keyboard keyboard)
    {
        MoveNear(GameObject.Find("舅舅").transform);
        yield return Press(keyboard, Key.E);
        Assert.That(GameObject.Find("舅舅").GetComponent<TemporaryDialogueEntryPoint>().IsOpen, Is.True);
        for (int i = 0; i < 3; i++)
            yield return Press(keyboard, Key.Enter);
        Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.NewOrder));
        yield return LoadSceneAndWait(SceneTransitionState.WorkshopSceneName);
        var computer = Object.FindFirstObjectByType<WorkshopComputer>();
        Assert.That(computer, Is.Not.Null);
        MoveNear(computer.transform);
        yield return Press(keyboard, Key.E);
        Click(computer.Screen, "订单");
        Assert.That(Find(computer.Screen, "订单样稿").activeInHierarchy, Is.True);
        yield return Press(keyboard, Key.Escape);
        Assert.That(order.CanUsePatternStation, Is.False);
        Assert.That(order.CanHarvest, Is.True);
    }

    static IEnumerator LoadSceneAndWait(string scene)
    {
        var loading = SceneManager.LoadSceneAsync(scene);
        Assert.That(loading, Is.Not.Null);
        double deadline = EditorApplication.timeSinceStartup + 25d;
        while (!loading.isDone || SceneManager.GetActiveScene().name != scene)
        {
            Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline), "等待场景 " + scene);
            yield return null;
        }
    }

    static IEnumerator SubmitSheet(Keyboard keyboard, string expectedIncome)
    {
        var computer = Object.FindFirstObjectByType<WorkshopComputer>();
        MoveNear(computer.transform);
        yield return Frames(2);
        Assert.That(GameFlowModalState.IsInteractionBlocked, Is.False, "等待纹样台释放交互");
        yield return Press(keyboard, Key.E);
        Assert.That(computer.Screen, Is.Not.Null, "E 打开交单电脑");
        Click(computer.Screen, "订单");
        yield return Frames(2);
        Click(computer.Screen, "提交订单");
        yield return Frames(2);
        Assert.That(Find(computer.Screen, "结算明细").GetComponent<TMP_Text>().text, Does.Contain(expectedIncome));
    }

    static IEnumerator AssertPatternRendered(CurtainPatternGraphic board, string fileName, bool colored = false)
    {
        var sample = GameObject.Find("样稿").GetComponent<CurtainPatternGraphic>();
        yield return Frames(2);

        string path = Path.Combine("Logs", "BambooHarvest", fileName);
        DateTime previousWrite = File.GetLastWriteTimeUtc(path);
        ScreenCapture.CaptureScreenshot(path);
        double deadline = EditorApplication.timeSinceStartup + 15d;
        while (File.GetLastWriteTimeUtc(path) == previousWrite)
        {
            Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline), "等待纹样台实机画面");
            yield return null;
        }
        yield return Frames(1);

        var screenshot = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            Assert.That(screenshot.LoadImage(File.ReadAllBytes(path)), Is.True);
            foreach (CurtainPatternGraphic graphic in new[] { board, sample })
            {
                Color32 pin = PixelAt(screenshot, graphic, 0, 0);
                if (!colored)
                {
                    Assert.That(pin.r < 185 && pin.g < 170 && pin.b < 145, Is.True,
                        $"{graphic.name} 应显示深色龟纹结，取样颜色 {pin}");
                    continue;
                }
                Assert.That(pin.g > pin.r + 30 && pin.b > pin.r + 20, Is.True,
                    $"{graphic.name} 应显示青纹，取样颜色 {pin}");
                Color32 edge = PixelAt(screenshot, graphic, CurtainPatternDraft.Frame, CurtainPatternDraft.RightJoin);
                Assert.That(edge.r > 120 && edge.r > edge.g + 35, Is.True,
                    $"{graphic.name} 应显示朱边，取样颜色 {edge}");
            }
        }
        finally
        {
            Object.DestroyImmediate(screenshot);
        }
    }

    static Color32 PixelAt(Texture2D screenshot, CurtainPatternGraphic graphic, int loop, int pin)
    {
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null,
            graphic.rectTransform.TransformPoint(graphic.PinLocalPosition(loop, pin)));
        int x = Mathf.Clamp(Mathf.FloorToInt(screen.x * screenshot.width / Screen.width), 0, screenshot.width - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(screen.y * screenshot.height / Screen.height), 0, screenshot.height - 1);
        return screenshot.GetPixel(x, y);
    }

    static IEnumerator StitchLoop(Mouse mouse, CurtainPatternGraphic board, int loop)
    {
        for (int pin = 0; pin <= CurtainPatternDraft.MainPins; pin++)
            yield return Stitch(mouse, board, loop, pin % CurtainPatternDraft.MainPins);
    }

    static IEnumerator Stitch(Mouse mouse, CurtainPatternGraphic board, int loop, int pin)
    {
        Canvas.ForceUpdateCanvases();
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null,
            board.rectTransform.TransformPoint(board.PinLocalPosition(loop, pin)));
        yield return MouseClick(mouse, screen, MouseButton.Left);
    }

    static IEnumerator MouseClick(Mouse mouse, Vector2 position, MouseButton button)
    {
        yield return InputFrame(() => InputSystem.QueueStateEvent(mouse,
            new MouseState { position = position }.WithButton(button, true)));
        yield return InputFrame(() => InputSystem.QueueStateEvent(mouse, new MouseState { position = position }));
    }

    static void ShowResult(MonoBehaviour controller)
    {
        Assert.That(controller, Is.Not.Null);
        var finish = controller.GetType().GetMethod("FinishRound", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(finish, Is.Not.Null);
        finish.Invoke(controller, null);
    }

    static void CompletePreparation(BambooCraftProgress craft, bool placeBamboo = true)
    {
        if (placeBamboo)
            Assert.That(craft.Place(), Is.True);
        Assert.That(craft.Stage, Is.EqualTo(BambooCraftProgress.Step.Splitting));
        craft.BeginStroke(new Vector2(0, .5f));
        craft.Drag(new Vector2(1, .5f), 1);
        craft.EndStroke();
        for (int round = 0; round < BambooCraftProgress.ScrapingRounds; round++)
        {
            Assert.That(craft.StartNextStrip(), Is.True);
            for (int row = 1; row < 12; row += 3)
            {
                var from = new Vector2(0, row / 11f);
                craft.BeginStroke(from);
                craft.Drag(new Vector2(1, from.y), 2);
                craft.EndStroke();
            }
        }
    }

    static void CompleteFinishing(BambooCraftProgress craft)
    {
        Assert.That(craft.BeginGates(), Is.True);
        for (int tick = 0; tick < 1600 && craft.IsGateStage; tick++)
            craft.TickFinishing(.05f, Mathf.Abs(craft.GateHeight - craft.PieceY) <= .025f
                ? 0 : Mathf.Sign(craft.GateHeight - craft.PieceY), false);
        for (int group = 0; group < BambooCraftProgress.FinishingGroups; group++)
        {
            Assert.That(craft.BeginDrawingGroup(), Is.True);
            bool pulling = true;
            for (int tick = 0; tick < 1200 && craft.Stage == BambooCraftProgress.Step.Drawing; tick++)
            {
                if (craft.Tension >= .44f) pulling = false;
                if (craft.Tension <= .08f) pulling = true;
                craft.TickFinishing(.05f, 0, pulling);
            }
        }
    }

    static GameObject Find(ComputerUI screen, string name) =>
        screen.GetComponentsInChildren<Transform>(true).Single(item => item.name == name).gameObject;

    static void Click(ComputerUI screen, string name) => Click(Find(screen, name));

    static void Click(GameObject button)
    {
        Assert.That(button, Is.Not.Null);
        string name = button.name;
        Assert.That(button.activeInHierarchy, Is.True, name);
        Assert.That(EventSystem.current, Is.Not.Null);
        Canvas.ForceUpdateCanvases();
        var rect = button.GetComponent<RectTransform>();
        var pointer = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
        };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Assert.That(hits.Count, Is.GreaterThan(0), name);
        string front = string.Join(", ", hits.Take(5).Select(hit =>
            (hit.gameObject.transform.parent ? hit.gameObject.transform.parent.name + "/" : "") + hit.gameObject.name));
        Assert.That(hits[0].gameObject.GetComponentInParent<Button>()?.gameObject, Is.EqualTo(button),
            $"{name} at {pointer.position}, raycasts: {front}");
        ExecuteEvents.Execute(button, pointer, ExecuteEvents.pointerClickHandler);
    }

    static void MoveNear(Transform target) => MoveTo(target.position - Vector3.right * 1.3f + Vector3.up * .15f);

    static IEnumerator MoveIntoStation(MonoBehaviour station, string prompt, float zOffset = 0)
    {
        var collider = station.GetComponent<BoxCollider>();
        var center = collider.bounds.center;
        MoveTo(new Vector3(center.x, collider.bounds.min.y + .18f, center.z + zOffset));
        var nearby = station.GetType().GetField("playerInRange", BindingFlags.Instance | BindingFlags.NonPublic);
        var deadline = EditorApplication.timeSinceStartup + 5;
        while (nearby.GetValue(station) == null || GameFlowModalState.IsInteractionBlocked ||
            SceneTransitionState.IsLocked || InteractionPromptUI.CurrentMessage != prompt)
        {
            var playerPosition = Object.FindFirstObjectByType<PlayerController>().transform.position;
            Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline),
                $"{station.name} trigger at {center}, player at {playerPosition}, modal: {GameFlowModalState.IsInteractionBlocked}, " +
                $"transition locked: {SceneTransitionState.IsLocked}, prompt: {InteractionPromptUI.CurrentMessage}");
            yield return null;
        }
    }

    static void MoveTo(Vector3 position)
    {
        var player = Object.FindFirstObjectByType<PlayerController>();
        var controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.position = position;
        controller.enabled = true;
        Physics.SyncTransforms();
    }

    static IEnumerator Press(Keyboard keyboard, Key key)
    {
        yield return InputFrame(() => InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)));
        yield return InputFrame(() => InputSystem.QueueStateEvent(keyboard, new KeyboardState()));
    }

    static IEnumerator Frames(int count)
    {
        int target = Time.frameCount + count;
        var deadline = EditorApplication.timeSinceStartup + 15;
        while (Time.frameCount < target)
        {
            Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline), "等待游戏帧");
            yield return null;
        }
    }

    static IEnumerator InputFrame(Action queue)
    {
        int frame = -1;
        Action before = () =>
        {
            if (frame >= 0 || InputState.currentUpdateType != InputUpdateType.Dynamic)
                return;
            queue();
            frame = Time.frameCount;
        };
        InputSystem.onBeforeUpdate += before;
        var deadline = EditorApplication.timeSinceStartup + 15;
        try
        {
            while (frame < 0 || Time.frameCount <= frame)
            {
                Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline), "等待模拟按键");
                yield return null;
            }
        }
        finally { InputSystem.onBeforeUpdate -= before; }
    }
}
