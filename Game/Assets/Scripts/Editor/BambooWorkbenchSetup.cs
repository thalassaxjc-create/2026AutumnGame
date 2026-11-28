using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

/// <summary>只配置工位组件。复用已打开的 Unity 执行配置/验收，不启动第二个编辑器。</summary>
public static class BambooWorkbenchSetup
{
    const string DirectoryPath = "Logs/BambooHarvest";
    const string CommandPath = DirectoryPath + "/workbench-command.txt";
    const string RestorePracticeKey = "BambooWorkbench.RestorePracticeAfterTests";
    const string PreviewKey = "BambooWorkbench.Preview";
    const string ResumeBatchKey = "BambooWorkbench.ResumeScrapedBatch";
    const string BatchCheckpointPath = DirectoryPath + "/blocked-batch-checkpoint.json";
    static TestRunnerApi testApi;
    static readonly Results callbacks = new Results();
    static double nextPoll;
    static Keyboard previewKeyboard;
    static Key previewKey;
    static int keyFrames;
    static bool previewInputOverride, previousRunInBackground;
    static InputSettings.BackgroundBehavior previousBackground;
    static InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;

    [InitializeOnLoadMethod]
    static void Initialize()
    {
        Environment.SetEnvironmentVariable("BEE_BUILD_THREADS", "2", EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("DOTNET_PROCESSOR_COUNT", "2", EnvironmentVariableTarget.Process);
        EditorApplication.update -= PollCommand;
        EditorApplication.update += PollCommand;
        EditorApplication.playModeStateChanged -= OnPlayMode;
        EditorApplication.playModeStateChanged += OnPlayMode;
        AssemblyReloadEvents.beforeAssemblyReload -= DisposeTestApi;
        AssemblyReloadEvents.beforeAssemblyReload += DisposeTestApi;
        InputSystem.onBeforeUpdate -= SendPreviewKey;
        InputSystem.onBeforeUpdate += SendPreviewKey;
        testApi = ScriptableObject.CreateInstance<TestRunnerApi>();
        testApi.hideFlags = HideFlags.HideAndDontSave;
        testApi.RegisterCallbacks(callbacks);
    }

    static void DisposeTestApi()
    {
        InputSystem.onBeforeUpdate -= SendPreviewKey;
        RestorePreviewInput();
        if (previewKeyboard != null && previewKeyboard.added) InputSystem.RemoveDevice(previewKeyboard);
        previewKeyboard = null;
        if (testApi == null) return;
        testApi.UnregisterCallbacks(callbacks);
        Object.DestroyImmediate(testApi);
    }

    // Fixed local commands only, no arbitrary C# or network listener.
    static void PollCommand()
    {
        if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        nextPoll = EditorApplication.timeSinceStartup + 0.5;
        if (!File.Exists(CommandPath)) return;
        string command = File.ReadAllText(CommandPath).Trim();
        File.Delete(CommandPath);
        try
        {
            switch (command)
            {
                case "configure": Configure(); break;
                case "configure-pattern": PatternStationSetup.Configure(); break;
                case "configure-trim": PatternStationSetup.ConfigureVermilionTrim(); break;
                case "configure-market": VillageMarketSetup.Configure(); break;
                case "configure-player": PlayerVisualSetup.Configure(); break;
                case "configure-grove": VillageMarketSetup.ConfigureSpottedGrove(); break;
                case "configure-black-market": VillageMarketSetup.ConfigureBlackMarket(); break;
                case "configure-official": VillageMarketSetup.ConfigureOfficial(); break;
                case "test": RunAllTests(); break;
                case "cancel-tests": TestRunnerApi.CancelTestRun(SessionState.GetString("BambooWorkbench.TestRunId", "")); break;
                case "test-story": RunTests(new Filter
                {
                    testMode = TestMode.EditMode,
                    testNames = new[] { nameof(WorkshopOrderSceneTests) + "." +
                        nameof(WorkshopOrderSceneTests.StoryOrderAndSettlementUseExistingSceneObjects) }
                }, "Running workshop story EditMode test..."); break;
                case "test-lacquer-strokes": RunTests(new Filter
                {
                    testMode = TestMode.EditMode,
                    testNames = new[] { nameof(WorkshopOrderSceneTests) + "." +
                        nameof(WorkshopOrderSceneTests.LacquerStrokesTrackTheGuideAcrossAllSongs) }
                }, "Running lacquer stroke Play Mode interaction..."); break;
                case "test-lacquer-taps": RunTests(new Filter
                {
                    testMode = TestMode.EditMode,
                    testNames = new[] { nameof(WorkshopOrderSceneTests) + "." +
                        nameof(WorkshopOrderSceneTests.FirstLacquerTapStaysPlayableAcrossAllSongs) }
                }, "Running lacquer opening tap Play Mode interaction..."); break;
                case "preview": Preview(); break;
                case "resume-batch": ResumeSavedBatch(); break;
                case "stop": EditorApplication.isPlaying = false; break;
                case "status": WriteStatus(); break;
                case "refresh": AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate); break;
                case "capture": ScreenCapture.CaptureScreenshot(DirectoryPath + "/workbench-" + BambooCraftSession.Current.Stage + ".png"); break;
                case "press-e": PressPreviewKey(Key.E); break;
                case "press-space": PressPreviewKey(Key.Space); break;
                case "press-escape": PressPreviewKey(Key.Escape); break;
                case "maximize": EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).maximized = true; break;
                default: throw new InvalidOperationException("Unknown workbench command.");
            }
            File.WriteAllText(DirectoryPath + "/workbench-command-result.txt", command + ": OK");
        }
        catch (Exception e)
        {
            File.WriteAllText(DirectoryPath + "/workbench-command-result.txt", command + ": " + e);
            Debug.LogException(e);
        }
    }

    [MenuItem("宣纸帘/配置篾竹小游戏")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先停止 Play Mode。");
        EditorSceneManager.SaveOpenScenes();
        if (SceneManager.GetActiveScene().path != SceneTransitionState.WorkshopScenePath)
            EditorSceneManager.OpenScene(SceneTransitionState.WorkshopScenePath);
        GameObject mat = GameObject.Find("BambooAreaMat");
        if (mat == null) throw new InvalidOperationException("找不到 BambooAreaMat。");
        var controller = mat.GetComponent<BambooWorkbench>();
        if (controller == null) controller = mat.AddComponent<BambooWorkbench>();
        GameObject[] canes = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(t => t.name.StartsWith("BambooCane_")).OrderBy(t => t.name).Select(t => t.gameObject).ToArray();
        controller.Configure(GameObject.Find("BambooTable").transform, canes);
        EditorSceneManager.MarkSceneDirty(mat.scene);
        EditorSceneManager.SaveScene(mat.scene);
        Debug.Log("篾竹小游戏已配置：BambooAreaMat 内 E 进入，空格放料，鼠标劈竹/剐青。");
    }

    [MenuItem("宣纸帘/试玩篾竹前两环节")]
    public static void Preview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先停止 Play Mode。");
        Configure();
        SessionState.SetBool(PreviewKey, true);
        EditorApplication.isPlaying = true;
    }

    [MenuItem("宣纸帘/试玩篾竹完整流程")]
    public static void PreviewFull() => Preview();

    // Editor recovery for the completed batch captured before this fix's reload.
    // It restores existing materials; the regular preview's free bamboo is not used.
    static void ResumeSavedBatch()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先停止 Play Mode。");
        ReadBatchCheckpoint();
        Configure();
        SessionState.SetBool(ResumeBatchKey, true);
        EditorApplication.isPaused = false;
        EditorApplication.isPlaying = true;
    }

    static Status ReadBatchCheckpoint()
    {
        var saved = JsonUtility.FromJson<Status>(File.ReadAllText(BatchCheckpointPath));
        if (saved == null || saved.stage != "BatchFinished" || saved.rough != 0 || saved.coverage != 1
            || saved.fine < 0 || saved.defective < 0 || saved.fine + saved.defective != 4
            || saved.raw < 0 || saved.raw > 100 || saved.damage < 0 || saved.accuracy < 0 || saved.accuracy > 1)
            throw new InvalidOperationException("不是有效的四片剐青完成检查点，未恢复材料。");
        return saved;
    }

    static void RestoreCompletedBatch()
    {
        var saved = ReadBatchCheckpoint();
        var model = BambooCraftSession.Current;
        if (model.Stage != BambooCraftProgress.Step.PlaceBamboo || model.ProcessedPieceCount != 0 || GreenBambooBag.Count != 0)
            throw new InvalidOperationException("只能在新进入 Play Mode 时恢复，避免覆盖正在加工的材料。");
        SetModelProperty(model, nameof(model.Stage), BambooCraftProgress.Step.BatchFinished);
        SetModelProperty(model, nameof(model.Splits), saved.cuts);
        SetModelProperty(model, nameof(model.FinishedInBatch), 4);
        SetModelProperty(model, nameof(model.FineCount), saved.fine);
        SetModelProperty(model, nameof(model.DefectiveCount), saved.defective);
        SetModelProperty(model, nameof(model.Damage), saved.damage);
        var removed = (bool[])ModelField("removed").GetValue(model);
        for (int i = 0; i < removed.Length; i++) removed[i] = true;
        ModelField("removedCount").SetValue(model, removed.Length);
        float scrapeScore = Mathf.Min(saved.accuracy, Mathf.Clamp01(1 - saved.damage * 0.045f));
        ModelField("splitAttempts").SetValue(model, 1);
        ModelField("splitScoreSum").SetValue(model, Mathf.Clamp01((saved.accuracy - scrapeScore * 0.65f) / 0.35f));
        ModelField("scrapeLength").SetValue(model, 1f);
        ModelField("unsafeLength").SetValue(model, Mathf.Max(0, 1 - saved.damage * 0.045f - scrapeScore));
        SetModelProperty(model, nameof(model.Message), "四片剐青已完成：合格 " + saved.fine + "、残次 " + saved.defective + "；按空格继续过剑门");
        SetModelProperty(model, nameof(model.Revision), model.Revision + 1);
        for (int i = 0; i < saved.raw; i++) GreenBambooBag.AddOne();
        GreenBambooBag.EnsureLabel();
    }

    static System.Reflection.FieldInfo ModelField(string name) => typeof(BambooCraftProgress).GetField(name,
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("检查点字段不存在：" + name);
    static void SetModelProperty(BambooCraftProgress model, string name, object value) =>
        typeof(BambooCraftProgress).GetProperty(name).SetValue(model, value);

    static void OnPlayMode(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            RestorePreviewInput();
            keyFrames = 0;
            if (previewKeyboard != null && previewKeyboard.added) InputSystem.RemoveDevice(previewKeyboard);
            previewKeyboard = null;
        }
        bool resumeBatch = SessionState.GetBool(ResumeBatchKey, false);
        if (state != PlayModeStateChange.EnteredPlayMode || (!SessionState.GetBool(PreviewKey, false) && !resumeBatch)) return;
        SessionState.EraseBool(PreviewKey);
        SessionState.EraseBool(ResumeBatchKey);
        if (resumeBatch) RestoreCompletedBatch();
        else GreenBambooBag.AddOne(); // Editor preview only; ordinary gameplay never grants free materials.
        var player = Object.FindFirstObjectByType<PlayerController>();
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.transform.position = new Vector3(0.4f, 0.1f, 4.6f);
        cc.enabled = true;
        Camera.main.GetComponent<CameraFollow>().SnapToTarget();
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        Debug.Log(resumeBatch ? "已恢复修复前的四片剐青材料，按 E 回到工位继续。" : "篾竹试玩就绪：测试用青竹 ×1，按 E 进入工作台。");
    }

    static void PressPreviewKey(Key key)
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("请先开始试玩。");
        if (!previewInputOverride)
        {
            previousRunInBackground = Application.runInBackground;
            previousBackground = InputSystem.settings.backgroundBehavior;
            previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            previewInputOverride = true;
        }
        Application.runInBackground = true;
        EditorApplication.isPaused = false;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        if (previewKeyboard == null) previewKeyboard = InputSystem.AddDevice<Keyboard>("WorkbenchPreviewKeyboard");
        previewKeyboard.MakeCurrent();
        previewKey = key;
        keyFrames = 2;
        EditorApplication.QueuePlayerLoopUpdate();
    }

    static void RestorePreviewInput()
    {
        if (!previewInputOverride) return;
        Application.runInBackground = previousRunInBackground;
        InputSystem.settings.backgroundBehavior = previousBackground;
        InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
        previewInputOverride = false;
    }

    static void SendPreviewKey()
    {
        if (keyFrames <= 0 || InputState.currentUpdateType != InputUpdateType.Dynamic || previewKeyboard == null) return;
        InputSystem.QueueStateEvent(previewKeyboard, keyFrames == 2 ? new KeyboardState(previewKey) : new KeyboardState());
        keyFrames--;
    }

    [MenuItem("宣纸帘/运行完整项目测试")]
    public static void RunAllTests() =>
        RunTests(new Filter { testMode = TestMode.EditMode }, "Running full project EditMode tests...");

    static void RunTests(Filter filter, string message)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先停止 Play Mode。");
        EditorApplication.isPaused = false;
        EditorSceneManager.SaveOpenScenes();
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(DirectoryPath + "/workbench-tests-summary.txt", message);
        SessionState.SetBool(RestorePracticeKey, SessionState.GetBool(WorkshopPracticeMode.PreferenceKey, false));
        SessionState.SetBool(WorkshopPracticeMode.PreferenceKey, false);
        try
        {
            SessionState.SetString("BambooWorkbench.TestRunId", testApi.Execute(new ExecutionSettings(filter)));
        }
        catch
        {
            RestorePracticeMode();
            throw;
        }
    }

    static void RestorePracticeMode()
    {
        if (SessionState.GetBool(RestorePracticeKey, false))
            SessionState.SetBool(WorkshopPracticeMode.PreferenceKey, true);
        SessionState.EraseBool(RestorePracticeKey);
    }

    class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor tests) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            RestorePracticeMode();
            Directory.CreateDirectory(DirectoryPath);
            TestRunnerApi.SaveResultToFile(result, DirectoryPath + "/workbench-full-results.xml");
            File.WriteAllText(DirectoryPath + "/workbench-tests-summary.txt",
                result.ResultState + ": " + result.PassCount + " passed, " + result.FailCount + " failed, " + result.SkipCount + " skipped.");
        }
    }

    [Serializable]
    class Status
    {
        public bool playing, open;
        public bool paused, focused;
        public int frame;
        public string stage;
        public int raw, rough, fine, defective, cuts, damage, fonts, materials, clips, uiCount;
        public float coverage, accuracy;
        public Vector3 player;
    }

    static void WriteStatus()
    {
        var controller = Object.FindFirstObjectByType<BambooWorkbench>();
        var player = Object.FindFirstObjectByType<PlayerController>();
        var progress = BambooCraftSession.Current;
        var status = new Status
        {
            playing = EditorApplication.isPlaying, open = controller != null && controller.IsOpen,
            paused = EditorApplication.isPaused, focused = Application.isFocused, frame = Time.frameCount,
            stage = progress.Stage.ToString(), raw = GreenBambooBag.Count, rough = progress.RoughCount,
            fine = progress.FineCount, defective = progress.DefectiveCount, cuts = progress.Splits, damage = progress.Damage,
            coverage = progress.Coverage, accuracy = progress.Accuracy,
            fonts = Resources.FindObjectsOfTypeAll<Font>().Length,
            materials = Resources.FindObjectsOfTypeAll<Material>().Length,
            clips = Resources.FindObjectsOfTypeAll<AudioClip>().Length,
            uiCount = Object.FindObjectsByType<BambooWorkbenchView>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
            player = player != null ? player.transform.position : Vector3.zero
        };
        File.WriteAllText(DirectoryPath + "/workbench-status.json", JsonUtility.ToJson(status, true));
    }
}
