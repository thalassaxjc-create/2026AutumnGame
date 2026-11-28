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
using Unity.Profiling;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class WorkshopOrderSceneTests
{
    const string RestorePracticeKey = "WorkshopOrderSceneTests.RestorePractice";
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
        RestorePracticePreference();
    }

    [UnityTest]
    public IEnumerator FirstLacquerTapStaysPlayableAcrossAllSongs()
    {
        const string practiceKey = WorkshopPracticeMode.PreferenceKey;
        bool previous = SessionState.GetBool(practiceKey, false);
        try
        {
            SessionState.SetBool(practiceKey, true);
            EditorSceneManager.OpenScene(SceneTransitionState.WorkshopScenePath);
            yield return new EnterPlayMode();
            yield return PlayFirstLacquerTaps();
        }
        finally
        {
            SessionState.SetBool(practiceKey, previous);
        }
    }

    IEnumerator PlayFirstLacquerTaps()
    {
        previousBackground = InputSystem.settings.backgroundBehavior;
        previousEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        restoreInput = true;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        yield return Frames(2);

        var keyboard = InputSystem.AddDevice<Keyboard>("LacquerTapKeyboard");
        var mouse = InputSystem.AddDevice<Mouse>("LacquerTapMouse");
        var controller = Object.FindFirstObjectByType<LacquerMinigameController>();
        IList schedule = LacquerField<IList>(controller, "schedule");
        RectTransform area = LacquerField<RectTransform>(controller, "playArea");

        foreach (string suffix in new[] { "", " · 第二天", " · 第三天" })
        {
            Assert.That(EditorApplication.ExecuteMenuItem("宣纸帘/工位试玩/刷大漆" + suffix), Is.True);
            if (suffix.Length == 0)
                yield return MouseClick(mouse, RectTransformUtility.WorldToScreenPoint(null, area.position), MouseButton.Left);
            else
                yield return Press(keyboard, Key.Space);

            double deadline = EditorApplication.timeSinceStartup + 10d;
            while (schedule.Count == 0)
            {
                Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline), "等待开场倒数" + suffix);
                yield return null;
            }
            object first = schedule[0];
            Assert.That(LacquerField<object>(first, "type").ToString(), Is.EqualTo("Tap"));
            float beat = LacquerField<float>(first, "targetTime");
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, area.TransformPoint(LacquerField<Vector2>(first, "start")));
            MouseButton button = LacquerField<object>(first, "color").ToString() == "Red" ? MouseButton.Left : MouseButton.Right;
            float earlyClick = beat - (LacquerField<float>(controller, "goodWindow") + LacquerField<float>(controller, "missWindow")) * .5f;
            while (LacquerField<float>(controller, "songTime") < earlyClick - .02f)
            {
                Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline));
                yield return null;
            }
            yield return MouseClick(mouse, point, button);
            Assert.That(LacquerField<int>(controller, "judgedCount"), Is.Zero, "点早时首点应继续保留" + suffix);

            while (LacquerField<float>(controller, "songTime") < beat - .05f)
            {
                Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline));
                yield return null;
            }
            yield return MouseClick(mouse, point, button);
            Assert.That(LacquerField<int>(controller, "judgedCount"), Is.EqualTo(1), "正确点到首拍" + suffix);
            Assert.That(LacquerField<int>(controller, "score"), Is.GreaterThanOrEqualTo(85), "首点得分" + suffix);
            Assert.That(LacquerField<int>(controller, "missCount"), Is.Zero);
            yield return Press(keyboard, Key.Escape);
            yield return Frames(2);
        }
        InputSystem.RemoveDevice(mouse);
        InputSystem.RemoveDevice(keyboard);
    }

    [UnityTest]
    public IEnumerator LacquerStrokesTrackTheGuideAcrossAllSongs()
    {
        const string practiceKey = WorkshopPracticeMode.PreferenceKey;
        bool previous = SessionState.GetBool(practiceKey, false);
        try
        {
            SessionState.SetBool(practiceKey, true);
            EditorSceneManager.OpenScene(SceneTransitionState.WorkshopScenePath);
            yield return new EnterPlayMode();
            yield return PlayLacquerStrokes();
        }
        finally
        {
            SessionState.SetBool(practiceKey, previous);
        }
    }

    IEnumerator PlayLacquerStrokes()
    {
        previousBackground = InputSystem.settings.backgroundBehavior;
        previousEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        restoreInput = true;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        yield return Frames(2);
        var keyboard = InputSystem.AddDevice<Keyboard>("LacquerStrokeKeyboard");
        var mouse = InputSystem.AddDevice<Mouse>("LacquerStrokeMouse");
        var controller = Object.FindFirstObjectByType<LacquerMinigameController>();
        var directions = new HashSet<Vector2>();
        int strokeCount = 0;
        bool captured = false;

        foreach (string suffix in new[] { "", " · 第二天", " · 第三天" })
        {
            Assert.That(EditorApplication.ExecuteMenuItem("宣纸帘/工位试玩/刷大漆" + suffix), Is.True);
            Assert.That(controller.IsOpen, Is.True);
            yield return Press(keyboard, Key.Space);
            IList schedule = LacquerField<IList>(controller, "schedule");
            double roundDeadline = EditorApplication.timeSinceStartup + 8d;
            while (schedule.Count == 0)
            {
                Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(roundDeadline), "等待开场倒数");
                yield return null;
            }
            Assert.That(LacquerField<GameObject>(controller, "introPanel").activeSelf, Is.False);
            var strokes = schedule.Cast<object>().Where(cue => LacquerField<float>(cue, "endTime") > 0f).ToArray();
            foreach (object cue in strokes)
            {
                Vector2 delta = LacquerField<Vector2>(cue, "end") - LacquerField<Vector2>(cue, "start");
                float duration = LacquerField<float>(cue, "endTime") - LacquerField<float>(cue, "targetTime");
                Assert.That(delta.magnitude / duration, Is.EqualTo(128f).Within(.02f));
                directions.Add(delta.normalized);
                strokeCount++;
            }

            object stroke = strokes[0];
            float startBeat = LacquerField<float>(stroke, "targetTime");
            float endBeat = LacquerField<float>(stroke, "endTime");
            Vector2 start = LacquerField<Vector2>(stroke, "start");
            Vector2 end = LacquerField<Vector2>(stroke, "end");
            Vector2 tangent = (end - start).normalized;
            Vector2 offset = new Vector2(-tangent.y, tangent.x) * 24f;
            MouseButton button = LacquerField<object>(stroke, "color").ToString() == "Red" ? MouseButton.Left : MouseButton.Right;
            RectTransform area = LacquerField<RectTransform>(controller, "playArea");
            Vector2 ScreenPoint(Vector2 point) => RectTransformUtility.WorldToScreenPoint(null, area.TransformPoint(point));

            double deadline = EditorApplication.timeSinceStartup + 12d;
            while (LacquerField<float>(controller, "songTime") < startBeat - .07f)
            {
                Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline), "等待实际音乐到起刷拍");
                yield return null;
            }
            int scoreBefore = LacquerField<int>(controller, "score");
            yield return SetMouse(mouse, ScreenPoint(start + offset), held: true, button);
            object active = LacquerField<object>(controller, "activeDrag");
            Assert.That(active, Is.Not.Null, "鼠标按住起刷圈");
            var graphic = LacquerField<LacquerNoteGraphic>(active, "graphic");
            bool waitedAtEnd = false;
            double dspStart = LacquerField<double>(controller, "musicDspStart");
            Action followStroke = () =>
            {
                if (InputState.currentUpdateType != InputUpdateType.Dynamic)
                    return;
                float time = (float)(AudioSettings.dspTime - dspStart);
                float progress = Mathf.InverseLerp(startBeat, endBeat, time);
                Vector2 point = Vector2.Lerp(start, end, progress) + offset;
                if (time >= endBeat - .46f)
                {
                    point = end;
                    waitedAtEnd |= time < endBeat - .02f;
                }
                mouse.MakeCurrent();
                InputSystem.QueueStateEvent(mouse, new MouseState { position = ScreenPoint(point) }.WithButton(button));
            };
            InputSystem.onBeforeUpdate += followStroke;
            try
            {
                while (LacquerField<object>(controller, "activeDrag") != null)
                {
                    Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline), "跟随亮点拖到收刷拍");
                    float time = LacquerField<float>(controller, "songTime");
                    float progress = Mathf.InverseLerp(startBeat, endBeat, time);
                    Assert.That(LacquerField<float>(graphic, "strokeProgress"), Is.EqualTo(progress).Within(.004f));
                    if (!captured && progress >= .42f && progress < .76f)
                    {
                        ScreenCapture.CaptureScreenshot("Logs/BambooHarvest/lacquer-synchronized-stroke.png");
                        captured = true;
                    }
                    yield return null;
                }
            }
            finally
            {
                InputSystem.onBeforeUpdate -= followStroke;
            }
            string feedback = LacquerField<Text>(controller, "feedbackText").text;
            float stoppedAt = LacquerField<float>(controller, "songTime");
            Assert.That(LacquerField<int>(controller, "score") - scoreBefore, Is.GreaterThanOrEqualTo(85),
                $"曲目{suffix}，终点拍 {endBeat:F2}，结束时间 {stoppedAt:F2}，判定 {feedback}");
            Assert.That(waitedAtEnd, Is.True, "提前到终点时仍在等尾拍");
            yield return SetMouse(mouse, ScreenPoint(end), held: false, button);
            yield return Press(keyboard, Key.Escape);
            Assert.That(controller.IsOpen, Is.False);
            yield return Frames(2);
        }
        Assert.That(strokeCount, Is.EqualTo(29));
        Assert.That(directions, Is.EquivalentTo(new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down }));
        InputSystem.RemoveDevice(mouse);
        InputSystem.RemoveDevice(keyboard);
    }

    static T LacquerField<T>(object instance, string name) =>
        (T)instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(instance);

    [UnityTest]
    public IEnumerator PracticeMenuStartsInWorkshopAndReopensEveryWorkstationWithoutStock()
    {
        const string key = WorkshopPracticeMode.PreferenceKey;
        const string menu = "宣纸帘/工位试玩/";
        bool previous = SessionState.GetBool(key, false);
        SessionState.SetString(RestorePracticeKey, previous.ToString());
        try
        {
            SessionState.SetBool(key, true);
            EditorSceneManager.OpenScene(SceneTransitionState.WorldScenePath);
            yield return new EnterPlayMode();
            double deadline = EditorApplication.timeSinceStartup + 20;
            while (SceneManager.GetActiveScene().name != SceneTransitionState.WorkshopSceneName)
            {
                Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline), "等待试玩工坊");
                yield return null;
            }
            yield return Frames(2);
            Assert.That(WorkshopPracticeMode.Enabled, Is.True);
            Assert.That(WorkshopOrder.Current, Is.Null);
            Assert.That(WorkshopInventory.TotalThreads, Is.Zero);

            previousBackground = InputSystem.settings.backgroundBehavior;
            previousEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            restoreInput = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var workbench = Object.FindFirstObjectByType<BambooWorkbench>();
            var mouse = InputSystem.AddDevice<Mouse>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            foreach (var (name, step) in new[]
            {
                ("劈竹", BambooCraftProgress.Step.Splitting),
                ("剐竹青", BambooCraftProgress.Step.Scraping),
                ("过剑门", BambooCraftProgress.Step.GatePassing),
                ("抽竹丝", BambooCraftProgress.Step.Drawing)
            })
            {
                Assert.That(EditorApplication.ExecuteMenuItem(menu + name), Is.True);
                Assert.That(workbench.IsOpen, Is.True);
                Assert.That(workbench.Progress.Stage, Is.EqualTo(step));
                yield return Press(keyboard, Key.Escape);
                Assert.That(workbench.IsOpen, Is.False);
            }
            foreach (var (name, track) in new[]
            {
                ("编帘", ""), ("刷大漆", "encanto"), ("刷大漆 · 第二天", "shoudie"), ("刷大漆 · 第三天", "orchid"),
                ("纹样", ""), ("上漆边", ""), ("上漆边", "")
            })
            {
                Assert.That(EditorApplication.ExecuteMenuItem(menu + name), Is.True);
                Assert.That(GameFlowModalState.IsModalOpen, Is.True, name);
                AudioSource music = null;
                if (track.Length > 0)
                {
                    var lacquer = Object.FindFirstObjectByType<LacquerMinigameController>();
                    music = lacquer.GetComponents<AudioSource>().Single(source => source.clip != null);
                    Assert.That(music.clip.name, Is.EqualTo(track));
                    Assert.That(lacquer.TotalMarks, Is.InRange(34, 43));
                    typeof(LacquerMinigameController).GetMethod("StartRound", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(lacquer, null);
                    double audioDeadline = EditorApplication.timeSinceStartup + 5d;
                    while (music.timeSamples <= 0)
                    {
                        Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(audioDeadline),
                            track + " 背景音乐开始播放");
                        yield return null;
                    }
                }
                if (name == "纹样")
                {
                    yield return Frames(2);
                    yield return TryAllPatterns(keyboard, mouse);
                }
                yield return Press(keyboard, Key.Escape);
                Assert.That(GameFlowModalState.IsModalOpen, Is.False, name);
                Assert.That(player.enabled, Is.True, name);
                if (music != null)
                    Assert.That(music.isPlaying, Is.False, track + " 退出时停止音乐");
            }
            Assert.That((GreenBambooBag.Count, WorkshopInventory.TotalThreads), Is.EqualTo((0, 0)));
            InputSystem.RemoveDevice(mouse);
            InputSystem.RemoveDevice(keyboard);
        }
        finally
        {
            RestorePracticePreference();
        }
    }

    static void RestorePracticePreference()
    {
        if (!bool.TryParse(SessionState.GetString(RestorePracticeKey, ""), out bool previous))
            return;
        SessionState.SetBool(WorkshopPracticeMode.PreferenceKey, previous);
        SessionState.EraseString(RestorePracticeKey);
    }

    [UnityTest]
    public IEnumerator TeaOwnerRespondsAfterStartingInWorkshopAndReturningToVillage()
    {
        EditorSceneManager.OpenScene(SceneTransitionState.WorkshopScenePath);
        yield return new EnterPlayMode();
        Assert.That(WorkshopOrder.Current, Is.Null, "室内工位仍允许独立预览");
        previousBackground = InputSystem.settings.backgroundBehavior;
        previousEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        restoreInput = true;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        InputSystem.AddDevice<Mouse>();

        yield return TalkToMarketNpc(VillageMarketDialogue.Role.XiaoPiBucket, keyboard);
        Assert.That(GameObject.Find("说明").GetComponent<Text>().text, Does.Contain("从村口开始正式游戏"));
        Assert.That(GameObject.Find("偷偷倒入 1 瓶"), Is.Null);
        yield return Press(keyboard, Key.Escape);

        yield return SceneManager.LoadSceneAsync(SceneTransitionState.WorldSceneName);
        var order = WorkshopOrder.Current;
        Assert.That(order, Is.Not.Null, "从工坊走进村庄也必须初始化剧情");
        Object.FindFirstObjectByType<OpeningSequence>().CompleteImmediately();
        order.SetClockSpeed(WorkshopOrder.ClockSpeed.Paused);
        yield return Frames(2);
        Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.Village));
        Assert.That(GameObject.Find("茶摊支线提示").GetComponent<Text>().enabled, Is.True);
        int initialMinutes = order.ElapsedGameMinutes;
        yield return TalkToMarketNpc(VillageMarketDialogue.Role.TeaOwner, keyboard);
        foreach (string word in new[] { "请乡亲们", "留意", "以鉴宝为由", "骗走旧物的人" })
            Click(GameObject.Find(word));
        Click(GameObject.Find("贴告示"));
        Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.Village), "支线不跳过村长主线对话");
        Assert.That((order.Funds, order.ElapsedGameMinutes - initialMinutes), Is.EqualTo((30, 30)));
        Assert.That(order.HasCollectorTip, Is.True);
        yield return Press(keyboard, Key.Escape);
        Assert.That(GameObject.Find("富商情报提示").GetComponent<Text>().enabled, Is.True);

        yield return SceneManager.LoadSceneAsync(SceneTransitionState.WorkshopSceneName);
        yield return SceneManager.LoadSceneAsync(SceneTransitionState.WorldSceneName);
        yield return Frames(2);
        Assert.That(WorkshopOrder.Current, Is.SameAs(order), "再出入工坊不能重置剧情和支线奖励");
        Assert.That(Object.FindObjectsByType<WorkshopOrderGuide>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        yield return TalkToMarketNpc(VillageMarketDialogue.Role.TeaOwner, keyboard);
        Assert.That(GameObject.Find("告示草稿").GetComponent<Text>().text, Does.Contain("已张贴"));
        Assert.That(order.TryFinishTeaNotice(), Is.False);
        Assert.That((order.Funds, order.ElapsedGameMinutes - initialMinutes), Is.EqualTo((30, 30)));
        yield return Press(keyboard, Key.Escape);
    }

    [UnityTest]
    public IEnumerator OfficialStewardOpensLightTableAndResumesTheSamePaperBatch()
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
        SetMorningClock(order);
        Object.FindFirstObjectByType<OpeningSequence>().CompleteImmediately();
        order.CloseIntroduction(WorkshopOrder.Phase.Village);
        order.SetClockSpeed(WorkshopOrder.ClockSpeed.Paused);
        Assert.That(order.TryFinishTeaNotice(), Is.True);
        for (int i = 0; i < 2; i++)
        {
            GreenBambooBag.AddOne();
            Assert.That(order.TrySellGreenBamboo(), Is.True);
        }
        yield return TalkToMarketNpc(VillageMarketDialogue.Role.TeaOwner, keyboard);
        Assert.That(GameObject.Find("打听消息"), Is.Null);
        yield return Press(keyboard, Key.Escape);
        Click(GameObject.Find("进入下一天"));
        yield return WaitForMorning(order, 2);
        yield return TalkToMarketNpc(VillageMarketDialogue.Role.TeaOwner, keyboard);
        Click(GameObject.Find("打听消息"));
        yield return Frames(2);
        Click(GameObject.Find("打听消息  ¥50"));
        Assert.That(order.Funds, Is.EqualTo(10));
        yield return Press(keyboard, Key.Escape);
        order.AdvanceTime(WorkshopOrder.OfficialOpeningHour * 60 - order.Hour * 60 - order.Minute);
        yield return Frames(2);
        yield return TalkToMarketNpc(VillageMarketDialogue.Role.OfficialSteward, keyboard);
        Click(GameObject.Find("开始验纸"));
        yield return Frames(2);

        var board = GameObject.Find("斜光纸样").GetComponent<RectTransform>();
        Rect rect = board.rect;
        Vector2 left = RectTransformUtility.WorldToScreenPoint(null, board.TransformPoint(
            new Vector2(rect.xMin + rect.width * .31f, rect.yMin + rect.height * .5f)));
        Vector2 lit = RectTransformUtility.WorldToScreenPoint(null, board.TransformPoint(
            new Vector2(rect.xMin + rect.width * .392f, rect.yMin + rect.height * .82f)));
        Vector2 shaded = RectTransformUtility.WorldToScreenPoint(null, board.TransformPoint(
            new Vector2(rect.xMin + rect.width * .71f, rect.yMin + rect.height * .82f)));
        yield return SetMouse(mouse, left, held: false);
        yield return CheckInspectionLight(lit, shaded);

        string[] bins = { "合格", "折痕", "薄斑", "针孔" };
        var inspection = order.OfficialInspection;
        Click(GameObject.Find("归档_" + bins[(int)inspection.Current.Defect]));
        yield return Press(keyboard, Key.Escape);
        Assert.That(GameObject.Find("府上验纸界面"), Is.Null);
        Assert.That(Object.FindFirstObjectByType<PlayerController>().enabled, Is.True);
        yield return TalkToMarketNpc(VillageMarketDialogue.Role.OfficialSteward, keyboard);
        Click(GameObject.Find("继续验纸"));
        yield return Frames(2);
        Assert.That(order.OfficialInspection, Is.SameAs(inspection));
        Assert.That(inspection.Inspected, Is.EqualTo(1));
        while (!inspection.Complete)
            Click(GameObject.Find("归档_" + bins[(int)inspection.Current.Defect]));
        Assert.That(order.Funds, Is.EqualTo(10 + WorkshopOrder.OfficialJobReward));
        Assert.That(order.DeliveredCount, Is.Zero);
        Assert.That(GameObject.Find("验纸明细").GetComponent<Text>().text, Does.Contain("分对 6 / 6 张"));
        yield return Press(keyboard, Key.Escape);
        Assert.That(GameFlowModalState.IsInteractionBlocked, Is.False);
        InputSystem.RemoveDevice(mouse);
        InputSystem.RemoveDevice(keyboard);
    }

    static IEnumerator CheckInspectionLight(Vector2 lit, Vector2 shaded) =>
        CaptureFrame("official-paper-inspection.png", image =>
            Assert.That(PixelAt(image, lit).r - PixelAt(image, shaded).r, Is.GreaterThan(40), "光带照亮纸面的一部分"));

    [UnityTest]
    public IEnumerator SpottedGroveTimeoutAndRepeatedBriefingsKeepResourcesStable()
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
        Object.FindFirstObjectByType<OpeningSequence>().CompleteImmediately();
        order.SetClockSpeed(WorkshopOrder.ClockSpeed.Paused);
        yield return Frames(2);
        Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.Village));
        Assert.That(order.CanStealSpottedBamboo, Is.True, "不需要先完成村长对话才能接这条支线。");
        yield return EnterSpottedGrove(keyboard);
        yield return Press(keyboard, Key.Escape);
        Assert.That(order.CanStealSpottedBamboo, Is.True, "砍竹前放弃不罚款。");
        var grove = Object.FindFirstObjectByType<VillageSpottedGrove>();
        int canvases = grove.GetComponentsInChildren<Canvas>(true).Length;
        int fonts = Resources.FindObjectsOfTypeAll<Font>().Length;
        Assert.That(GroveClipCount(), Is.EqualTo(3));
        for (int repeat = 0; repeat < 8; repeat++)
        {
            MoveTo(GameObject.Find("斑竹地入口").transform.position + Vector3.up * .12f);
            yield return Press(keyboard, Key.E);
            Assert.That(VillageSpottedGrove.IsSneaking, Is.False);
            Assert.That(Object.FindFirstObjectByType<PlayerController>().enabled, Is.False);
            yield return Press(keyboard, Key.Escape);
        }
        Assert.That(grove.GetComponentsInChildren<Canvas>(true).Length, Is.EqualTo(canvases));
        Assert.That(Resources.FindObjectsOfTypeAll<Font>().Length, Is.EqualTo(fonts));
        Assert.That(GroveClipCount(), Is.EqualTo(3));
        Assert.That((order.Funds, order.SpottedBamboo), Is.EqualTo((0, 0)));

        yield return EnterSpottedGrove(keyboard);
        yield return StartSpottedChop(keyboard);
        ScreenCapture.CaptureScreenshot("Logs/BambooHarvest/spotted-grove-qte.png");
        yield return Frames(2);
        yield return HoldGroveSpace(keyboard, .25f);
        Assert.That(grove.IsTiming, Is.True, "长按只算一次，不能自动完成掩声。");
        Assert.That(GameObject.Find("掩声填充").GetComponent<RectTransform>().anchorMax.x,
            Is.EqualTo(1f / VillageSpottedGrove.QtePresses).Within(.001f));
        float deadline = Time.unscaledTime + VillageSpottedGrove.QteSeconds + 1;
        while (grove.IsTiming)
        {
            Assert.That(Time.unscaledTime, Is.LessThan(deadline), "QTE 必须在五秒后结束。");
            yield return null;
        }
        Assert.That((order.Funds, order.SpottedBamboo), Is.EqualTo((-100, 0)));
        Assert.That(order.CanStealSpottedBamboo, Is.False);
        ScreenCapture.CaptureScreenshot("Logs/BambooHarvest/spotted-grove-caught.png");
        yield return Frames(2);
        Click(GameObject.Find("回到村里"));
        yield return Frames(2);
        Assert.That(Object.FindFirstObjectByType<PlayerController>().enabled, Is.True);
        Assert.That(GameFlowModalState.IsModalOpen, Is.False);
        Assert.That(order.TryResolveSpottedBambooTheft(true), Is.False);
        yield return LoadSceneAndWait(SceneTransitionState.WorkshopSceneName);
        yield return Frames(2);
        Assert.That(GroveClipCount(), Is.Zero, "离开场景必须释放本次创建的三个音效。");
        yield return LoadSceneAndWait(SceneTransitionState.WorldSceneName);
        Assert.That(WorkshopOrder.Current, Is.SameAs(order));
        Assert.That(order.Funds, Is.EqualTo(-100));
        Assert.That(order.CanStealSpottedBamboo, Is.False);
        Assert.That(VillageSpottedGrove.IsSneaking, Is.False);
        InputSystem.RemoveDevice(mouse);
        InputSystem.RemoveDevice(keyboard);
    }

    [UnityTest]
    public IEnumerator LeavingSceneDuringSpottedQteChargesPenaltyOnlyOnce()
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
        Object.FindFirstObjectByType<OpeningSequence>().CompleteImmediately();
        order.SetClockSpeed(WorkshopOrder.ClockSpeed.Paused);
        yield return Frames(2);
        yield return EnterSpottedGrove(keyboard);
        yield return StartSpottedChop(keyboard);
        yield return LoadSceneAndWait(SceneTransitionState.WorkshopSceneName);
        yield return Frames(2);
        Assert.That((order.Funds, order.SpottedBamboo), Is.EqualTo((-100, 0)));
        Assert.That(VillageSpottedGrove.IsSneaking, Is.False);
        Assert.That(GameFlowModalState.IsModalOpen, Is.False);
        Assert.That(Object.FindFirstObjectByType<PlayerController>().enabled, Is.True);
        Assert.That(GroveClipCount(), Is.Zero);
        yield return LoadSceneAndWait(SceneTransitionState.WorldSceneName);
        Assert.That(order.Funds, Is.EqualTo(-100));
        Assert.That(order.TryResolveSpottedBambooTheft(false), Is.False);
        InputSystem.RemoveDevice(mouse);
        InputSystem.RemoveDevice(keyboard);
    }

    static int GroveClipCount() => Resources.FindObjectsOfTypeAll<AudioClip>().Count(
        clip => clip.name is "斑竹硬节咔声" or "竹叶掩声" or "黄狗警报");

    [UnityTest]
    public IEnumerator WorkshopPreviewThenSpottedQuestCanContinueMainStory()
    {
        Directory.CreateDirectory("Logs/BambooHarvest/MainStoryRecovery");
        EditorSceneManager.OpenScene(SceneTransitionState.WorkshopScenePath);
        yield return new EnterPlayMode();
        previousBackground = InputSystem.settings.backgroundBehavior;
        previousEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        restoreInput = true;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var mouse = InputSystem.AddDevice<Mouse>();

        Assert.That(WorkshopOrder.Current, Is.Null);
        var computer = Object.FindFirstObjectByType<WorkshopComputer>();
        MoveNear(computer.transform);
        yield return Press(keyboard, Key.E);
        Assert.That(computer.Screen, Is.Not.Null, "首次从工坊播放时电脑可以独立预览。");
        yield return Press(keyboard, Key.Escape);

        yield return LoadSceneAndWait(SceneTransitionState.WorldSceneName);
        var order = WorkshopOrder.Current;
        Object.FindFirstObjectByType<OpeningSequence>().CompleteImmediately();
        order.SetClockSpeed(WorkshopOrder.ClockSpeed.Paused);
        yield return Frames(2);
        yield return EnterSpottedGrove(keyboard);
        yield return StartSpottedChop(keyboard);
        for (int i = 0; i < VillageSpottedGrove.QtePresses; i++)
            yield return Press(keyboard, Key.Space);
        Assert.That(order.SpottedBamboo, Is.EqualTo(2));
        ScreenCapture.CaptureScreenshot("Logs/BambooHarvest/MainStoryRecovery/quest-return-hint.png");
        yield return Frames(2);
        Click(GameObject.Find("回到村里"));
        yield return Frames(2);
        Assert.That(GameFlowModalState.IsInteractionBlocked, Is.False, "支线结束应释放全部交互锁。");
        Assert.That(VillageSpottedGrove.IsSneaking, Is.False);
        Assert.That(Object.FindFirstObjectByType<PlayerController>().enabled, Is.True);
        Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.Village), "支线不能自动完成开场对话。");
        Debug.Log("主线回归：工坊预览电脑可用；首次进入村庄启动 Village；斑竹成功后仍为 Village，玩家控制已恢复且无残留交互锁。");

        var stalk = Object.FindObjectsByType<ChopableBamboo>(FindObjectsSortMode.None)
            .First(item => item.CanChop);
        MoveTo(stalk.transform.position - Vector3.forward * 1.5f + Vector3.up * .15f);
        yield return Press(keyboard, Key.Space);
        Assert.That(Object.FindFirstObjectByType<BambooHarvest>().CurrentTarget, Is.Null);
        Assert.That(InteractionPromptUI.CurrentMessage, Does.Contain("村口大爷"), "未解锁时不能无声拒绝砍竹。");

        var uncle = GameObject.Find("舅舅").GetComponent<TemporaryDialogueEntryPoint>();
        MoveNear(uncle.transform);
        yield return Press(keyboard, Key.E);
        Assert.That(uncle.IsOpen, Is.True, "先做支线再找舅舅，应给出返回主线的对话提示。");
        ScreenCapture.CaptureScreenshot("Logs/BambooHarvest/MainStoryRecovery/uncle-reminder.png");
        yield return Frames(2);
        yield return Press(keyboard, Key.Enter);
        Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.Village));

        yield return LoadSceneAndWait(SceneTransitionState.WorkshopSceneName);
        computer = Object.FindFirstObjectByType<WorkshopComputer>();
        MoveNear(computer.transform);
        yield return Press(keyboard, Key.E);
        Assert.That(computer.Screen, Is.Null);
        Assert.That(InteractionPromptUI.CurrentMessage, Does.Contain("村口大爷"));
        var bench = Object.FindFirstObjectByType<BambooWorkbench>();
        MoveTo(bench.transform.position + Vector3.up * .15f);
        yield return Press(keyboard, Key.E);
        Assert.That(bench.IsOpen, Is.True, "支线获得的斑竹可直接在有材料的工位加工。");
        yield return Press(keyboard, Key.Escape);
        Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.Village));

        yield return LoadSceneAndWait(SceneTransitionState.WorldSceneName);
        var elder = GameObject.Find("村口大爷").GetComponent<TemporaryDialogueEntryPoint>();
        MoveNear(elder.transform);
        yield return Press(keyboard, Key.E);
        Assert.That(elder.IsOpen, Is.True);
        yield return Press(keyboard, Key.Enter);
        yield return Press(keyboard, Key.Digit1);
        Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.Home));
        uncle = GameObject.Find("舅舅").GetComponent<TemporaryDialogueEntryPoint>();
        MoveNear(uncle.transform);
        yield return Press(keyboard, Key.E);
        Assert.That(uncle.IsOpen, Is.True);
        for (int i = 0; i < 3; i++) yield return Press(keyboard, Key.Enter);
        Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.NewOrder));
        stalk = Object.FindObjectsByType<ChopableBamboo>(FindObjectsSortMode.None).First(item => item.CanChop);
        yield return HarvestStalk(stalk, keyboard);
        Assert.That(GreenBambooBag.Count, Is.EqualTo(1));
        yield return LoadSceneAndWait(SceneTransitionState.WorkshopSceneName);
        computer = Object.FindFirstObjectByType<WorkshopComputer>();
        MoveNear(computer.transform);
        yield return Press(keyboard, Key.E);
        Assert.That(computer.Screen, Is.Not.Null);
        Click(computer.Screen, "订单");
        yield return Press(keyboard, Key.Escape);
        Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.Production));
        bench = Object.FindFirstObjectByType<BambooWorkbench>();
        MoveTo(bench.transform.position + Vector3.up * .15f);
        yield return Press(keyboard, Key.E);
        Assert.That(bench.IsOpen, Is.True);
        yield return Press(keyboard, Key.Space);
        Assert.That(bench.Progress.Stage, Is.EqualTo(BambooCraftProgress.Step.Splitting));
        Assert.That(GreenBambooBag.Count, Is.Zero);
        yield return Press(keyboard, Key.Escape);
        Assert.That(GameFlowModalState.IsInteractionBlocked, Is.False);
        Assert.That(order.SpottedBamboo, Is.EqualTo(2), "恢复主线不能重置支线材料。");
        InputSystem.RemoveDevice(mouse);
        InputSystem.RemoveDevice(keyboard);
    }

    static IEnumerator EnterSpottedGrove(Keyboard keyboard)
    {
        MoveTo(GameObject.Find("斑竹地入口").transform.position + Vector3.up * .12f);
        yield return Press(keyboard, Key.E);
        ScreenCapture.CaptureScreenshot("Logs/BambooHarvest/spotted-grove-briefing.png");
        yield return Frames(2);
        Click(GameObject.Find("开始潜入"));
        yield return Frames(2);
        Assert.That(VillageSpottedGrove.IsSneaking, Is.True);
    }

    static IEnumerator StartSpottedChop(Keyboard keyboard)
    {
        Transform dog = GameObject.Find("黄狗").transform;
        Vector3 clump = GameObject.Find("紫斑竹").transform.position;
        float deadline = Time.unscaledTime + 15;
        while (VillageSpottedGrove.SeesPlayer(dog.position, dog.forward, clump))
        {
            Assert.That(Time.unscaledTime, Is.LessThan(deadline));
            yield return null;
        }
        MoveTo(clump + Vector3.up * .12f);
        yield return Press(keyboard, Key.Space);
        Assert.That(Object.FindFirstObjectByType<VillageSpottedGrove>().IsTiming, Is.True);
    }

    static IEnumerator HoldGroveSpace(Keyboard keyboard, float seconds)
    {
        yield return InputFrame(() => InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)));
        float until = Time.unscaledTime + seconds;
        while (Time.unscaledTime < until) yield return null;
        yield return InputFrame(() => InputSystem.QueueStateEvent(keyboard, new KeyboardState()));
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
        SetMorningClock(order);
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
        var booklet = GameObject.Find("打开手册").GetComponent<RectTransform>();
        Assert.That(booklet.anchorMin, Is.EqualTo(Vector2.zero));
        Assert.That(booklet.anchorMax, Is.EqualTo(Vector2.zero));
        Assert.That(booklet.pivot, Is.EqualTo(Vector2.zero));
        Assert.That(booklet.anchoredPosition, Is.EqualTo(new Vector2(32f, 26f)),
            "手册按钮应在左下角，避免与右上角任务提示重叠");
        var elder = GameObject.Find("村口大爷").GetComponent<TemporaryDialogueEntryPoint>();
        var uncle = GameObject.Find("舅舅").GetComponent<TemporaryDialogueEntryPoint>();
        MoveNear(uncle.transform);
        yield return Press(keyboard, Key.E);
        Assert.That(uncle.IsOpen, Is.True, "提前找舅舅也应得到主线指路回应。");
        yield return Press(keyboard, Key.Enter);
        Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.Village), "指路对话不能跳过大爷。");
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

        int[] pricesAtTestGrade = { 80, 112, 144, 120 };
        for (int day = 1; day <= WorkshopOrder.LastStoryDay; day++)
        {
            if (day > 1)
            {
                yield return WaitForMorning(order, day);
                dailyStalkNames = CheckFreshBamboo();
                Assert.That(dailyStalkNames.All(seenStalkNames.Add), Is.True, "每天从不同位置的竹竿中采集");
                yield return ReceivePatternOrder(order, keyboard);
            }
            for (int sheet = 1; !order.IsTeachingDay && sheet <= order.RequiredSheets; sheet++)
            {
                Assert.That(order.CurrentSheetNumber, Is.EqualTo(sheet));
                Assert.That(order.PatternDraft, Is.Null);
                Assert.That(order.CanHarvest, Is.True);
                if (day == 4 && sheet == 1)
                {
                    yield return RepairThread(keyboard);
                    yield return FinishPattern(order, keyboard, mouse, false);
                }
                else if (day == 4)
                {
                    yield return RepairWarp(keyboard);
                    yield return BindVermilionTrim(order, keyboard, mouse, false);
                }
                else
                    yield return ProduceAndDry(order, keyboard, dailyStalkNames);
                if (order.IsPatternOrder)
                    yield return FinishPattern(order, keyboard, mouse, sheet == 1);
                if (order.RequiresVermilionTrim)
                    yield return BindVermilionTrim(order, keyboard, mouse, sheet == 1);
                int payout = day == 4 ? (sheet == 1 ? 135 : 150) : pricesAtTestGrade[day - 1];
                yield return SubmitSheet(keyboard, "+" + payout);
                Assert.That(order.DeliveredCount, Is.EqualTo(sheet));
                Assert.That(order.TodayIncome, Is.EqualTo(day == 4 ? (sheet == 1 ? 135 : 285) : payout * sheet));
                Assert.That(order.Submit(), Is.False);
                Assert.That(order.CanAdvanceDay, Is.False);
                Assert.That(order.Settlements.Count, Is.EqualTo(day - 1));
                var reportComputer = Object.FindFirstObjectByType<WorkshopComputer>();
                if (sheet == 1 && order.RequiredSheets > 1)
                    Assert.That(Find(reportComputer.Screen, "结算标题").GetComponent<TMP_Text>().text, Is.EqualTo("第 1 张已交付"));
                if (order.IsPatternOrder)
                    Assert.That(Find(reportComputer.Screen, "结算明细").GetComponent<TMP_Text>().text, Does.Contain("纹样 D"));
                Click(reportComputer.Screen, "确认");
                if (day == 1 && sheet == 1)
                    yield return CloseJournal(checkPeople: true);
                else
                {
                    yield return Frames(2);
                    Assert.That(GameObject.Find("关闭手册"), Is.Null, "重复交付静默入册");
                }
            }
            if (order.IsTeachingDay)
            {
                AssertCustomerBlocksWalking("艾琳");
                MoveNear(GameObject.Find("艾琳").transform);
                yield return Press(keyboard, Key.E);
                Click(GameObject.Find("跟我来"));
                Assert.That(order.LessonPrepared, Is.True);
                Assert.That(order.CanUsePatternStation, Is.False, "刮青和编素帘做完才有帘可教");
                yield return LoadSceneAndWait(SceneTransitionState.WorkshopSceneName);
                Assert.That(GameObject.Find("艾琳"), Is.Not.Null, "艾琳要跟着进工坊");
                yield return TeachAilinScrape(keyboard, mouse);
                yield return TeachAilinWeave(keyboard);
                yield return TeachAilinAtLoom(order, keyboard, mouse);
                MoveNear(GameObject.Find("艾琳").transform);
                yield return Press(keyboard, Key.E);
                Assert.That(order.TaughtLesson, Is.Null, "交帘之前不能算教学完毕");
                Assert.That(order.LessonAdviceGiven, Is.True);
                Click(GameObject.Find("给艾琳带走"));
                Assert.That(order.LessonSucceeded, Is.True);
                yield return Press(keyboard, Key.Escape);
                yield return CloseJournal();
                yield return LoadSceneAndWait(SceneTransitionState.WorldSceneName);
            }
            Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.OrderComplete));
            Assert.That(order.CanExplore, Is.True);
            if (day < WorkshopOrder.LastStoryDay)
                Click(GameObject.Find("进入下一天"));
            else
            {
                MoveNear(GameObject.Find("舅舅").transform);
                yield return Press(keyboard, Key.E);
                yield return Press(keyboard, Key.Enter);
                // Let the newly opened panel join the raycast canvas before clicking it.
                yield return Frames(2);
                var results = GameObject.Find("每日结算").GetComponentsInChildren<Text>()
                    .Single(label => label.name == "明细");
                Assert.That(results.text, Does.Contain("五日名次"));
                Click(GameObject.Find("收工"));
                Click(GameObject.Find("另一个选择"));
                Click(GameObject.Find("另一个选择"));
                Assert.That(results.text, Does.Contain("行李箱"));
                Click(GameObject.Find("收工"));
                yield return CloseJournal();
            }
        }
        Assert.That((order.Funds, order.XiaoPiFunds, order.XiaoBaiFunds), Is.EqualTo((1197, 1574, 1362)));
        Assert.That(order.Days, Is.EqualTo(5));
        Assert.That(order.Settlements.Count, Is.EqualTo(5));
        Assert.That(order.Settlements.All(record => record.Delivered), Is.True);
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
        SetMorningClock(order);
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
        Click(GameObject.Find("今晚"));
        Assert.That(GameObject.Find("告示草稿").GetComponent<Text>().text, Does.Contain("今晚"));
        Click(GameObject.Find("撤回"));
        Assert.That(GameObject.Find("今晚").GetComponent<Button>().interactable, Is.True);
        Assert.That(GameObject.Find("贴告示").GetComponent<Button>().interactable, Is.False);
        yield return Press(keyboard, Key.Escape);
        Assert.That((order.Funds, order.ElapsedGameMinutes - initialMinutes), Is.EqualTo((30, 20)),
            "选词后取消不能扣时或领奖");
        yield return TalkToMarketNpc(VillageMarketDialogue.Role.TeaOwner, keyboard);
        foreach (string word in new[] { "今晚", "留意", "以鉴宝为由", "骗走旧物的人" })
            Click(GameObject.Find(word));
        Click(GameObject.Find("贴告示"));
        Assert.That(order.TeaNoticeCompleted, Is.False);
        Assert.That(order.ElapsedGameMinutes - initialMinutes, Is.EqualTo(20));
        Click(GameObject.Find("清空"));
        foreach (string word in new[] { "请乡亲们", "留意", "以鉴宝为由", "骗走旧物的人" })
            Click(GameObject.Find(word));
        ScreenCapture.CaptureScreenshot("Logs/BambooHarvest/tea-notice-draft.png");
        yield return Frames(2);
        Click(GameObject.Find("贴告示"));
        Assert.That(order.TeaNoticeCompleted, Is.True);
        Assert.That(order.HasCollectorTip, Is.True);
        Assert.That((order.Funds, order.ElapsedGameMinutes - initialMinutes), Is.EqualTo((60, 50)));
        ScreenCapture.CaptureScreenshot("Logs/BambooHarvest/tea-notice-complete.png");
        yield return Frames(2);
        yield return Press(keyboard, Key.Escape);
        yield return Frames(2);
        Assert.That(GameObject.Find("关闭手册"), Is.Null);
        Assert.That(teaHint.enabled, Is.False, "完成茶摊支线后清除指路文字");
        var collectorHint = GameObject.Find("富商情报提示").GetComponent<Text>();
        Assert.That(collectorHint.enabled, Is.True);
        Assert.That(collectorHint.text, Does.Contain("明天").And.Contain("集市东头").And.Contain("罗纹"));
        var teaOwner = MarketNpc(VillageMarketDialogue.Role.TeaOwner);
        int teaCanvases = teaOwner.GetComponentsInChildren<Canvas>(true).Length;
        int teaFonts = Resources.FindObjectsOfTypeAll<Font>().Length;
        for (int repeat = 0; repeat < 6; repeat++)
        {
            yield return TalkToMarketNpc(VillageMarketDialogue.Role.TeaOwner, keyboard);
            Assert.That(GameObject.Find("贴告示"), Is.Null, "完成后不再提供领奖按钮");
            Assert.That(GameObject.Find("告示草稿").GetComponent<Text>().text, Does.Contain("已张贴"));
            yield return Press(keyboard, Key.Escape);
        }
        Assert.That((order.Funds, order.ElapsedGameMinutes - initialMinutes), Is.EqualTo((60, 50)));
        Assert.That(teaOwner.GetComponentsInChildren<Canvas>(true).Length, Is.EqualTo(teaCanvases));
        Assert.That(Resources.FindObjectsOfTypeAll<Font>().Length, Is.EqualTo(teaFonts));

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
        Click(GameObject.Find("开始潜入"));
        yield return Frames(2);
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
        Click(GameObject.Find("开始潜入"));
        yield return Frames(2);
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
        Assert.That(order.WovenCurtains, Is.EqualTo(1));
        Assert.That(WorkshopInventory.SpottedThreads, Is.Zero);
        Assert.That(order.Begin(WorkshopOrder.Stage.Lacquer), Is.True);
        order.ConfirmLacquer(new LacquerMinigameController.LacquerResult
            { grade = LacquerMinigameController.LacquerGrade.S, gameMinutes = 20 });
        Assert.That(order.BeginDrying(), Is.True);
        order.AdvanceTime(order.RemainingDryingMinutes);
        Assert.That(order.IsDrying, Is.False);
        Assert.That(order.CanUsePatternStation, Is.True);
        yield return FinishPattern(order, keyboard, mouse, verifyPreview: false);
        yield return SubmitSheet(keyboard, "+280");
        Assert.That(order.LastDelivery.Value.Grade, Is.EqualTo(WorkshopOrder.QualityGrade.B));
        Assert.That(order.LastDelivery.Value.UsedSpottedBamboo, Is.True);
        Assert.That(order.XiaoPiSabotaged, Is.False);
        Assert.That(order.CurrentSettlement, Is.Null);
        Click(Object.FindFirstObjectByType<WorkshopComputer>().Screen, "确认");
        yield return CloseJournal();
        Assert.That(Object.FindFirstObjectByType<PlayerController>().enabled, Is.True);
        order.AdvanceTime(18 * 60 - order.Hour * 60 - order.Minute);
        yield return Frames(2);
        Assert.That(GameObject.Find("夜间小贩提示").GetComponent<Image>().color, Is.EqualTo(arrivalColor),
            "第二天傍晚再次高亮提醒");
        InputSystem.RemoveDevice(mouse);
        InputSystem.RemoveDevice(keyboard);
    }

    [UnityTest]
    public IEnumerator NightMarketScrapSaleBottleAndSabotageSurviveSceneChangesAndSettleOnce()
    {
        Directory.CreateDirectory("Logs/BambooHarvest/BlackMarket");
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
        Object.FindFirstObjectByType<OpeningSequence>().CompleteImmediately();
        order.SetClockSpeed(WorkshopOrder.ClockSpeed.Paused);
        order.CloseIntroduction(WorkshopOrder.Phase.Village);
        order.CloseIntroduction(WorkshopOrder.Phase.Home);
        order.ReadOrder();
        order.CloseComputer();
        yield return Frames(2);

        var stalk = Object.FindObjectsByType<ChopableBamboo>(FindObjectsSortMode.None)
            .Where(item => item.CanChop).OrderBy(item => (item.transform.position - BambooHarvest.PathAnchor).sqrMagnitude).First();
        yield return HarvestStalk(stalk, keyboard);
        yield return LoadSceneAndWait(SceneTransitionState.WorkshopSceneName);
        var bench = Object.FindFirstObjectByType<BambooWorkbench>();
        MoveTo(bench.transform.position + Vector3.up * .15f);
        yield return Press(keyboard, Key.E);
        Assert.That(bench.IsOpen, Is.True);
        CompletePreparation(bench.Progress);
        CompleteFinishing(bench.Progress, clipAllGates: true);
        yield return Frames(2);
        Assert.That((GreenBambooBag.Count, WorkshopInventory.BambooThreads, WorkshopInventory.DefectiveThreads),
            Is.EqualTo((0, 0, 100)), "真实消耗一根青竹，加工出可出售的残次竹丝。");
        yield return Press(keyboard, Key.Escape);
        yield return LoadSceneAndWait(SceneTransitionState.WorldSceneName);
        order.AdvanceTime(17 * 60 + 59 - order.Hour * 60 - order.Minute);
        yield return Frames(2);
        var trader = MarketNpc(VillageMarketDialogue.Role.NightTrader);
        Assert.That(trader.transform.Find("人物模型").gameObject.activeSelf, Is.False);
        MoveNear(trader.transform);
        yield return Press(keyboard, Key.E);
        Assert.That(trader.GetComponent<NpcDialogueInteraction>().IsDialogueOpen, Is.False);
        order.AdvanceTime(1);
        yield return Frames(2);
        Assert.That(trader.transform.Find("人物模型").gameObject.activeSelf, Is.True);
        yield return TalkToMarketNpc(VillageMarketDialogue.Role.NightTrader, keyboard);
        Assert.That(GameObject.Find("买特效增白剂  ¥20").GetComponent<Button>().interactable, Is.False);
        Click(GameObject.Find("卖 25 根残次竹丝  +¥5"));
        Assert.That((WorkshopInventory.DefectiveThreads, order.Funds), Is.EqualTo((75, 5)));
        for (int sale = 0; sale < 3; sale++) Click(GameObject.Find("卖 25 根残次竹丝  +¥5"));
        Assert.That((WorkshopInventory.DefectiveThreads, order.Funds, order.TodayIncome), Is.EqualTo((0, 20, 20)));
        Assert.That(GameObject.Find("卖 25 根残次竹丝  +¥5").GetComponent<Button>().interactable, Is.False);
        Click(GameObject.Find("买特效增白剂  ¥20"));
        Assert.That((order.Brightener, order.Funds, order.TodayIncome), Is.EqualTo((1, 0, 0)));
        Assert.That(GameObject.Find("买特效增白剂  ¥20").GetComponent<Button>().interactable, Is.False);
        ScreenCapture.CaptureScreenshot("Logs/BambooHarvest/BlackMarket/trade.png");
        yield return Frames(2);
        yield return Press(keyboard, Key.Escape);
        int fonts = Resources.FindObjectsOfTypeAll<Font>().Length;
        int canvases = trader.GetComponentsInChildren<Canvas>(true).Length;
        int materials = Resources.FindObjectsOfTypeAll<Material>().Length;
        for (int repeat = 0; repeat < 6; repeat++)
        {
            yield return TalkToMarketNpc(VillageMarketDialogue.Role.NightTrader, keyboard);
            yield return Press(keyboard, Key.Escape);
        }
        Assert.That(Resources.FindObjectsOfTypeAll<Font>().Length, Is.EqualTo(fonts));
        Assert.That(Resources.FindObjectsOfTypeAll<Material>().Length, Is.EqualTo(materials));
        Assert.That(trader.GetComponentsInChildren<Canvas>(true).Length, Is.EqualTo(canvases));

        yield return LoadSceneAndWait(SceneTransitionState.WorkshopSceneName);
        var bucket = MarketNpc(VillageMarketDialogue.Role.XiaoPiBucket);
        var visual = bucket.GetComponent<XiaoPiBucketVisual>();
        Assert.That(visual, Is.Not.Null, "水桶视觉组件必须保存在工坊场景中。");
        Assert.That(visual.IsBleached, Is.False);
        var freshThreads = new BambooCraftProgress(() => .5f, () => true);
        CompletePreparation(freshThreads);
        CompleteFinishing(freshThreads);
        WorkshopInventory.CaptureCraft(freshThreads);
        Assert.That(order.CanPlay(WorkshopOrder.Stage.Weaving), Is.True);
        var weaving = Object.FindFirstObjectByType<WeavingStationTrigger>();
        Bounds weavingArea = weaving.GetComponent<BoxCollider>().bounds;
        Vector3 intersection = Vector3.MoveTowards(weavingArea.ClosestPoint(bucket.transform.position), weavingArea.center, .03f);
        intersection.y = bucket.transform.position.y + .15f;
        MoveTo(intersection);
        yield return new WaitForFixedUpdate();
        yield return Frames(2);
        var bucketInteraction = bucket.GetComponent<NpcDialogueInteraction>();
        Assert.That(bucketInteraction.PlayerIsInRange, Is.True);
        Assert.That(typeof(WeavingStationTrigger).GetField("playerInRange", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(weaving), Is.SameAs(Object.FindFirstObjectByType<PlayerController>()));
        Assert.That(InteractionPromptUI.CurrentMessage, Is.EqualTo(bucketInteraction.PromptText));
        yield return Press(keyboard, Key.E);
        Assert.That(bucketInteraction.IsDialogueOpen, Is.True, "重叠区域按 E 应打开水桶");
        Assert.That(weaving.IsMinigameOpen, Is.False);
        yield return Press(keyboard, Key.Escape);
        Assert.That(order.Brightener, Is.EqualTo(1), "选择离开不消耗瓶子。");
        yield return TalkToMarketNpc(VillageMarketDialogue.Role.XiaoPiBucket, keyboard);
        var water = bucket.transform.Find("水面").GetComponent<Renderer>();
        var sharedWater = water.sharedMaterial;
        int materialsBefore = Resources.FindObjectsOfTypeAll<Material>().Length;
        Click(GameObject.Find("偷偷倒入 1 瓶"));
        yield return Frames(2);
        Assert.That((order.Brightener, order.Funds, order.XiaoPiFunds), Is.EqualTo((0, 0, 0)));
        Assert.That(visual.IsBleached, Is.True);
        Assert.That(order.TrySabotageXiaoPi(), Is.False);
        Assert.That(GameObject.Find("偷偷倒入 1 瓶"), Is.Null);
        Assert.That(water.sharedMaterial, Is.SameAs(sharedWater));
        Assert.That(Resources.FindObjectsOfTypeAll<Material>().Length, Is.EqualTo(materialsBefore));
        AssertBleached(bucket);
        ScreenCapture.CaptureScreenshot("Logs/BambooHarvest/BlackMarket/bucket-result.png");
        yield return Frames(2);
        yield return Press(keyboard, Key.Escape);
        ScreenCapture.CaptureScreenshot("Logs/BambooHarvest/BlackMarket/bleached-curtain.png");
        yield return Frames(2);
        Assert.That(GameFlowModalState.IsInteractionBlocked, Is.False);
        Assert.That(Object.FindFirstObjectByType<PlayerController>().enabled, Is.True);
        yield return LoadSceneAndWait(SceneTransitionState.WorldSceneName);
        yield return LoadSceneAndWait(SceneTransitionState.WorkshopSceneName);
        yield return Frames(2);
        bucket = MarketNpc(VillageMarketDialogue.Role.XiaoPiBucket);
        Assert.That(bucket.GetComponent<XiaoPiBucketVisual>().IsBleached, Is.True);
        AssertBleached(bucket);
        yield return TalkToMarketNpc(VillageMarketDialogue.Role.XiaoPiBucket, keyboard);
        Assert.That(GameObject.Find("偷偷倒入 1 瓶"), Is.Null);
        yield return Press(keyboard, Key.Escape);

        order.AdvanceTime(order.MinutesUntilClose);
        yield return Frames(2);
        Assert.That(order.CurrentSettlement.Value.XiaoPiIncome, Is.EqualTo(90));
        Assert.That(order.XiaoPiFunds, Is.EqualTo(90));
        Assert.That(GameObject.Find("每日结算").GetComponentsInChildren<Text>().Single(t => t.name == "明细").text,
            Does.Contain("帘子发白").And.Contain("100"));
        ScreenCapture.CaptureScreenshot("Logs/BambooHarvest/BlackMarket/settlement.png");
        yield return Frames(2);
        Assert.That(order.CloseDay(), Is.False);
        Assert.That(order.XiaoPiFunds, Is.EqualTo(90));
        Click(GameObject.Find("收工"));
        yield return WaitForMorning(order, 2);
        Assert.That(order.XiaoPiSabotaged, Is.False);
        Assert.That(MarketNpc(VillageMarketDialogue.Role.NightTrader).IsDialogueAvailable, Is.False);
        yield return LoadSceneAndWait(SceneTransitionState.WorkshopSceneName);
        yield return Frames(2);
        bucket = MarketNpc(VillageMarketDialogue.Role.XiaoPiBucket);
        Assert.That(bucket.GetComponent<XiaoPiBucketVisual>().IsBleached, Is.False);
        yield return TalkToMarketNpc(VillageMarketDialogue.Role.XiaoPiBucket, keyboard);
        Assert.That(GameObject.Find("偷偷倒入 1 瓶").GetComponent<Button>().interactable, Is.False);
        yield return Press(keyboard, Key.Escape);
        Assert.That(GameFlowModalState.IsInteractionBlocked, Is.False);
        Assert.That(order.CanHarvest, Is.True);
        InputSystem.RemoveDevice(mouse);
        InputSystem.RemoveDevice(keyboard);
    }

    static void AssertBleached(VillageMarketDialogue bucket)
    {
        var block = new MaterialPropertyBlock();
        foreach (string path in new[] { "水面", "小皮今日帘架/小皮今日帘面" })
        {
            bucket.transform.Find(path).GetComponent<Renderer>().GetPropertyBlock(block);
            Color color = block.GetColor("_BaseColor");
            Assert.That(Mathf.Min(color.r, color.g, color.b), Is.GreaterThan(.85f), path);
        }
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

    static void SetMorningClock(WorkshopOrder order)
    {
        // Run the story from the morning even when the local settings are set up for a midnight test.
        typeof(WorkshopOrder).GetField("minuteOfDay", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(order, WorkshopOrder.FirstDayStartMinute);
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
        Assert.That(order.WovenCurtains, Is.EqualTo(1));
        Assert.That(WorkshopInventory.TotalThreads, Is.Zero);
        Assert.That(workbench.Progress.TotalThreads, Is.Zero);

        var lacquer = Object.FindFirstObjectByType<LacquerStationTrigger>();
        yield return MoveIntoStation(lacquer, "按 E 开始刷大漆", .6f);
        yield return Press(keyboard, Key.E);
        Assert.That(lacquer.IsMinigameOpen, Is.True);
        ShowResult(lacquer.GetComponent<LacquerMinigameController>());
        yield return Press(keyboard, Key.Enter);
        Assert.That(order.LacqueredCurtains, Is.EqualTo(1));

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
        Assert.That(order.CanUsePatternStation, Is.True);
        Assert.That(order.CanUseTrimStation, Is.True);
    }

    static IEnumerator TeachAilinScrape(Keyboard keyboard, Mouse mouse)
    {
        var bench = Object.FindFirstObjectByType<BambooWorkbench>();
        Assert.That(bench, Is.Not.Null);
        MoveTo(bench.transform.position + Vector3.up * .15f);
        double wait = EditorApplication.timeSinceStartup + 5d;
        while (InteractionPromptUI.CurrentMessage != "按 E 教艾琳刮青")
        {
            Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(wait), "按 E 教艾琳刮青");
            yield return null;
        }
        yield return Press(keyboard, Key.E);
        Assert.That(bench.IsOpen, Is.True, "刮青要用篾竹台");
        yield return Frames(2);
        var surface = bench.View.SurfaceRect;
        foreach (float y in new[] { .12f, .38f, .64f, .9f })
            for (float x = 0f; x <= 1.02f; x += .08f)
                yield return AimSurface(keyboard, mouse, surface, Mathf.Clamp01(x), y);
        double deadline = EditorApplication.timeSinceStartup + 30d;
        while (!AilinLessonTalk.IsOpen)
        {
            Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline), "刮完应告诉艾琳哪里要改");
            yield return null;
        }
        Assert.That(GameObject.Find("点评正文").GetComponent<Text>().text, Does.Contain("艾琳"));
        yield return Press(keyboard, Key.Enter);
        Assert.That(WorkshopOrder.Current.CurrentLesson, Is.EqualTo(WorkshopOrder.LessonBeat.Weave));
    }

    static IEnumerator AimSurface(Keyboard keyboard, Mouse mouse, RectTransform surface, float x, float y)
    {
        Canvas.ForceUpdateCanvases();
        Rect rect = surface.rect;
        Vector2 local = new(Mathf.Lerp(rect.xMin, rect.xMax, x), Mathf.Lerp(rect.yMin, rect.yMax, y));
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, surface.TransformPoint(local));
        yield return InputFrame(() =>
        {
            keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }.WithButton(MouseButton.Left, true));
        });
    }

    static IEnumerator TeachAilinWeave(Keyboard keyboard)
    {
        yield return MoveIntoStation(Object.FindFirstObjectByType<WeavingStationTrigger>(), "按 E 教艾琳编素帘");
        yield return Press(keyboard, Key.E);
        Assert.That(Object.FindFirstObjectByType<WeavingMinigameController>().IsOpen, Is.True, "编帘要用原来的编帘台");
        yield return Press(keyboard, Key.Space);
        double deadline = EditorApplication.timeSinceStartup + 50d;
        while (!AilinLessonTalk.IsOpen)
        {
            Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline), "编完应告诉艾琳哪里要改");
            yield return null;
        }
        Assert.That(GameObject.Find("点评正文").GetComponent<Text>().text, Does.Contain("艾琳"));
        yield return Press(keyboard, Key.Enter);
        Assert.That(WorkshopOrder.Current.CurrentLesson, Is.EqualTo(WorkshopOrder.LessonBeat.Pattern));
    }

    static IEnumerator TeachAilinAtLoom(WorkshopOrder order, Keyboard keyboard, Mouse mouse)
    {
        var station = Object.FindFirstObjectByType<PatternStationTrigger>();
        Assert.That(station, Is.Not.Null);
        MoveNear(station.transform);
        yield return Press(keyboard, Key.E);
        Assert.That(station.IsOpen, Is.True);
        Click(GameObject.Find("选样_龟纹"));
        yield return Frames(2);
        Assert.That(order.PatternDraft, Is.Null, "艾琳只要扎花");
        Click(GameObject.Find("选样_扎花"));
        yield return Frames(2);
        var draft = order.PatternDraft;
        Assert.That(draft.Kind, Is.EqualTo(CurtainPatternKind.TiedFlower));
        var board = Object.FindFirstObjectByType<CurtainPatternView>().Board;
        yield return WeaveShuttleRows(draft, board, keyboard, mouse, correct: true, maxRows: 1);
        Assert.That(draft.CompletedRows, Is.EqualTo(1), "第一道是示范");
        double deadline = EditorApplication.timeSinceStartup + 40d;
        while (!draft.CanConfirm)
        {
            Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline), "扶着艾琳的梭应能教完后两道");
            int row = draft.CompletedRows;
            int crossing = Mathf.Min(draft.CheckedCrossings(row), CurtainPatternDraft.CrossingsPerRow - 1);
            bool under = draft.GoesUnder(row, crossing);
            yield return SetShuttleInput(keyboard, mouse, board, row, draft.Shuttle, under);
        }
        Click(GameObject.Find("预览纹样"));
        yield return Frames(2);
        Assert.That(GameObject.Find("计分").GetComponent<Text>().text, Does.Contain("纹样 S"));
        Click(GameObject.Find("预览纹样"));
        Assert.That(order.AilinCurtainWoven, Is.True);
        Assert.That(order.TaughtLesson, Is.Null);
        Assert.That(station.IsOpen, Is.False);
        Assert.That(AilinLessonTalk.IsOpen, Is.True, "扎完直接告诉她哪里要改");
        yield return Press(keyboard, Key.Enter);
        Assert.That(order.LessonAdviceGiven, Is.True);
    }

    static IEnumerator RepairThread(Keyboard keyboard)
    {
        yield return LoadSceneAndWait(SceneTransitionState.WorldSceneName);
        MoveNear(GameObject.Find("老周").transform);
        yield return Press(keyboard, Key.E);
        foreach (string corner in new[] { "左上孔", "右上孔", "右下孔", "左下孔" })
            Click(GameObject.Find(corner));
        Click(GameObject.Find("收线并检查"));
        Assert.That(WorkshopOrder.Current.ThreadRepaired, Is.True);
        Assert.That(WorkshopOrder.Current.PlainCurtains, Is.EqualTo(1));
        yield return Press(keyboard, Key.Escape);
        yield return CloseJournal();
        yield return LoadSceneAndWait(SceneTransitionState.WorkshopSceneName);
    }

    static IEnumerator RepairWarp(Keyboard keyboard)
    {
        yield return LoadSceneAndWait(SceneTransitionState.WorldSceneName);
        MoveNear(GameObject.Find("老周").transform);
        yield return Press(keyboard, Key.E);
        Click(GameObject.Find("左上 · 翘 · 放夹子"));
        Click(GameObject.Find("左下 · 翘 · 放夹子"));
        Click(GameObject.Find("转半圈"));
        Click(GameObject.Find("把松边拉平"));
        Assert.That(WorkshopOrder.Current.WarpRepaired, Is.True);
        Assert.That(WorkshopOrder.Current.PlainCurtains, Is.EqualTo(1));
        yield return Press(keyboard, Key.Escape);
        yield return CloseJournal();
        yield return LoadSceneAndWait(SceneTransitionState.WorkshopSceneName);
    }

    static void AssertCustomerBlocksWalking(string name)
    {
        Transform customer = GameObject.Find(name).transform;
        MoveNear(customer);
        var controller = Object.FindFirstObjectByType<PlayerController>().GetComponent<CharacterController>();
        CollisionFlags collision = controller.Move(Vector3.right * 1.3f);
        Assert.That(collision.HasFlag(CollisionFlags.Sides), Is.True, $"玩家撞到{name}时应被挡住");
        Assert.That(controller.transform.position.x, Is.LessThan(customer.position.x - .3f));
    }

    static IEnumerator FinishPattern(WorkshopOrder order, Keyboard keyboard, Mouse mouse, bool verifyPreview)
    {
        string patternName = order.NextSheetStyle.Pattern switch
        {
            CurtainPatternKind.Turtle => "龟纹",
            CurtainPatternKind.SingleThread => "单丝路",
            CurtainPatternKind.DoubleThread => "双丝路",
            CurtainPatternKind.Ribbed => "罗纹",
            CurtainPatternKind.TiedFlower => "扎花",
            _ => "素帘"
        };
        string patternCard = "选样_" + patternName;
        bool turtle = order.NextSheetStyle.Pattern == CurtainPatternKind.Turtle;
        var station = Object.FindFirstObjectByType<PatternStationTrigger>();
        Assert.That(station, Is.Not.Null);
        MoveNear(station.transform);
        yield return Press(keyboard, Key.E);
        Assert.That(station.IsOpen, Is.True);
        Assert.That(GameObject.Find("纹样选样页"), Is.Not.Null);
        Assert.That(order.PatternDraft, Is.Null, "打开纹样台时仍未选择订单纹样");
        if (verifyPreview && turtle)
        {
            yield return CaptureFrame("pattern-catalog.png");
            using (var frameTime = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread"))
            {
                yield return Frames(12);
                if (frameTime.Valid && frameTime.LastValue > 0)
                    Debug.Log($"Pattern catalog main thread {frameTime.LastValue / 1000000d:F2} ms");
            }
            foreach (string name in new[] { "单丝路", "双丝路", "罗纹", "扎花" })
            {
                Click(GameObject.Find("选样_" + name));
                yield return Frames(2);
                Assert.That(GameObject.Find("当前纹样").GetComponent<Text>().text, Is.EqualTo(name));
                Assert.That(GameObject.Find("加工类型").GetComponent<Text>().text, Does.Contain("阴干素帘"));
                Assert.That(order.PatternDraft, Is.Not.Null);
                Assert.That(order.PlainCurtains, Is.EqualTo(1), "选样不消耗库存");
                Click(GameObject.Find("返回选样"));
                yield return Frames(2);
            }
        }
        Click(GameObject.Find(patternCard));
        yield return Frames(2);
        var board = Object.FindFirstObjectByType<CurtainPatternView>().Board;
        var draft = order.PatternDraft;
        int before = order.ElapsedGameMinutes;
        if (verifyPreview && turtle)
        {
            Assert.That(GameObject.Find("穿线顺序").GetComponent<Text>().text, Does.Contain("道 ←"));
            yield return CaptureFrame("pattern-shuttle.png");
            Vector2 firstCrossing = draft.GuidePosition(0, .24f);
            yield return SetShuttleInput(keyboard, mouse, board, 0, draft.Shuttle, under: true);
            yield return SetShuttleInput(keyboard, mouse, board, 0, firstCrossing, under: true);
            Assert.That(draft.ResultAt(0, 0), Is.EqualTo(CurtainPatternDraft.CrossingResult.Wrong));
            yield return SetShuttleInput(keyboard, mouse, board, 0, firstCrossing, under: true, held: false);
            yield return SetShuttleInput(keyboard, mouse, board, 0, draft.GuidePosition(0, .6f), under: false, held: false);
            Assert.That(draft.Shuttle.x, Is.EqualTo(.24f).Within(.002f));
            yield return MouseClick(mouse, Vector2.zero, MouseButton.Right);
            Assert.That(draft.Shuttle.x, Is.Zero);
            Assert.That(draft.ResultAt(0, 0), Is.EqualTo(CurtainPatternDraft.CrossingResult.Pending));

            yield return SetShuttleInput(keyboard, mouse, board, 0, draft.Shuttle, under: true);
            yield return SetShuttleInput(keyboard, mouse, board, 0, firstCrossing, under: true);
            yield return Press(keyboard, Key.Escape);
            Assert.That(station.IsOpen, Is.False);
            Assert.That(draft.IsGripped, Is.False);
            Assert.That(order.ElapsedGameMinutes, Is.EqualTo(before), "拖梭时世界时钟暂停");
            Assert.That(Object.FindFirstObjectByType<PlayerController>().enabled, Is.True);
            yield return LoadSceneAndWait(SceneTransitionState.WorldSceneName);
            yield return LoadSceneAndWait(SceneTransitionState.WorkshopSceneName);
            station = Object.FindFirstObjectByType<PatternStationTrigger>();
            MoveNear(station.transform);
            yield return Press(keyboard, Key.E);
            Assert.That(station.IsOpen, Is.True);
            Assert.That(GameObject.Find("纹样选样页"), Is.Not.Null);
            Click(GameObject.Find(patternCard));
            yield return Frames(2);
            Assert.That(order.PatternDraft, Is.SameAs(draft));
            Assert.That(draft.Shuttle.x, Is.EqualTo(.24f).Within(.002f));
            board = Object.FindFirstObjectByType<CurtainPatternView>().Board;
            yield return SetShuttleInput(keyboard, mouse, board, 0, draft.Shuttle, under: false, held: false);
        }

        yield return WeaveShuttleRows(draft, board, keyboard, mouse, correct: false);
        Assert.That(draft.ThreadingScore, Is.Zero);
        Assert.That(draft.Grade, Is.EqualTo(WorkshopOrder.QualityGrade.D));
        before = order.ElapsedGameMinutes;
        Click(GameObject.Find("预览纹样"));
        yield return Frames(2);
        Assert.That(GameObject.Find("计分").GetComponent<Text>().text, Does.Contain("纹样 D"));
        if (verifyPreview && turtle)
            yield return CaptureFrame("pattern-turtle-inspection.png", image =>
            {
                AssertFullTurtle(image, board);
                AssertFullTurtle(image, GameObject.Find("样稿").GetComponent<CurtainPatternGraphic>());
                Vector2 fault = ScreenAt(board, board.ShuttleLocalPosition(0, draft.GuidePosition(0,
                    CurtainPatternDraft.CrossingProgress(0))));
                bool hasVermilion = false;
                for (int y = -4; y <= 4 && !hasVermilion; y++)
                    for (int x = -4; x <= 4 && !hasVermilion; x++)
                    {
                        Color32 pixel = PixelAt(image, fault + new Vector2(x, y));
                        hasVermilion = pixel.r > 120 && pixel.r - pixel.g > 50;
                    }
                Assert.That(hasVermilion, Is.True, "成品对样保留错针的朱色线结");
            });
        Click(GameObject.Find("返回修补"));
        Assert.That(GameObject.Find("计分"), Is.Null);
        Click(GameObject.Find("预览纹样"));
        Click(GameObject.Find("预览纹样"));
        Assert.That(order.GetStatus(WorkshopOrder.Stage.Pattern), Is.EqualTo(WorkshopOrder.StageStatus.Complete));
        Assert.That(order.ElapsedGameMinutes - before, Is.EqualTo(order.PatternMinutes));
        Assert.That(order.PlainCurtains, Is.Zero);
        Assert.That(order.FinishedCurtains, Is.EqualTo(1));
        Assert.That(station.IsOpen, Is.False);
    }

    static IEnumerator BindVermilionTrim(WorkshopOrder order, Keyboard keyboard, Mouse mouse, bool verifyResume)
    {
        Assert.That(order.CanUseTrimStation, Is.True);
        Assert.That(order.PatternDraft, Is.Null);
        Assert.That(order.Submit(), Is.False);
        var style = order.NextSheetStyle.Edge;
        string card = "选边_" + order.NextSheetStyle.Name;
        var station = Object.FindFirstObjectByType<VermilionTrimStation>();
        Assert.That(station, Is.Not.Null);
        Assert.That(station.transform.parent.parent.name, Is.EqualTo("LacqueringArea"));
        MoveTo(station.transform.position + new Vector3(0f, .15f, .25f));
        yield return Frames(2);
        Assert.That(InteractionPromptUI.CurrentMessage, Is.EqualTo("按 E 在上漆边台选样"));
        yield return Press(keyboard, Key.E);
        Assert.That(station.IsOpen, Is.True);
        Assert.That(GameObject.Find("包边评分"), Is.Null);
        yield return Frames(2);
        Assert.That(new[] { "朱边青", "浅竹黄", "棕红", "深褐" }.All(name => GameObject.Find("选边_" + name) != null),
            Is.True);
        Click(GameObject.Find(card));
        yield return Frames(2);
        var board = Object.FindFirstObjectByType<VermilionTrimView>().Board;
        var draft = order.TrimDraft;
        Assert.That(draft.Style, Is.EqualTo(style));

        if (verifyResume)
        {
            int halfway = VermilionTrimDraft.SectionsPerSide / 2;
            yield return SetMouse(mouse, ScreenAt(board, board.ClothPoint(draft.Front)), held: true);
            Vector2 point = ScreenAt(board, board.ClothPoint(VermilionTrimDraft.GuidePoint(halfway)));
            yield return SetMouse(mouse, point, held: true);
            Assert.That(draft.LaidSteps, Is.EqualTo(halfway));
            yield return Press(keyboard, Key.Escape);
            Assert.That(station.IsOpen, Is.False);
            Assert.That(Object.FindFirstObjectByType<PlayerController>().enabled, Is.True);
            yield return SetMouse(mouse, point, held: false);
            yield return Press(keyboard, Key.E);
            Assert.That(station.IsOpen, Is.True);
            Click(GameObject.Find(card));
            yield return Frames(2);
            Assert.That(order.TrimDraft, Is.SameAs(draft));
            Assert.That(draft.LaidSteps, Is.EqualTo(halfway));
        }

        yield return SetMouse(mouse, ScreenAt(board, board.ClothPoint(draft.Front)), held: true);
        for (int step = draft.LaidSteps + 1; step <= VermilionTrimDraft.TotalSteps; step++)
        {
            Vector2 point = ScreenAt(board, board.ClothPoint(VermilionTrimDraft.GuidePoint(step)));
            yield return SetMouse(mouse, point, held: true);
            Assert.That(draft.LaidSteps, Is.GreaterThanOrEqualTo(step), $"沿包边路线推进到第 {step} 步");
        }
        yield return SetMouse(mouse, ScreenAt(board, board.ClothPoint(draft.Front)), held: false);
        Assert.That(draft.SealedCorners, Is.EqualTo(VermilionTrimDraft.SideCount));

        if (verifyResume)
            yield return CaptureFrame("vermilion-trim.png", image =>
            {
                Color32 bamboo = PixelAt(image, ScreenAt(board, board.ClothPoint(new Vector2(.45f, .45f))));
                Assert.That(bamboo.g > bamboo.r + 10 && bamboo.g > bamboo.b + 12, Is.True, $"青竹帘面 {bamboo}");
                for (int side = 0; side < VermilionTrimDraft.SideCount; side++)
                {
                    Color32 edge = PixelAt(image, ScreenAt(board, board.ClothPoint(VermilionTrimDraft.EdgePosition(side, .36f))));
                    Assert.That(edge.r > 100 && edge.r > edge.g + 38, Is.True, $"第 {side + 1} 边 {edge}");
                }
            });

        int before = order.ElapsedGameMinutes;
        Click(GameObject.Find("确认包边"));
        Assert.That(order.GetStatus(WorkshopOrder.Stage.VermilionTrim), Is.EqualTo(WorkshopOrder.StageStatus.Complete));
        Assert.That(order.ElapsedGameMinutes - before, Is.EqualTo(order.TrimMinutes));
        Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.ReadyToSubmit));
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
        for (int i = 0; i < (order.IsTeachingDay ? 1 : 3); i++)
            yield return Press(keyboard, Key.Enter);
        if (order.Days == 4)
            yield return Press(keyboard, Key.Digit1);
        if (order.IsTeachingDay)
        {
            Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.Production));
            yield break;
        }
        Assert.That(order.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.NewOrder));
        yield return LoadSceneAndWait(SceneTransitionState.WorkshopSceneName);
        var computer = Object.FindFirstObjectByType<WorkshopComputer>();
        Assert.That(computer, Is.Not.Null);
        MoveNear(computer.transform);
        yield return Press(keyboard, Key.E);
        Click(computer.Screen, "订单");
        bool showsSamples = order.Days is >= 2 and <= 4;
        Assert.That(Find(computer.Screen, "订单样稿").activeInHierarchy, Is.EqualTo(showsSamples));
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

    static IEnumerator CloseJournal(bool checkPeople = false)
    {
        double deadline = EditorApplication.timeSinceStartup + 10d;
        while (GameObject.Find("关闭手册") == null)
        {
            Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline), "等待手册弹出");
            yield return null;
        }
        if (checkPeople)
        {
            Assert.That(GameObject.Find("事件截图").GetComponent<RawImage>().texture, Is.Not.Null);
            Click(GameObject.Find("人物书签"));
            Assert.That(GameObject.Find("条目标题").GetComponent<Text>().text, Is.EqualTo("舅舅"));
            Click(GameObject.Find("事件书签"));
            Assert.That(GameObject.Find("事件截图").GetComponent<RawImage>().texture, Is.Not.Null);
        }
        Click(GameObject.Find("关闭手册"));
        yield return Frames(2);
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
        string details = Find(computer.Screen, "结算明细").GetComponent<TMP_Text>().text;
        Assert.That(details, Does.Contain(expectedIncome).And.Not.Contain("朱边 "));
        if (WorkshopOrder.Current.IsPatternOrder)
            Assert.That(details, Does.Contain("纹样 D"));
        else
            Assert.That(details, Does.Not.Contain("纹样 "));
    }

    static IEnumerator CaptureFrame(string fileName, Action<Texture2D> verify = null)
    {
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
        if (verify == null)
            yield break;

        var screenshot = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            Assert.That(screenshot.LoadImage(File.ReadAllBytes(path)), Is.True);
            verify(screenshot);
        }
        finally
        {
            Object.DestroyImmediate(screenshot);
        }
    }

    static void AssertFullTurtle(Texture2D image, CurtainPatternGraphic graphic)
    {
        for (int row = 0; row < 3; row++)
            for (int column = 0; column < 4; column++)
            {
                Vector2 min = ScreenAt(graphic, graphic.ClothPoint(new Vector2(column * .25f + .06f, row / 3f + .06f)));
                Vector2 max = ScreenAt(graphic, graphic.ClothPoint(new Vector2((column + 1) * .25f - .06f, (row + 1) / 3f - .06f)));
                int seams = 0;
                for (float y = min.y; y <= max.y && seams < 10; y += 2f)
                    for (float x = min.x; x <= max.x && seams < 10; x += 2f)
                    {
                        Color32 pixel = PixelAt(image, new Vector2(x, y));
                        if (pixel.r < 187 && pixel.g < 180 && pixel.b < 151)
                            seams++;
                    }
                Assert.That(seams, Is.EqualTo(10), $"{graphic.name} 第 {row + 1} 行第 {column + 1} 区应有龟纹");
            }
    }

    static Color32 PixelAt(Texture2D screenshot, Vector2 screen)
    {
        int x = Mathf.Clamp(Mathf.FloorToInt(screen.x * screenshot.width / Screen.width), 0, screenshot.width - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(screen.y * screenshot.height / Screen.height), 0, screenshot.height - 1);
        return screenshot.GetPixel(x, y);
    }

    static Vector2 ScreenAt(CurtainPatternGraphic board, Vector2 local) =>
        RectTransformUtility.WorldToScreenPoint(null, board.rectTransform.TransformPoint(local));

    static IEnumerator TryAllPatterns(Keyboard keyboard, Mouse mouse)
    {
        var view = Object.FindFirstObjectByType<CurtainPatternView>();
        var draftField = typeof(CurtainPatternView).GetField("draft", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (string name in new[] { "单丝路", "双丝路", "罗纹", "龟纹", "扎花" })
        {
            Click(GameObject.Find("选样_" + name));
            yield return Frames(2);
            var draft = (CurtainPatternDraft)draftField.GetValue(view);
            Assert.That(GameObject.Find("穿线顺序").GetComponent<Text>().text, Does.Contain("第 3 道"));
            yield return WeaveShuttleRows(draft, view.Board, keyboard, mouse, correct: true);
            Click(GameObject.Find("预览纹样"));
            Assert.That(GameObject.Find("计分").GetComponent<Text>().text, Does.Contain("纹样 S"), name);
            Click(GameObject.Find("预览纹样"));
            yield return Frames(2);
            Assert.That(GameObject.Find("纹样选样页"), Is.Not.Null);
        }
    }

    static IEnumerator WeaveShuttleRows(CurtainPatternDraft draft, CurtainPatternGraphic board, Keyboard keyboard,
        Mouse mouse, bool correct, int maxRows = int.MaxValue)
    {
        int limit = Mathf.Min(CurtainPatternDraft.RowCount, maxRows);
        for (int row = draft.CompletedRows; row < limit; row++)
        {
            yield return SetShuttleInput(keyboard, mouse, board, row, draft.Shuttle, under: false);
            Assert.That(draft.IsGripped, Is.True);
            for (int crossing = draft.CheckedCrossings(row); crossing < CurtainPatternDraft.CrossingsPerRow; crossing++)
            {
                Vector2 point = draft.GuidePosition(row, CurtainPatternDraft.CrossingProgress(crossing) + .008f);
                yield return SetShuttleInput(keyboard, mouse, board, row, point, draft.GoesUnder(row, crossing) == correct);
                Assert.That(draft.CheckedCrossings(row), Is.EqualTo(crossing + 1));
            }
            Vector2 end = draft.GuidePosition(row, 1f);
            yield return SetShuttleInput(keyboard, mouse, board, row, end, under: false);
            Assert.That(draft.CompletedRows, Is.EqualTo(row + 1));
            yield return SetShuttleInput(keyboard, mouse, board, row, end, under: false, held: false);
        }
    }

    static IEnumerator SetShuttleInput(Keyboard keyboard, Mouse mouse, CurtainPatternGraphic board,
        int row, Vector2 travel, bool under, bool held = true)
    {
        Canvas.ForceUpdateCanvases();
        Vector2 screen = ScreenAt(board, board.ShuttleLocalPosition(row, travel));
        return InputFrame(() =>
        {
            keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard, under ? new KeyboardState(Key.Space) : new KeyboardState());
            mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }.WithButton(MouseButton.Left, held));
        });
    }

    static IEnumerator MouseClick(Mouse mouse, Vector2 position, MouseButton button)
    {
        yield return SetMouse(mouse, position, held: true, button);
        yield return SetMouse(mouse, position, held: false, button);
    }

    static IEnumerator SetMouse(Mouse mouse, Vector2 position, bool held, MouseButton button = MouseButton.Left) =>
        InputFrame(() =>
        {
            mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(button, held));
        });

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

    static void CompleteFinishing(BambooCraftProgress craft, bool clipAllGates = false)
    {
        Assert.That(craft.BeginGates(), Is.True);
        for (int tick = 0; tick < 1600 && craft.IsGateStage; tick++)
            craft.TickFinishing(.05f, clipAllGates ? 1 : Mathf.Abs(craft.GateHeight - craft.PieceY) <= .025f
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
