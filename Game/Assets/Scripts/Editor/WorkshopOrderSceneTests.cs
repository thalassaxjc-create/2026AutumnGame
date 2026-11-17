using System;
using System.Collections;
using System.Collections.Generic;
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
        var order = WorkshopOrder.Current;
        Assert.That(order, Is.Not.Null);
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
        Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.Home));
        MoveNear(uncle.transform);
        yield return Press(keyboard, Key.E);
        Assert.That(uncle.IsOpen, Is.True);
        yield return Press(keyboard, Key.Enter);
        Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.NewOrder));

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

        yield return SceneManager.LoadSceneAsync(SceneTransitionState.WorldSceneName);
        var stalk = Object.FindObjectsByType<ChopableBamboo>(FindObjectsSortMode.None)
            .OrderBy(bamboo => (bamboo.transform.position - BambooHarvest.PathAnchor).sqrMagnitude).First();
        MoveNear(stalk.transform);
        for (int i = 0; i < stalk.ChopsToFell; i++)
            yield return Press(keyboard, Key.Space);
        double deadline = EditorApplication.timeSinceStartup + 15;
        while (!stalk.CanPickup)
        {
            Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline), "等待青竹掉落");
            yield return null;
        }
        MoveTo(stalk.InteractPoint + Vector3.up * .2f);
        yield return Press(keyboard, Key.Space);
        Assert.That(order.HasBamboo, Is.True);
        Assert.That(GreenBambooBag.Count, Is.EqualTo(1));

        yield return SceneManager.LoadSceneAsync(SceneTransitionState.WorkshopSceneName);
        var workbench = Object.FindFirstObjectByType<BambooWorkbench>();
        MoveTo(new Vector3(.4f, .1f, 4.6f));
        yield return Press(keyboard, Key.E);
        Assert.That(workbench.IsOpen, Is.True);
        CompletePreparation(workbench.Progress);
        Assert.That(workbench.Progress.Stage, Is.EqualTo(BambooCraftProgress.Step.BatchFinished));
        yield return Frames(2);
        Assert.That(order.Progress, Is.EqualTo(.25f));
        CompleteFinishing(workbench.Progress);
        Assert.That(workbench.Progress.Stage, Is.EqualTo(BambooCraftProgress.Step.CraftFinished));
        yield return Frames(2);
        Assert.That(order.Progress, Is.EqualTo(.50f));
        yield return Press(keyboard, Key.Escape);
        Assert.That(workbench.IsOpen, Is.False, "Esc leaves bamboo workbench");
        Assert.That(order.CanPlay(WorkshopOrder.Stage.Weaving), Is.True, "weaving unlocks");
        Assert.That(GameFlowModalState.IsInteractionBlocked, Is.False, "workbench releases modal input");

        weaving = Object.FindFirstObjectByType<WeavingStationTrigger>();
        yield return MoveIntoStation(weaving, "按 E 开始编帘");
        yield return Press(keyboard, Key.E);
        Assert.That(weaving.IsMinigameOpen, Is.True, "E opens weaving");
        // Drive the real result screen with a zero-score fixture. The gameplay score
        // logic is unchanged and the fixture isolates the confirmation/exit contract.
        ShowResult(weaving.GetComponent<WeavingMinigameController>());
        Assert.That(order.Progress, Is.EqualTo(.50f));
        yield return Press(keyboard, Key.Escape);
        Assert.That(order.Progress, Is.EqualTo(.50f));
        yield return Press(keyboard, Key.E);
        ShowResult(weaving.GetComponent<WeavingMinigameController>());
        yield return Press(keyboard, Key.Enter);
        Assert.That(order.WeavingResult.Value.grade, Is.EqualTo(WeavingMinigameController.WeavingGrade.D));
        Assert.That(order.Progress, Is.EqualTo(.75f));

        var lacquer = Object.FindFirstObjectByType<LacquerStationTrigger>();
        Assert.That(order.CanPlay(WorkshopOrder.Stage.Lacquer), Is.True, "lacquer unlocks");
        yield return MoveIntoStation(lacquer, "按 E 开始刷大漆", .6f);
        yield return Press(keyboard, Key.E);
        Assert.That(lacquer.IsMinigameOpen, Is.True);
        ShowResult(lacquer.GetComponent<LacquerMinigameController>());
        Assert.That(order.Progress, Is.EqualTo(.75f));
        yield return Press(keyboard, Key.Enter);
        Assert.That(order.LacquerResult.Value.grade, Is.EqualTo(LacquerMinigameController.LacquerGrade.D));

        computer = Object.FindFirstObjectByType<WorkshopComputer>();
        MoveNear(computer.transform);
        yield return Press(keyboard, Key.E);
        Click(computer.Screen, "订单");
        yield return Frames(2);
        Assert.That(Find(computer.Screen, "进度数值").GetComponent<TMP_Text>().text, Does.EndWith("100%"));
        Assert.That(Find(computer.Screen, "时间").GetComponent<TMP_Text>().text, Is.EqualTo("15:25"));
        Assert.That(order.Funds, Is.EqualTo(102));
        Click(computer.Screen, "提交订单");
        Assert.That(Find(computer.Screen, "结算明细").GetComponent<TMP_Text>().text, Does.Contain("+400"));
        Assert.That(order.Funds, Is.EqualTo(502));
        Assert.That(order.Submit(), Is.False);
        Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.Complete));
        yield return Press(keyboard, Key.Escape);
        Assert.That(Object.FindFirstObjectByType<PlayerController>().enabled, Is.True);
        Assert.That(Object.FindObjectsByType<WorkshopOrderGuide>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        InputSystem.RemoveDevice(keyboard);
    }

    static void ShowResult(MonoBehaviour controller)
    {
        Assert.That(controller, Is.Not.Null);
        var finish = controller.GetType().GetMethod("FinishRound", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(finish, Is.Not.Null);
        finish.Invoke(controller, null);
    }

    static void CompletePreparation(BambooCraftProgress craft)
    {
        Assert.That(craft.Place(), Is.True);
        craft.BeginStroke(new Vector2(0, .5f));
        craft.Drag(new Vector2(1, .5f), 1);
        craft.EndStroke();
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
        }
    }

    static void CompleteFinishing(BambooCraftProgress craft)
    {
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
        }
    }

    static GameObject Find(ComputerUI screen, string name) =>
        screen.GetComponentsInChildren<Transform>(true).Single(item => item.name == name).gameObject;

    static void Click(ComputerUI screen, string name)
    {
        var button = Find(screen, name);
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
        Assert.That(hits[0].gameObject.GetComponentInParent<Button>()?.gameObject, Is.EqualTo(button), name);
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
