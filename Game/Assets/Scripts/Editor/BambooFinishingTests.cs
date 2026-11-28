using System;
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class BambooFinishingTests
{
    bool restoreSettings, oldBackground;
    int oldFrameRate;
    InputSettings.BackgroundBehavior oldInputBackground;
    InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;

    static BambooCraftProgress Ready()
    {
        var model = new BambooCraftProgress(() => 0.5f, () => true);
        PrepareFine(model);
        return model;
    }

    [Test]
    public void FourProcessedPiecesAreRequiredAndReservedOnlyOnce()
    {
        var empty = new BambooCraftProgress(() => 0.5f, () => false);
        Assert.That(empty.BeginGates(), Is.False);
        Assert.That(empty.BeginDrawingGroup(), Is.False);
        var model = Ready();
        Assert.That(model.FineCount, Is.EqualTo(4));
        Assert.That(model.Advance(), Is.True);
        Assert.That(model.FineCount, Is.Zero);
        Assert.That(model.BeginGates(), Is.False);
        Assert.That(model.Advance(), Is.False);
        Assert.That(model.BeginDrawingGroup(), Is.False);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(4)]
    public void ScrapedDefectsContinueWithoutExtraBambooAndRetainQuality(int defectivePieces)
    {
        int bambooStock = 1;
        int pieceRoll = 0;
        var model = new BambooCraftProgress(() => pieceRoll < BambooCraftProgress.PiecesPerBatch
            ? (pieceRoll++ < defectivePieces ? 0f : .99f) : .5f, () => bambooStock > 0 && bambooStock-- > 0);
        PrepareFine(model, defectivePieces);
        Assert.That(bambooStock, Is.Zero);
        Assert.That(model.Advance(), Is.True, "Space must enter the gates after all four pieces are scraped.");
        Assert.That(model.Stage, Is.EqualTo(BambooCraftProgress.Step.GatePassing));
        Assert.That(model.InheritedDefectivePieces, Is.EqualTo(defectivePieces));
        Assert.That(model.ProcessedPieceCount, Is.Zero);
        Assert.That(model.Advance(), Is.False);
        PassGates(model);
        Assert.That(model.PerfectGates, Is.EqualTo(BambooCraftProgress.FinishingGroups));
        Assert.That(model.FinishingFeedbackSerial, Is.Zero, "Existing scars must not trigger a fresh blade collision.");
        Assert.That(model.RodCount, Is.EqualTo((4 - defectivePieces) * 25));
        Assert.That(model.DefectiveRodCount, Is.EqualTo(defectivePieces * 25));
        FinishDrawing(model);
        Assert.That(model.ThreadCount, Is.EqualTo((4 - defectivePieces) * 25));
        Assert.That(model.DefectiveThreadCount, Is.EqualTo(defectivePieces * 25));
        Assert.That(model.RodCount + model.DefectiveRodCount + model.ProcessedPieceCount, Is.Zero);
        Assert.That(bambooStock, Is.Zero);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(3)]
    public void UpgradedKnifeUsesOneScrapeAndOneGateThenConservesBothDrawingGroups(int defectivePieces)
    {
        int pieceRoll = 0;
        var model = new BambooCraftProgress(() => pieceRoll < BambooCraftProgress.PiecesPerBatch
            ? (pieceRoll++ < defectivePieces ? 0f : .99f) : .5f, () => true);
        typeof(BambooCraftProgress).GetProperty(nameof(BambooCraftProgress.UpgradedKnife)).SetValue(model, true);
        PrepareFine(model, defectivePieces);
        Assert.That(model.ScrapingRoundsCompleted, Is.EqualTo(1));
        Assert.That(model.ScrapingPiecesThisRound, Is.EqualTo(4));
        Assert.That(model.BeginGates(), Is.True);
        Assert.That(model.GatePassCount, Is.EqualTo(1));
        Assert.That(model.RodsPerGate, Is.EqualTo(100));
        PassGates(model);
        Assert.That(model.GatesCompleted, Is.EqualTo(1));
        Assert.That(model.RodCount + model.DefectiveRodCount, Is.EqualTo(100));
        FinishDrawing(model);
        Assert.That(model.DrawingGroupsCompleted, Is.EqualTo(2));
        Assert.That(model.TotalThreads, Is.EqualTo(100));
        Assert.That(model.DefectiveThreadCount, Is.EqualTo(defectivePieces * 25));
    }

    [Test]
    public void TwoForgivingPassesProduce100RodsAndEveryOpeningChanges()
    {
        var model = Ready(); model.BeginGates();
        float previous = model.GateHeight;
        int completed = 0;
        for (int i = 0; i < 1600 && model.IsGateStage; i++)
        {
            float offset = model.GateHeight <= 0.65f ? 0.07f : -0.07f;
            model.TickFinishing(0.05f, Steer(model, model.GateHeight + offset), false);
            if (model.Stage == BambooCraftProgress.Step.GatePassing && model.GatesCompleted != completed)
            {
                Assert.That(Mathf.Abs(model.GateHeight - previous), Is.GreaterThanOrEqualTo(0.15f));
                previous = model.GateHeight; completed = model.GatesCompleted;
            }
        }
        Assert.That(model.GatesCompleted, Is.EqualTo(2));
        Assert.That(model.PerfectGates, Is.EqualTo(2));
        Assert.That(model.RodCount, Is.EqualTo(100));
        Assert.That(model.DefectiveRodCount, Is.Zero);
        Assert.That(model.Stage, Is.EqualTo(BambooCraftProgress.Step.DrawingReady));
        model.TickFinishing(0.25f, 1, true);
        Assert.That(model.RodCount, Is.EqualTo(100));
    }

    [Test]
    public void BladeContactDamagesOnePacketAndCannotAwardItTwice()
    {
        var model = Ready(); model.BeginGates();
        PassGates(model, true);
        Assert.That(model.GatesCompleted, Is.EqualTo(2));
        Assert.That(model.PerfectGates, Is.EqualTo(1));
        Assert.That(model.FinishingFeedbackSerial, Is.EqualTo(1));
        Assert.That(model.RodCount, Is.EqualTo(50));
        Assert.That(model.DefectiveRodCount, Is.EqualTo(50));
        FinishDrawing(model);
        Assert.That(model.ThreadCount, Is.EqualTo(50));
        Assert.That(model.DefectiveThreadCount, Is.EqualTo(50));
        Assert.That(model.RodCount + model.DefectiveRodCount, Is.Zero);
        Assert.That(model.BeginDrawingGroup(), Is.False);
        model.TickFinishing(0.25f, 0, true);
        Assert.That(model.ThreadCount + model.DefectiveThreadCount, Is.EqualTo(100));
    }

    [Test]
    public void TwoSeparateDrawingChallengesConserve100Rods()
    {
        var model = Ready(); model.BeginGates(); PassGates(model);
        for (int group = 1; group <= BambooCraftProgress.FinishingGroups; group++)
        {
            Assert.That(model.BeginDrawingGroup(), Is.True);
            Assert.That(model.BeginDrawingGroup(), Is.False);
            Assert.That(model.RodCount, Is.EqualTo(100 - group * 50));
            PullSafely(model);
            Assert.That(model.DrawingGroupsCompleted, Is.EqualTo(group));
            Assert.That(model.ThreadCount, Is.EqualTo(group * 50));
            Assert.That(model.DrawProgress, Is.EqualTo(1));
            Assert.That(model.DrawErrors, Is.Zero);
        }
        Assert.That(model.Stage, Is.EqualTo(BambooCraftProgress.Step.CraftFinished));
        Assert.That(model.DefectiveThreadCount, Is.Zero);
    }

    [Test]
    public void ContinuousPullLocksUntilReleaseAndTensionRecovery()
    {
        var model = Ready(); model.BeginGates(); PassGates(model); model.BeginDrawingGroup();
        for (int i = 0; i < 500; i++) model.TickFinishing(0.05f, 0, true);
        Assert.That(model.DrawNeedsRelease, Is.True);
        Assert.That(model.DrawProgress, Is.LessThan(1));
        Assert.That(model.DrawErrors, Is.EqualTo(1));
        Assert.That(model.ThreadCount, Is.Zero);
        float progress = model.DrawProgress;
        for (int i = 0; i < 60; i++) model.TickFinishing(0.05f, 0, false);
        Assert.That(model.Tension, Is.LessThan(0.24f));
        Assert.That(model.DrawNeedsRelease, Is.False);
        Assert.That(model.DrawProgress, Is.EqualTo(progress));
        PullSafely(model);
        Assert.That(model.ThreadCount, Is.EqualTo(45));
        Assert.That(model.DefectiveThreadCount, Is.EqualTo(5));
    }

    [Test]
    public void RandomDangerHasWarningAndDoesNotPunishReleasedMouse()
    {
        var model = Ready(); model.BeginGates(); PassGates(model); model.BeginDrawingGroup();
        bool warning = false, danger = false;
        for (int i = 0; i < 180; i++)
        {
            model.TickFinishing(0.05f, 0, false);
            warning |= model.DangerWarning;
            if (model.DangerActive) { Assert.That(warning, Is.True); danger = true; }
        }
        Assert.That(warning && danger, Is.True);
        Assert.That(model.DangerMin, Is.InRange(0.58f, 0.74f));
        Assert.That(model.DrawErrors, Is.Zero);
        Assert.That(model.DrawProgress, Is.Zero);
        Assert.That(model.Tension, Is.Zero);
    }

    [UnityTest]
    public IEnumerator RealWSAndRightMouseCompleteTwoGatesAndTwoGroupsWithStableResources()
    {
        EditorSceneManager.OpenScene(SceneTransitionState.WorkshopScenePath);
        yield return new EnterPlayMode();
        oldBackground = Application.runInBackground;
        oldFrameRate = Application.targetFrameRate;
        oldInputBackground = InputSystem.settings.backgroundBehavior;
        oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        restoreSettings = true;
        Application.runInBackground = true;
        Application.targetFrameRate = 60;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        yield return Frames(3); // Let the first Play Mode input update finish before attaching test devices.
        var keyboard = InputSystem.AddDevice<Keyboard>("FinishingTestKeyboard");
        var mouse = InputSystem.AddDevice<Mouse>("FinishingTestMouse");
        var station = Object.FindFirstObjectByType<BambooWorkbench>();
        MoveToStation();
        yield return Frames(3);
        yield return Press(keyboard, Key.E);
        Assert.That(station.IsOpen, Is.True);
        GreenBambooBag.AddOne();
        PrepareMixedBatchInPlay(station.Progress); // Reproduce the user's 2 good + 2 defective pieces, with no raw bamboo left.
        Assert.That(station.Progress.FineCount, Is.EqualTo(2));
        Assert.That(station.Progress.DefectiveCount, Is.EqualTo(2));
        Assert.That(GreenBambooBag.Count, Is.Zero);
        yield return Press(keyboard, Key.Space);
        Assert.That(station.Progress.Stage, Is.EqualTo(BambooCraftProgress.Step.GatePassing));
        Assert.That(station.Progress.FineCount, Is.Zero);
        Assert.That(station.Progress.DefectiveCount, Is.Zero);
        Assert.That(station.Progress.InheritedDefectivePieces, Is.EqualTo(2));
        int fonts = Resources.FindObjectsOfTypeAll<Font>().Length;
        int materials = Resources.FindObjectsOfTypeAll<Material>().Length;
        int clips = Resources.FindObjectsOfTypeAll<AudioClip>().Length;
        bool capturedDamage = false;
        double deadline = EditorApplication.timeSinceStartup + 100;
        while (station.Progress.IsGateStage)
        {
            Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline));
            var model = station.Progress;
            float target = model.GatesCompleted == 1 ? (model.GateHeight < 0.5f ? 0.9f : 0.1f) : model.GateHeight;
            float direction = Steer(model, target);
            yield return SteerKeyboard(keyboard, direction);
            if (!capturedDamage && model.GateDamaged)
            {
                capturedDamage = true;
                ScreenCapture.CaptureScreenshot("Logs/BambooHarvest/finishing-gate.png");
            }
        }
        yield return SteerKeyboard(keyboard, 0);
        Assert.That(station.Progress.GatesCompleted, Is.EqualTo(2));
        Assert.That(station.Progress.RodCount, Is.EqualTo(50));
        Assert.That(station.Progress.DefectiveRodCount, Is.EqualTo(50));
        Assert.That(capturedDamage, Is.True);
        yield return Press(keyboard, Key.Space);
        bool pulling = true, testedPause = false, sawWarning = false, sawDanger = false;
        deadline = EditorApplication.timeSinceStartup + 180;
        while (station.Progress.Stage != BambooCraftProgress.Step.CraftFinished)
        {
            Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline));
            var model = station.Progress;
            if (model.Stage == BambooCraftProgress.Step.DrawingReady)
            {
                yield return PullMouse(mouse, false);
                yield return Press(keyboard, Key.Space);
                pulling = true;
            }
            if (model.Tension >= 0.44f) pulling = false;
            if (model.Tension <= 0.08f) pulling = true;
            yield return PullMouse(mouse, pulling);
            sawWarning |= model.DangerWarning;
            if (model.DangerActive && !sawDanger)
            {
                sawDanger = true;
                ScreenCapture.CaptureScreenshot("Logs/BambooHarvest/finishing-drawing.png");
            }
            if (!testedPause && model.DrawProgress >= 0.15f)
            {
                testedPause = true;
                yield return PullMouse(mouse, false);
                yield return Press(keyboard, Key.Escape);
                float pausedProgress = model.DrawProgress, pausedTension = model.Tension;
                yield return Frames(15);
                Assert.That(model.DrawProgress, Is.EqualTo(pausedProgress));
                Assert.That(model.Tension, Is.EqualTo(pausedTension));
                yield return SceneManager.LoadSceneAsync(SceneTransitionState.HomeSceneName);
                yield return Frames(3);
                Assert.That(Object.FindObjectsByType<BambooWorkbenchView>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.Zero);
                yield return SceneManager.LoadSceneAsync(SceneTransitionState.WorkshopSceneName);
                MoveToStation();
                yield return Frames(3);
                station = Object.FindFirstObjectByType<BambooWorkbench>();
                yield return Press(keyboard, Key.E);
                Assert.That(station.Progress, Is.SameAs(model));
                Assert.That(model.DrawProgress, Is.EqualTo(pausedProgress));
                // Repeated opens must reuse the UI and all three audio clips.
                fonts = Resources.FindObjectsOfTypeAll<Font>().Length;
                materials = Resources.FindObjectsOfTypeAll<Material>().Length;
                clips = Resources.FindObjectsOfTypeAll<AudioClip>().Length;
                for (int i = 0; i < 6; i++) { yield return Press(keyboard, Key.Escape); yield return Press(keyboard, Key.E); }
                Assert.That(Resources.FindObjectsOfTypeAll<Font>().Length, Is.EqualTo(fonts));
                Assert.That(Resources.FindObjectsOfTypeAll<Material>().Length, Is.EqualTo(materials));
                Assert.That(Resources.FindObjectsOfTypeAll<AudioClip>().Length, Is.EqualTo(clips));
            }
        }
        yield return PullMouse(mouse, false);
        Assert.That(sawWarning && sawDanger && testedPause, Is.True);
        Assert.That(station.Progress.DrawingGroupsCompleted, Is.EqualTo(2));
        Assert.That(station.Progress.ThreadCount, Is.EqualTo(50));
        Assert.That(station.Progress.DefectiveThreadCount, Is.EqualTo(50));
        Assert.That(station.Progress.RodCount + station.Progress.DefectiveRodCount, Is.Zero);
        ScreenCapture.CaptureScreenshot("Logs/BambooHarvest/finishing-complete.png");
        yield return Frames(4);
        yield return Press(keyboard, Key.Escape);
        Assert.That(Object.FindFirstObjectByType<PlayerController>().enabled, Is.True);
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        for (int i = InputSystem.devices.Count - 1; i >= 0; i--)
            if (InputSystem.devices[i].name.StartsWith("FinishingTest")) InputSystem.RemoveDevice(InputSystem.devices[i]);
        if (restoreSettings)
        {
            Application.runInBackground = oldBackground; Application.targetFrameRate = oldFrameRate;
            InputSystem.settings.backgroundBehavior = oldInputBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
            restoreSettings = false;
        }
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    static void PrepareFine(BambooCraftProgress model, int defectivePieces = 0)
    {
        Assert.That(model.Place(), Is.True);
        model.BeginStroke(new Vector2(0, 0.5f)); model.Drag(new Vector2(1, 0.5f), 1); model.EndStroke();
        for (int round = 0; round < model.ScrapingRoundCount; round++)
        {
            Assert.That(model.StartNextStrip(), Is.True);
            if (round * BambooCraftProgress.PiecesPerRound < defectivePieces)
            {
                model.BeginStroke(new Vector2(0, 0.5f));
                model.Drag(new Vector2(1, 0.5f), 0.01f); model.EndStroke();
            }
            for (int row = 1; row < 12; row += 3)
            {
                model.BeginStroke(new Vector2(0, row / 11f));
                model.Drag(new Vector2(1, row / 11f), 2); model.EndStroke();
            }
        }
        Assert.That(model.FineCount, Is.EqualTo(4 - defectivePieces));
        Assert.That(model.DefectiveCount, Is.EqualTo(defectivePieces));
    }
    static void PrepareMixedBatchInPlay(BambooCraftProgress model)
    {
        var oldRandom = UnityEngine.Random.state;
        try
        {
            for (int seed = 0; seed < 1000; seed++)
            {
                UnityEngine.Random.InitState(seed);
                float first = UnityEngine.Random.value, second = UnityEngine.Random.value;
                if (first >= 0.54f || second >= 0.54f) continue;
                UnityEngine.Random.InitState(seed);
                PrepareFine(model, 2);
                return;
            }
            Assert.Fail("Could not prepare a deterministic mixed-quality batch.");
        }
        finally { UnityEngine.Random.state = oldRandom; }
    }
    static float Steer(BambooCraftProgress model, float target) => Mathf.Abs(target - model.PieceY) <= 0.025f ? 0 : Mathf.Sign(target - model.PieceY);
    static void PassGates(BambooCraftProgress model, bool damageFirst = false)
    {
        for (int i = 0; i < 1600 && model.IsGateStage; i++)
        {
            float target = damageFirst && model.GatesCompleted == 0 ? 0.9f : model.GateHeight;
            model.TickFinishing(0.05f, Steer(model, target), false);
        }
        Assert.That(model.Stage, Is.EqualTo(BambooCraftProgress.Step.DrawingReady));
    }
    static void PullSafely(BambooCraftProgress model)
    {
        bool pulling = true;
        for (int i = 0; i < 1200 && model.Stage == BambooCraftProgress.Step.Drawing; i++)
        {
            if (model.Tension >= 0.44f) pulling = false;
            if (model.Tension <= 0.08f) pulling = true;
            model.TickFinishing(0.05f, 0, pulling);
        }
        Assert.That(model.Stage, Is.Not.EqualTo(BambooCraftProgress.Step.Drawing));
    }
    static void FinishDrawing(BambooCraftProgress model)
    {
        for (int group = 0; group < BambooCraftProgress.FinishingGroups; group++) { Assert.That(model.BeginDrawingGroup(), Is.True); PullSafely(model); }
    }
    static void MoveToStation()
    {
        var player = Object.FindFirstObjectByType<PlayerController>();
        var cc = player.GetComponent<CharacterController>(); cc.enabled = false;
        player.transform.position = new Vector3(0.4f, 0.1f, 4.6f); cc.enabled = true; Physics.SyncTransforms();
        Camera.main.GetComponent<CameraFollow>().SnapToTarget();
    }
    static IEnumerator Press(Keyboard keyboard, Key key)
    {
        yield return InputFrame(() => InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)));
        yield return InputFrame(() => InputSystem.QueueStateEvent(keyboard, new KeyboardState()));
    }
    // Keep input closures outside the UnityTest iterator: EnterPlayMode reloads
    // the domain and does not restore compiler-generated display-class objects.
    static IEnumerator SteerKeyboard(Keyboard keyboard, float direction) => InputFrame(() => InputSystem.QueueStateEvent(keyboard,
        direction > 0 ? new KeyboardState(Key.W) : direction < 0 ? new KeyboardState(Key.S) : new KeyboardState()));
    static IEnumerator PullMouse(Mouse mouse, bool held) => InputFrame(() => InputSystem.QueueStateEvent(mouse,
        new MouseState().WithButton(MouseButton.Right, held)));
    static IEnumerator Frames(int count)
    {
        int until = Time.frameCount + count;
        double deadline = EditorApplication.timeSinceStartup + 15;
        while (Time.frameCount < until) { Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline)); yield return null; }
    }
    static IEnumerator InputFrame(Action queue)
    {
        int frame = -1;
        Action before = () => { if (frame < 0 && InputState.currentUpdateType == InputUpdateType.Dynamic) { queue(); frame = Time.frameCount; } };
        InputSystem.onBeforeUpdate += before;
        double deadline = EditorApplication.timeSinceStartup + 15;
        try { while (frame < 0 || Time.frameCount <= frame) { Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline)); yield return null; } }
        finally { InputSystem.onBeforeUpdate -= before; }
    }
}
