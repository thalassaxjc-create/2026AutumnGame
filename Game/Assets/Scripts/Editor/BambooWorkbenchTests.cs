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

public class BambooWorkbenchTests
{
    bool restoreInput, previousRunInBackground;
    int previousFrameRate;
    InputSettings.BackgroundBehavior previousBackground;
    InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;

    [UnityTearDown]
    public IEnumerator CleanupInputAfterFailure()
    {
        for (int i = InputSystem.devices.Count - 1; i >= 0; i--)
            if (InputSystem.devices[i].name.StartsWith("WorkbenchTest")) InputSystem.RemoveDevice(InputSystem.devices[i]);
        RestoreInput();
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    void RestoreInput()
    {
        if (!restoreInput) return;
        Application.runInBackground = previousRunInBackground;
        Application.targetFrameRate = previousFrameRate;
        InputSystem.settings.backgroundBehavior = previousBackground;
        InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
        restoreInput = false;
    }

    static BambooCraftProgress Model(float roll = 1) => new BambooCraftProgress(() => roll, () => true);

    [Test]
    public void NoMaterialCannotStartAndPlacementOnlyConsumesOnce()
    {
        var empty = new BambooCraftProgress(null, () => false);
        Assert.That(empty.Place(), Is.False);
        Assert.That(empty.Stage, Is.EqualTo(BambooCraftProgress.Step.PlaceBamboo));
        int stock = 1;
        var model = new BambooCraftProgress(null, () => stock > 0 && stock-- > 0);
        Assert.That(model.Place(), Is.True);
        Assert.That(model.Place(), Is.False);
        Assert.That(stock, Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OneStrokeProducesExactlyFourStrandsOnlyOnce(bool reverse)
    {
        var model = Model(); model.Place();
        Cut(model, reverse);
        Assert.That(model.Splits, Is.EqualTo(1));
        Assert.That(model.Stage, Is.EqualTo(BambooCraftProgress.Step.RoughReady));
        Assert.That(model.EndStroke(), Is.False);
        model.BeginStroke(new Vector2(0, 0.5f)); model.Drag(new Vector2(1, 0.5f), 0.2f);
        Assert.That(model.EndStroke(), Is.False);
        Assert.That(model.RoughCount, Is.EqualTo(4));
        Assert.That(model.SplitAccuracy, Is.EqualTo(1).Within(0.001));
        Assert.That(model.Place(), Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ApproximateCurvedStrokeWithSmallCorrectionSucceeds(bool reverse)
    {
        var model = Model(); model.Place();
        Vector2[] points = RelaxedStroke();
        if (reverse) for (int i = 0; i < points.Length; i++) points[i].x = 1 - points[i].x;
        model.BeginStroke(points[0]);
        for (int i = 1; i < points.Length; i++) model.Drag(points[i], 0.08f);
        Assert.That(model.EndStroke(), Is.True);
        Assert.That(model.RoughCount, Is.EqualTo(4));
        Assert.That(model.SplitAccuracy, Is.InRange(0.5f, 0.99f), "Approximate strokes still report their accuracy.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void StrokeCanStartAndFinishSlightlyBeyondBambooEnds(bool reverse)
    {
        var model = Model(); model.Place();
        model.BeginStroke(new Vector2(reverse ? 1.08f : -0.08f, 0.65f));
        model.Drag(new Vector2(reverse ? -0.1f : 1.1f, 0.68f), 0.15f);
        Assert.That(model.EndStroke(), Is.True);
        Assert.That(model.RoughCount, Is.EqualTo(4));
    }

    [Test]
    public void ClickShortVerticalOffSurfaceAndLargeBacktrackingDoNotSplit()
    {
        var model = Model(); model.Place();
        model.BeginStroke(new Vector2(0, model.GuideY));
        Assert.That(model.EndStroke(), Is.False);
        model.BeginStroke(new Vector2(0, model.GuideY)); model.Drag(new Vector2(0.3f, model.GuideY), 1);
        Assert.That(model.EndStroke(), Is.False);
        model.BeginStroke(new Vector2(0.2f, 0.1f)); model.Drag(new Vector2(0.2f, 0.9f), 1);
        Assert.That(model.EndStroke(), Is.False);
        model.BeginStroke(new Vector2(0, 1.15f)); model.Drag(new Vector2(1, 1.15f), 1);
        Assert.That(model.EndStroke(), Is.False);
        model.BeginStroke(new Vector2(0, model.GuideY)); model.Drag(new Vector2(0.5f, 2), 1); model.Drag(new Vector2(1, model.GuideY), 1);
        Assert.That(model.EndStroke(), Is.False);
        model.BeginStroke(new Vector2(0, model.GuideY)); model.Drag(new Vector2(0.7f, model.GuideY), 1);
        model.Drag(new Vector2(0.2f, model.GuideY), 1); model.Drag(new Vector2(1, model.GuideY), 1);
        Assert.That(model.EndStroke(), Is.False);
        Assert.That(model.Splits, Is.Zero);
        Assert.That(model.RoughCount, Is.Zero);
        Assert.That(model.SplitAccuracy, Is.Zero);
    }

    [Test]
    public void FourCleanStripsConserveMaterialsAndGiveFourFineStrips()
    {
        var model = Model(); PrepareRough(model);
        for (int i = 1; i <= 4; i++)
        {
            Assert.That(model.StartNextStrip(), Is.True);
            Assert.That(model.StartNextStrip(), Is.False);
            ScrapeClean(model);
            Assert.That(model.FineCount, Is.EqualTo(i));
            Assert.That(model.RoughCount, Is.EqualTo(4 - i));
            Assert.That(model.Damage, Is.Zero);
            Assert.That(model.Coverage, Is.EqualTo(1));
        }
        Assert.That(model.Stage, Is.EqualTo(BambooCraftProgress.Step.BatchFinished));
        Assert.That(model.DefectiveCount, Is.Zero);
        Assert.That(model.StartNextStrip(), Is.False);
        int total = model.FineCount;
        model.Drag(Vector2.one, 1); model.EndStroke();
        Assert.That(model.FineCount, Is.EqualTo(total));
    }

    [Test]
    public void FastSweepHitsAllKnotsAndCanCreateOneDefectiveStrip()
    {
        var model = Model(0); PrepareRough(model); model.StartNextStrip();
        model.BeginStroke(new Vector2(0, 0.5f)); model.Drag(new Vector2(1, 0.5f), 0.01f); model.EndStroke();
        Assert.That(model.Damage, Is.EqualTo(3), "A fast jump must not tunnel through knots.");
        Assert.That(model.Scars.Count, Is.EqualTo(3));
        Assert.That(model.DefectChance, Is.GreaterThan(0));
        Assert.That(model.Accuracy, Is.LessThan(1));
        ScrapeClean(model);
        Assert.That(model.DefectiveCount, Is.EqualTo(1));
        Assert.That(model.FineCount, Is.Zero);
        Assert.That(model.RoughCount, Is.EqualTo(3));
    }

    [Test]
    public void ControlledShortReversalAtKnotIsSafeButLongReversalIsNot()
    {
        var model = Model(); PrepareRough(model); model.StartNextStrip();
        model.BeginStroke(new Vector2(0.18f, 0.5f)); model.Drag(new Vector2(0.235f, 0.5f), 0.3f);
        model.Drag(new Vector2(0.22f, 0.5f), 0.01f);
        Assert.That(model.Damage, Is.Zero);
        model.Drag(new Vector2(0.8f, 0.5f), 2f);
        model.Drag(new Vector2(0.1f, 0.5f), 0.01f);
        Assert.That(model.Damage, Is.GreaterThan(0));
    }

    [Test]
    public void HeldStrokeCanStartOutsideLeaveAndResumeWithoutRepressing()
    {
        var model = Model(); PrepareRough(model); model.StartNextStrip();
        model.BeginStroke(new Vector2(-0.2f, 0.1f)); model.Drag(new Vector2(-0.1f, 0.1f), 1);
        Assert.That(model.Coverage, Is.Zero);
        model.Drag(new Vector2(0.15f, 0.1f), 0.5f);
        Assert.That(model.Coverage, Is.GreaterThan(0));
        model.Drag(new Vector2(-0.1f, 0.1f), 0.5f);
        float coverage = model.Coverage;
        model.Drag(new Vector2(-0.1f, 0.9f), 0.1f);
        Assert.That(model.Coverage, Is.EqualTo(coverage), "Off-surface movement must not paint a clamped edge.");
        Assert.That(model.ScrapeAccuracy, Is.EqualTo(1));
        model.Drag(new Vector2(0.15f, 0.9f), 0.5f);
        Assert.That(model.Coverage, Is.GreaterThan(coverage), "Re-entry continues without a second BeginStroke.");
        Assert.That(model.Damage, Is.Zero);
        model.EndStroke();
        coverage = model.Coverage;
        model.Drag(new Vector2(1, 0.9f), 1);
        Assert.That(model.Coverage, Is.EqualTo(coverage), "Releasing still puts the knife down.");
    }

    [Test]
    public void NormalSpeedAndSmallMouseJitterDoNotDamageKnots()
    {
        var model = Model(); PrepareRough(model); model.StartNextStrip();
        model.BeginStroke(new Vector2(0, 0.5f));
        for (int x = 1; x <= 40; x++) model.Drag(new Vector2(x / 40f, 0.5f), 0.025f);
        model.EndStroke();
        model.BeginStroke(new Vector2(0.23f, 0.7f));
        for (int i = 0; i < 20; i++) model.Drag(new Vector2(i % 2 == 0 ? 0.24f : 0.23f, 0.7f), 0.001f);
        Assert.That(model.Damage, Is.Zero);
        Assert.That(model.DefectChance, Is.Zero);
        Assert.That(model.ScrapeAccuracy, Is.EqualTo(1));
    }

    [Test]
    public void ScrapingContinuesPast97PercentUntilEveryCellIsCleanAndAwardsOnlyOnce()
    {
        var model = Model(); PrepareRough(model); model.StartNextStrip();
        for (int row = 1; row <= 7; row += 3)
        {
            model.BeginStroke(new Vector2(-0.1f, row / 11f));
            model.Drag(new Vector2(1.1f, row / 11f), 2);
            model.EndStroke();
        }
        model.BeginStroke(new Vector2(-0.1f, 10 / 11f));
        model.Drag(new Vector2(0.88f, 10 / 11f), 2);
        Assert.That(model.Coverage, Is.InRange(0.97f, 0.99f));
        Assert.That(model.Stage, Is.EqualTo(BambooCraftProgress.Step.Scraping));
        Assert.That(model.FineCount, Is.Zero);
        model.Drag(new Vector2(1.1f, 10 / 11f), 0.5f);
        Assert.That(model.Coverage, Is.EqualTo(1));
        for (int y = 0; y < BambooCraftProgress.Rows; y++)
        for (int x = 0; x < BambooCraftProgress.Columns; x++)
            Assert.That(model.IsRemoved(x, y), Is.True, "All green skin, including edges and knots, must be removable.");
        Assert.That(model.Stage, Is.EqualTo(BambooCraftProgress.Step.StripFinished));
        Assert.That(model.FineCount, Is.EqualTo(1));
        model.EndStroke();
        model.BeginStroke(Vector2.zero); model.Drag(Vector2.one, 1); model.EndStroke();
        Assert.That(model.FineCount, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator RealInputOpensPlacesSplitsScrapesResumesAndKeepsResourcesStable()
    {
        EditorSceneManager.OpenScene(SceneTransitionState.WorkshopScenePath);
        Assert.That(GameObject.Find("BambooAreaMat").GetComponent<BambooWorkbench>(), Is.Not.Null);
        yield return new EnterPlayMode();
        previousRunInBackground = Application.runInBackground;
        previousFrameRate = Application.targetFrameRate;
        previousBackground = InputSystem.settings.backgroundBehavior;
        previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        restoreInput = true;
        Application.runInBackground = true;
        Application.targetFrameRate = 60;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var keyboard = InputSystem.AddDevice<Keyboard>("WorkbenchTestKeyboard");
        var mouse = InputSystem.AddDevice<Mouse>("WorkbenchTestMouse");
        var station = Object.FindFirstObjectByType<BambooWorkbench>();
        var player = Object.FindFirstObjectByType<PlayerController>();
        Move(player, new Vector3(0.4f, 0.1f, 4.6f));
        yield return GameSeconds(0.1f);
        Assert.That(InteractionPromptUI.CurrentMessage, Is.EqualTo("按 E 进入篾竹工作台"));
        Vector3 oldCamera = Camera.main.transform.position;
        yield return KeyPress(keyboard, Key.E);
        Assert.That(station.IsOpen, Is.True);
        Assert.That(player.enabled, Is.False);
        Assert.That(Vector3.Distance(oldCamera, Camera.main.transform.position), Is.GreaterThan(1));
        yield return KeyPress(keyboard, Key.Space);
        Assert.That(station.Progress.Stage, Is.EqualTo(BambooCraftProgress.Step.PlaceBamboo));
        GreenBambooBag.AddOne();
        yield return KeyPress(keyboard, Key.Space);
        Assert.That(GreenBambooBag.Count, Is.Zero);
        Assert.That(station.Progress.Stage, Is.EqualTo(BambooCraftProgress.Step.Splitting));
        Canvas.ForceUpdateCanvases();
        var renderer = station.View.SurfaceRect.GetComponent<CanvasRenderer>();
        Assert.That(renderer, Is.Not.Null, "The interactive bamboo must have a renderer.");
        Assert.That(renderer.cull, Is.False, "The placed bamboo must not be culled.");
        ScreenCapture.CaptureScreenshot("Logs/BambooHarvest/workbench-test-splitting.png");

        Vector2[] stroke = RelaxedStroke();
        foreach (Vector2 point in stroke) yield return MouseStateAt(mouse, station, point, true);
        yield return MouseStateAt(mouse, station, stroke[stroke.Length - 1], false);
        Assert.That(station.Progress.Splits, Is.EqualTo(1));
        Assert.That(station.Progress.Stage, Is.EqualTo(BambooCraftProgress.Step.RoughReady));
        Assert.That(station.Progress.RoughCount, Is.EqualTo(4));
        ScreenCapture.CaptureScreenshot("Logs/BambooHarvest/workbench-test-split-complete.png");
        yield return GameSeconds(0.1f);
        yield return KeyPress(keyboard, Key.Space);
        Assert.That(station.Progress.RoughCount, Is.EqualTo(3));
        Vector2 outside = new Vector2(-0.08f, 0.1f);
        yield return MouseStateAt(mouse, station, outside, true);
        AssertKnifeAt(station, outside);
        yield return MouseStateAt(mouse, station, new Vector2(0, 0.1f), true);
        yield return MouseStateAt(mouse, station, new Vector2(0.15f, 0.1f), true);
        Assert.That(station.Progress.Coverage, Is.GreaterThan(0));
        yield return MouseStateAt(mouse, station, outside, true);
        AssertKnifeAt(station, outside);
        float beforeOutsideMove = station.Progress.Coverage;
        outside.y = 0.9f;
        yield return MouseStateAt(mouse, station, outside, true);
        AssertKnifeAt(station, outside);
        Assert.That(station.Progress.Coverage, Is.EqualTo(beforeOutsideMove));
        foreach (AudioSource source in station.View.GetComponents<AudioSource>())
            if (source.loop) Assert.That(source.isPlaying, Is.False, "No scraping sound while the knife is off the bamboo.");
        yield return MouseStateAt(mouse, station, new Vector2(0.15f, 0.9f), true);
        Assert.That(station.Progress.Coverage, Is.GreaterThan(beforeOutsideMove));
        yield return MouseStateAt(mouse, station, new Vector2(0.15f, 0.9f), false);
        Assert.That(station.Progress.Damage, Is.Zero);
        float coverage = station.Progress.Coverage;
        yield return KeyPress(keyboard, Key.Escape);
        Assert.That(station.IsOpen, Is.False);
        Assert.That(player.enabled, Is.True);
        Assert.That(Vector3.Distance(oldCamera, Camera.main.transform.position), Is.LessThan(0.2f));
        yield return KeyPress(keyboard, Key.E);
        Assert.That(station.Progress.Coverage, Is.EqualTo(coverage));
        int fonts = Resources.FindObjectsOfTypeAll<Font>().Length;
        int materials = Resources.FindObjectsOfTypeAll<Material>().Length;
        int clips = Resources.FindObjectsOfTypeAll<AudioClip>().Length;
        for (int i = 0; i < 8; i++)
        {
            yield return KeyPress(keyboard, Key.Escape);
            yield return KeyPress(keyboard, Key.E);
        }
        Assert.That(Resources.FindObjectsOfTypeAll<Font>().Length, Is.EqualTo(fonts));
        Assert.That(Resources.FindObjectsOfTypeAll<Material>().Length, Is.EqualTo(materials));
        Assert.That(Resources.FindObjectsOfTypeAll<AudioClip>().Length, Is.EqualTo(clips));
        Assert.That(Object.FindObjectsByType<BambooWorkbenchView>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(1));
        // Complete real mouse coverage slowly, including the raised nodes.
        bool sawNearComplete = false;
        for (int row = 1; row < 12 && station.Progress.Stage == BambooCraftProgress.Step.Scraping; row += 3)
        {
            float y = row / 11f;
            yield return MouseStateAt(mouse, station, new Vector2(0, y), true);
            for (int x = 1; x <= 384 && station.Progress.Stage == BambooCraftProgress.Step.Scraping; x++)
            {
                yield return MouseStateAt(mouse, station, new Vector2(x / 384f, y), true);
                if (station.Progress.Coverage >= 0.965f && station.Progress.Coverage < 1)
                {
                    sawNearComplete = true;
                    Assert.That(station.Progress.Stage, Is.EqualTo(BambooCraftProgress.Step.Scraping));
                    Assert.That(station.Progress.FineCount, Is.Zero);
                }
            }
            yield return MouseStateAt(mouse, station, new Vector2(1, y), false);
            Debug.Log("Workbench scrape row " + row + ": coverage=" + station.Progress.Coverage + ", damage=" + station.Progress.Damage);
            if (row == 1) ScreenCapture.CaptureScreenshot("Logs/BambooHarvest/workbench-test-scraping.png");
        }
        Assert.That(station.Progress.FineCount, Is.EqualTo(1), "Coverage=" + station.Progress.Coverage + ", damage=" + station.Progress.Damage + ", stage=" + station.Progress.Stage);
        Assert.That(station.Progress.DefectiveCount, Is.Zero);
        Assert.That(station.Progress.RoughCount, Is.EqualTo(3));
        Assert.That(station.Progress.Damage, Is.Zero);
        Assert.That(sawNearComplete, Is.True);
        Assert.That(station.Progress.Coverage, Is.EqualTo(1));
        ScreenCapture.CaptureScreenshot("Logs/BambooHarvest/workbench-test-scraping-complete.png");
        yield return GameSeconds(0.1f);
        yield return KeyPress(keyboard, Key.Escape);
        yield return SceneManager.LoadSceneAsync(SceneTransitionState.HomeSceneName);
        yield return GameSeconds(0.1f);
        Assert.That(Object.FindObjectsByType<BambooWorkbenchView>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.Zero);
        Assert.That(BambooCraftSession.Current.FineCount, Is.EqualTo(1));
        yield return SceneManager.LoadSceneAsync(SceneTransitionState.WorkshopSceneName);
        yield return GameSeconds(0.1f);
        station = Object.FindFirstObjectByType<BambooWorkbench>();
        player = Object.FindFirstObjectByType<PlayerController>();
        Move(player, new Vector3(0.4f, 0.1f, 4.6f));
        yield return KeyPress(keyboard, Key.E);
        Assert.That(station.IsOpen, Is.True);
        Assert.That(station.Progress.FineCount, Is.EqualTo(1));
        Assert.That(station.Progress.RoughCount, Is.EqualTo(3));
        station.Close();
        Move(player, new Vector3(10, 0.1f, 0));
        yield return KeyPress(keyboard, Key.E);
        Assert.That(station.IsOpen, Is.False, "E outside the mat must not open the minigame.");
        InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
        RestoreInput();
        yield return new ExitPlayMode();
    }

    static Vector2[] RelaxedStroke() => new[] {
        new Vector2(0.22f, 0.8f), new Vector2(0.43f, 0.63f), new Vector2(0.38f, 0.82f),
        new Vector2(0.64f, 1.08f), new Vector2(0.84f, 0.65f)
    };
    static void PrepareRough(BambooCraftProgress model) { model.Place(); Cut(model); }
    static void Cut(BambooCraftProgress model, bool reverse = false)
    {
        model.BeginStroke(new Vector2(reverse ? 1 : 0, model.GuideY));
        for (int i = 1; i <= 20; i++) model.Drag(new Vector2(reverse ? 1 - i / 20f : i / 20f, model.GuideY), 0.1f);
        Assert.That(model.EndStroke(), Is.True);
    }
    static void ScrapeClean(BambooCraftProgress model)
    {
        for (int row = 1; row < 12 && model.Stage == BambooCraftProgress.Step.Scraping; row += 3)
        {
            model.BeginStroke(new Vector2(0, row / 11f));
            for (int x = 1; x <= 80; x++) model.Drag(new Vector2(x / 80f, row / 11f), 0.05f);
            model.EndStroke();
        }
    }
    static void Move(PlayerController player, Vector3 position)
    {
        var cc = player.GetComponent<CharacterController>(); cc.enabled = false;
        player.transform.position = position; cc.enabled = true; Physics.SyncTransforms();
        Camera.main.GetComponent<CameraFollow>().SnapToTarget();
    }
    static void AssertKnifeAt(BambooWorkbench station, Vector2 point)
    {
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, station.View.KnifeRect.position);
        station.View.ScreenToSurface(screen, out Vector2 knifePoint);
        Assert.That(Vector2.Distance(knifePoint, point), Is.LessThan(0.002f), "The held knife must follow the pointer outside the bamboo too.");
    }
    static IEnumerator KeyPress(Keyboard keyboard, Key key)
    {
        yield return InputFrame(() => InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)));
        yield return InputFrame(() => InputSystem.QueueStateEvent(keyboard, new KeyboardState()));
    }
    static IEnumerator MouseStateAt(Mouse mouse, BambooWorkbench station, Vector2 point, bool pressed)
    {
        RectTransform rect = station.View.SurfaceRect;
        Vector3 world = rect.TransformPoint(new Vector3(rect.rect.xMin + point.x * rect.rect.width, rect.rect.yMin + point.y * rect.rect.height));
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, world);
        var state = new MouseState { position = screen }.WithButton(MouseButton.Left, pressed);
        yield return InputFrame(() => InputSystem.QueueStateEvent(mouse, state));
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
    static IEnumerator GameSeconds(float seconds)
    {
        float end = Time.time + seconds;
        double deadline = EditorApplication.timeSinceStartup + 15;
        while (Time.time < end) { Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline)); yield return null; }
    }
}
