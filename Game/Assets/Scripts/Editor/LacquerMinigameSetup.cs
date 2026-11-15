#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 将刷大漆小游戏安装到 WorkshopScene 已有的 LacquerInteractionPoint。
/// 脚本首次编译或退出 Play 模式后会自动安装，也可从菜单手动更新。
/// </summary>
[InitializeOnLoad]
public static class LacquerMinigameSetup
{
    const string SceneName = "WorkshopScene";
    const string ScenePath = "Assets/Scenes/WorkshopScene.unity";
    const string StationName = "LacquerInteractionPoint";
    const string CanvasName = "LacquerMinigameCanvas";

    static LacquerMinigameSetup()
    {
        EditorApplication.delayCall += TryAutoInstall;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += TryAutoInstall;
    }

    static void TryAutoInstall()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.name != SceneName)
            return;

        GameObject station = FindDeep(StationName);
        GameObject canvas = FindDeep(CanvasName);
        bool needsInstall = station != null &&
                            (canvas == null || station.GetComponent<LacquerMinigameController>() == null ||
                             station.GetComponent<LacquerStationTrigger>() == null ||
                             station.GetComponent<BoxCollider>() == null || station.GetComponent<Rigidbody>() == null);

        if (needsInstall)
            InstallOrUpdateInternal(false);
    }

    [MenuItem("宣纸帘/刷大漆小游戏/安装或更新 _F10", false, 340)]
    public static void InstallOrUpdate()
    {
        InstallOrUpdateInternal(true);
    }

    /// <summary>供 CI 或 Unity -batchmode 使用：打开工坊场景并完成安装保存。</summary>
    public static void InstallFromCommandLine()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid() || !scene.isLoaded)
            throw new System.InvalidOperationException("无法打开场景：" + ScenePath);

        InstallOrUpdateInternal(false);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static void InstallOrUpdateInternal(bool selectStation)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("请先退出 Play 模式，再安装刷大漆小游戏。");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("当前没有可编辑的场景。");
            return;
        }

        GameObject station = FindDeep(StationName);
        if (station == null)
        {
            Debug.LogError("场景中找不到 " + StationName + "。请先打开 WorkshopScene。");
            return;
        }

        GameObject canvasObject = FindDeep(CanvasName);
        if (canvasObject == null)
        {
            canvasObject = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create Lacquer Minigame Canvas");
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
        }

        BoxCollider trigger = station.GetComponent<BoxCollider>();
        if (trigger == null)
            trigger = Undo.AddComponent<BoxCollider>(station);
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 1f, 0f);
        trigger.size = new Vector3(3f, 2f, 3f);

        Rigidbody body = station.GetComponent<Rigidbody>();
        if (body == null)
            body = Undo.AddComponent<Rigidbody>(station);
        body.useGravity = false;
        body.isKinematic = true;

        LacquerMinigameController controller = station.GetComponent<LacquerMinigameController>();
        if (controller == null)
            controller = Undo.AddComponent<LacquerMinigameController>(station);

        LacquerStationTrigger stationTrigger = station.GetComponent<LacquerStationTrigger>();
        if (stationTrigger == null)
            stationTrigger = Undo.AddComponent<LacquerStationTrigger>(station);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        if (canvas == null)
            canvas = Undo.AddComponent<Canvas>(canvasObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 210;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = Undo.AddComponent<CanvasScaler>(canvasObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (canvasObject.GetComponent<GraphicRaycaster>() == null)
            Undo.AddComponent<GraphicRaycaster>(canvasObject);

        controller.Configure(canvasObject);
        stationTrigger.Configure(canvasObject, controller);
        canvasObject.SetActive(false);

        EditorUtility.SetDirty(trigger);
        EditorUtility.SetDirty(body);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(stationTrigger);
        EditorUtility.SetDirty(canvasObject);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        if (selectStation)
            Selection.activeGameObject = station;

        Debug.Log("刷大漆小游戏安装完成：靠近漆台按 E，双色漆点点按，长漆条按住顺箭头拖动。", station);
    }

    [MenuItem("宣纸帘/刷大漆小游戏/校验配置", false, 341)]
    public static void ValidateSetup()
    {
        GameObject station = FindDeep(StationName);
        GameObject canvas = FindDeep(CanvasName);
        var report = new StringBuilder();
        report.AppendLine("刷大漆小游戏校验：");
        report.AppendLine("- 当前场景: " + SceneManager.GetActiveScene().path);
        report.AppendLine("- 漆台交互点: " + (station != null));
        report.AppendLine("- 小游戏 Canvas: " + (canvas != null));
        report.AppendLine("- BoxCollider Trigger: " +
                          (station != null && station.TryGetComponent(out BoxCollider box) && box.isTrigger));
        report.AppendLine("- Kinematic Rigidbody: " +
                          (station != null && station.TryGetComponent(out Rigidbody body) && body.isKinematic && !body.useGravity));
        report.AppendLine("- LacquerStationTrigger: " +
                          (station != null && station.GetComponent<LacquerStationTrigger>() != null));
        report.AppendLine("- LacquerMinigameController: " +
                          (station != null && station.GetComponent<LacquerMinigameController>() != null));
        Debug.Log(report.ToString().TrimEnd(), station);
    }

    [MenuItem("宣纸帘/刷大漆小游戏/运行时打开预览 _F11", false, 342)]
    public static void OpenRuntimePreview()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("请先进入 Play 模式，再使用刷大漆运行时预览。");
            return;
        }

        LacquerMinigameController controller = Object.FindFirstObjectByType<LacquerMinigameController>();
        if (controller == null)
        {
            Debug.LogError("运行中的场景找不到 LacquerMinigameController。");
            return;
        }

        controller.OpenPreview();
    }

    static GameObject FindDeep(string objectName)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == objectName)
                return root;

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == objectName)
                    return child.gameObject;
            }
        }

        return null;
    }
}
#endif
