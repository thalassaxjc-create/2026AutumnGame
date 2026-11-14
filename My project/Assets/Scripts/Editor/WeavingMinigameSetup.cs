#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>把编帘小游戏组件可靠地安装到现有白盒工坊场景。</summary>
public static class WeavingMinigameSetup
{
    const string WorkshopScenePath = "Assets/Scenes/WorkshopScene.unity";
    const string StationName = "WeavingInteractionPoint";
    const string CanvasName = "WeavingMinigameCanvas";

    [InitializeOnLoadMethod]
    static void InstallWhenMissing()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                SceneManager.GetActiveScene().path != WorkshopScenePath)
                return;

            GameObject station = FindDeep(StationName);
            GameObject canvas = FindDeep(CanvasName);
            if (station == null || canvas == null)
                return;

            if (station.GetComponent<WeavingMinigameController>() == null)
                InstallOrUpdate();
        };
    }

    [MenuItem("宣纸帘/编帘小游戏/安装或更新 _F8", false, 330)]
    public static void InstallOrUpdate()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("请先退出 Play 模式，再安装编帘小游戏。");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != WorkshopScenePath)
        {
            if (scene.isDirty)
            {
                Debug.LogError("当前场景有未保存修改。请先保存，再打开 WorkshopScene 安装小游戏。");
                return;
            }

            scene = EditorSceneManager.OpenScene(WorkshopScenePath, OpenSceneMode.Single);
        }

        GameObject station = FindDeep(StationName);
        GameObject canvasObject = FindDeep(CanvasName);
        if (station == null || canvasObject == null)
        {
            Debug.LogError("安装失败：场景必须包含 " + StationName + " 与 " + CanvasName + "。", station);
            return;
        }

        Undo.RecordObject(station, "安装编帘小游戏");
        Undo.RecordObject(canvasObject, "配置编帘小游戏 Canvas");

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

        WeavingMinigameController controller = station.GetComponent<WeavingMinigameController>();
        if (controller == null)
            controller = Undo.AddComponent<WeavingMinigameController>(station);

        WeavingStationTrigger stationTrigger = station.GetComponent<WeavingStationTrigger>();
        if (stationTrigger == null)
            stationTrigger = Undo.AddComponent<WeavingStationTrigger>(station);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        if (canvas == null)
            canvas = Undo.AddComponent<Canvas>(canvasObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

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
        Selection.activeGameObject = station;

        Debug.Log("编帘小游戏安装完成：靠近工作台按 E，Space 开始，F/J 编织，F+J 打结。", station);
    }

    [MenuItem("宣纸帘/编帘小游戏/校验配置", false, 331)]
    public static void ValidateSetup()
    {
        GameObject station = FindDeep(StationName);
        GameObject canvas = FindDeep(CanvasName);
        var report = new StringBuilder();
        report.AppendLine("编帘小游戏校验：");
        report.AppendLine("- 当前场景: " + SceneManager.GetActiveScene().path);
        report.AppendLine("- 工作台交互点: " + (station != null));
        report.AppendLine("- 小游戏 Canvas: " + (canvas != null));
        report.AppendLine("- BoxCollider Trigger: " +
                          (station != null && station.TryGetComponent(out BoxCollider box) && box.isTrigger));
        report.AppendLine("- Kinematic Rigidbody: " +
                          (station != null && station.TryGetComponent(out Rigidbody body) && body.isKinematic && !body.useGravity));
        report.AppendLine("- WeavingStationTrigger: " +
                          (station != null && station.GetComponent<WeavingStationTrigger>() != null));
        report.AppendLine("- WeavingMinigameController: " +
                          (station != null && station.GetComponent<WeavingMinigameController>() != null));
        Debug.Log(report.ToString().TrimEnd(), station);
    }

    [MenuItem("宣纸帘/编帘小游戏/运行时打开预览 _F9", false, 332)]
    public static void OpenRuntimePreview()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("请先进入 Play 模式，再使用运行时预览。");
            return;
        }

        WeavingMinigameController controller = Object.FindFirstObjectByType<WeavingMinigameController>();
        if (controller == null)
        {
            Debug.LogError("运行中的场景找不到 WeavingMinigameController。");
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
