using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 配置第四阶段双向场景切换。不重建白盒，不改建筑布局。
/// </summary>
public static class SceneTransitionSetup
{
    const string WorldScenePath = "Assets/Scenes/WorldScene.unity";
    const string WorkshopScenePath = "Assets/Scenes/WorkshopScene.unity";
    const string RootName = "SceneTransition";
    const string CanvasName = "InteractionPromptCanvas";

    [MenuItem("宣纸帘/配置第四阶段场景切换")]
    public static void SetupMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("请先退出 Play Mode，再配置场景切换。");
            return;
        }

        Debug.Log(ConfigureTransitions());
    }

    [MenuItem("宣纸帘/检查第四阶段场景切换")]
    public static void ValidateMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("请先退出 Play Mode，再检查场景切换。");
            return;
        }

        Debug.Log(ValidateTransitions());
    }

    public static string ConfigureTransitions()
    {
        EditorSceneManager.SaveOpenScenes();
        var report = new StringBuilder();
        report.AppendLine(ConfigureWorldScene());
        report.AppendLine(ConfigureWorkshopScene());
        EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);
        AssetDatabase.SaveAssets();
        report.AppendLine("已保存两个场景，并重新打开 WorldScene。");
        report.AppendLine(ValidateCurrentWorld());
        return report.ToString();
    }

    public static string ValidateTransitions()
    {
        var report = new StringBuilder();
        report.AppendLine(OpenAndValidate(WorldScenePath, ValidateCurrentWorld));
        report.AppendLine(OpenAndValidate(WorkshopScenePath, ValidateCurrentWorkshop));
        EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);
        return report.ToString();
    }

    static string OpenAndValidate(string path, System.Func<string> validator)
    {
        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        return validator();
    }

    static string ConfigureWorldScene()
    {
        EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);
        Transform root = EnsureRoot(RootName).transform;
        EnsureManager(root);
        EnsurePromptCanvas();
        RemoveDuplicateEventSystems();

        Transform marker = FindDeep("WorkshopEntranceMarker");
        if (marker == null)
            return "WorldScene: 找不到 WorkshopEntranceMarker，已中止该场景配置。";

        Vector3 outward = Flatten(marker.forward);
        Vector3 doorPosition = marker.position;
        doorPosition.y = 0f;

        GameObject trigger = EnsureChild(root, "WorkshopEntranceTrigger");
        trigger.transform.SetPositionAndRotation(doorPosition + outward * 1.05f + Vector3.up * 1.1f, marker.rotation);
        ConfigureBoxTrigger(trigger, new Vector3(2.7f, 2.2f, 2.4f));
        DoorTransition door = EnsureComponent<DoorTransition>(trigger);
        door.Configure(SceneTransitionState.WorkshopSceneName, SceneTransitionState.SpawnWorkshopEntrance, "按 E 进入工坊");

        GameObject spawn = EnsureChild(root, "WorkshopExteriorSpawn");
        Vector3 spawnPos = doorPosition + outward * 3.3f + Vector3.up * 1f;
        spawnPos = NudgeSpawn(spawnPos, outward);
        spawn.transform.SetPositionAndRotation(spawnPos, marker.rotation);
        SceneSpawnPoint spawnPoint = EnsureComponent<SceneSpawnPoint>(spawn);
        spawnPoint.SetSpawnId(SceneTransitionState.SpawnWorkshopExterior);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), WorldScenePath);
        return DescribeWorld();
    }

    static string ConfigureWorkshopScene()
    {
        EditorSceneManager.OpenScene(WorkshopScenePath, OpenSceneMode.Single);
        Transform root = EnsureRoot(RootName).transform;
        EnsureManager(root);
        EnsurePromptCanvas();
        RemoveDuplicateEventSystems();

        Transform doorA = FindDeep("DoorA");
        Transform doorB = FindDeep("DoorB");
        if (doorA == null || doorB == null)
            return "WorkshopScene: 找不到 DoorA 或 DoorB，已中止该场景配置。";

        ConfigureExit(root, "WorkshopExitTriggerA", doorA, "按 E 离开工坊");
        ConfigureExit(root, "WorkshopExitTriggerB", doorB, "按 E 离开工坊");

        Transform existingInside = FindDeep("Spawn_DoorAInside");
        GameObject spawn = EnsureChild(root, "WorkshopEntranceSpawn");
        Vector3 inward = Flatten(doorA.forward);
        Vector3 spawnPos = (existingInside != null ? existingInside.position : doorA.position + inward * 2.8f);
        spawnPos.y = 1f;
        spawnPos = NudgeSpawn(spawnPos, inward);
        spawn.transform.SetPositionAndRotation(spawnPos, Quaternion.LookRotation(inward, Vector3.up));
        SceneSpawnPoint spawnPoint = EnsureComponent<SceneSpawnPoint>(spawn);
        spawnPoint.SetSpawnId(SceneTransitionState.SpawnWorkshopEntrance);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), WorkshopScenePath);
        return DescribeWorkshop();
    }

    static void ConfigureExit(Transform root, string name, Transform door, string prompt)
    {
        Vector3 inward = Flatten(door.forward);
        GameObject trigger = EnsureChild(root, name);
        trigger.transform.SetPositionAndRotation(door.position + inward * 1.15f + Vector3.up * 1.1f, door.rotation);
        ConfigureBoxTrigger(trigger, new Vector3(2.6f, 2.3f, 2.0f));
        DoorTransition doorTransition = EnsureComponent<DoorTransition>(trigger);
        doorTransition.Configure(SceneTransitionState.WorldSceneName, SceneTransitionState.SpawnWorkshopExterior, prompt);
    }

    static string ValidateCurrentWorld()
    {
        var report = new StringBuilder();
        report.AppendLine("WorldScene 校验:");
        report.AppendLine("- 路径: " + SceneManager.GetActiveScene().path);
        report.AppendLine("- Player: " + Count<PlayerController>() + "  Camera: " + Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
        report.AppendLine("- SceneTransitionManager: " + Count<SceneTransitionManager>());
        report.AppendLine("- InteractionPromptUI: " + Count<InteractionPromptUI>());
        report.AppendLine("- EventSystem: " + Count<EventSystem>());
        DoorTransition trigger = FindDoor("WorkshopEntranceTrigger");
        SceneSpawnPoint spawn = FindSpawn(SceneTransitionState.SpawnWorkshopExterior);
        report.AppendLine("- WorkshopEntranceTrigger: " + DescribeTrigger(trigger));
        report.AppendLine("- WorkshopExteriorSpawn: " + DescribeSpawn(spawn));
        report.AppendLine("- Interact 动作存在: " + (UnityEngine.InputSystem.InputSystem.actions != null && UnityEngine.InputSystem.InputSystem.actions.FindAction("Player/Interact", false) != null));
        report.AppendLine("- 入口胶囊重叠: " + SpawnOverlaps(spawn));
        report.AppendLine("- Build Index World/Workshop: " + SceneUtility.GetBuildIndexByScenePath(WorldScenePath) + "/" + SceneUtility.GetBuildIndexByScenePath(WorkshopScenePath));
        return report.ToString().TrimEnd();
    }

    static string ValidateCurrentWorkshop()
    {
        var report = new StringBuilder();
        report.AppendLine("WorkshopScene 校验:");
        report.AppendLine("- 路径: " + SceneManager.GetActiveScene().path);
        report.AppendLine("- Player: " + Count<PlayerController>() + "  Camera: " + Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
        report.AppendLine("- SceneTransitionManager: " + Count<SceneTransitionManager>());
        report.AppendLine("- InteractionPromptUI: " + Count<InteractionPromptUI>());
        report.AppendLine("- EventSystem: " + Count<EventSystem>());
        report.AppendLine("- WorkshopExitTriggerA: " + DescribeTrigger(FindDoor("WorkshopExitTriggerA")));
        report.AppendLine("- WorkshopExitTriggerB: " + DescribeTrigger(FindDoor("WorkshopExitTriggerB")));
        report.AppendLine("- WorkshopEntranceSpawn: " + DescribeSpawn(FindSpawn(SceneTransitionState.SpawnWorkshopEntrance)));
        report.AppendLine("- 入口胶囊重叠: " + SpawnOverlaps(FindSpawn(SceneTransitionState.SpawnWorkshopEntrance)));
        return report.ToString().TrimEnd();
    }

    static string DescribeWorld()
    {
        DoorTransition trigger = FindDoor("WorkshopEntranceTrigger");
        SceneSpawnPoint spawn = FindSpawn(SceneTransitionState.SpawnWorkshopExterior);
        return "WorldScene 已配置。Trigger=" + (trigger != null ? trigger.transform.position.ToString("F2") : "无")
            + " Spawn=" + (spawn != null ? spawn.transform.position.ToString("F2") : "无");
    }

    static string DescribeWorkshop()
    {
        return "WorkshopScene 已配置。ExitA=" + Pos("WorkshopExitTriggerA")
            + " ExitB=" + Pos("WorkshopExitTriggerB")
            + " EntranceSpawn=" + Pos("WorkshopEntranceSpawn");
    }

    static string DescribeTrigger(DoorTransition door)
    {
        if (door == null)
            return "缺失";
        BoxCollider box = door.GetComponent<BoxCollider>();
        return "pos " + door.transform.position.ToString("F2")
            + " size " + (box != null ? box.size.ToString("F2") : "?")
            + " trigger=" + (box != null && box.isTrigger)
            + " -> " + door.TargetSceneName + "/" + door.TargetSpawnId;
    }

    static string DescribeSpawn(SceneSpawnPoint spawn)
    {
        if (spawn == null)
            return "缺失";
        return "id=" + spawn.SpawnId + " pos=" + spawn.transform.position.ToString("F2")
            + " yaw=" + spawn.transform.eulerAngles.y.ToString("0");
    }

    static bool SpawnOverlaps(SceneSpawnPoint spawn)
    {
        if (spawn == null)
            return true;
        Vector3 pos = spawn.transform.position;
        return Physics.CheckCapsule(pos + Vector3.up * 0.45f, pos + Vector3.up * 1.5f, 0.4f, ~0, QueryTriggerInteraction.Ignore);
    }

    static Vector3 NudgeSpawn(Vector3 position, Vector3 along)
    {
        for (int i = 0; i < 8; i++)
        {
            if (!Physics.CheckCapsule(position + Vector3.up * 0.45f, position + Vector3.up * 1.5f, 0.4f, ~0, QueryTriggerInteraction.Ignore))
                return position;
            position += along * 0.35f;
        }

        return position;
    }

    static void ConfigureBoxTrigger(GameObject target, Vector3 size)
    {
        BoxCollider box = EnsureComponent<BoxCollider>(target);
        box.isTrigger = true;
        box.center = Vector3.zero;
        box.size = size;
        Collider[] colliders = target.GetComponents<Collider>();
        foreach (Collider collider in colliders)
        {
            if (collider != box)
                Object.DestroyImmediate(collider);
        }

        Rigidbody body = EnsureComponent<Rigidbody>(target);
        body.isKinematic = true;
        body.useGravity = false;
        body.collisionDetectionMode = CollisionDetectionMode.Discrete;
    }

    static void EnsureManager(Transform root)
    {
        SceneTransitionManager[] managers = Object.FindObjectsByType<SceneTransitionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        SceneTransitionManager keep = root.GetComponent<SceneTransitionManager>();
        if (keep == null)
            keep = root.gameObject.AddComponent<SceneTransitionManager>();
        foreach (SceneTransitionManager manager in managers)
        {
            if (manager != null && manager != keep)
                Object.DestroyImmediate(manager);
        }
    }

    static void EnsurePromptCanvas()
    {
        InteractionPromptUI[] existing = Object.FindObjectsByType<InteractionPromptUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        GameObject canvasGo = null;
        if (existing.Length > 0)
            canvasGo = existing[0].gameObject;
        if (canvasGo == null)
            canvasGo = GameObject.Find(CanvasName);
        if (canvasGo == null)
            canvasGo = new GameObject(CanvasName);

        canvasGo.name = CanvasName;
        Canvas canvas = EnsureComponent<Canvas>(canvasGo);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
        CanvasScaler scaler = EnsureComponent<CanvasScaler>(canvasGo);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        CanvasGroup group = EnsureComponent<CanvasGroup>(canvasGo);
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        Transform textTransform = canvasGo.transform.Find("PromptText");
        GameObject textGo = textTransform != null ? textTransform.gameObject : new GameObject("PromptText");
        textGo.transform.SetParent(canvasGo.transform, false);
        Text text = EnsureComponent<Text>(textGo);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 72f);
        rect.sizeDelta = new Vector2(900f, 72f);
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 34;
        text.color = Color.white;
        text.raycastTarget = false;
        text.text = "按 E 进入工坊";
        InteractionPromptUI.EnsureChineseFont(text);
        Outline outline = EnsureComponent<Outline>(textGo);
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);

        InteractionPromptUI ui = EnsureComponent<InteractionPromptUI>(canvasGo);
        SerializedObject serialized = new SerializedObject(ui);
        serialized.FindProperty("label").objectReferenceValue = text;
        serialized.FindProperty("canvasGroup").objectReferenceValue = group;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        for (int i = 1; i < existing.Length; i++)
        {
            if (existing[i] != null && existing[i].gameObject != canvasGo)
                Object.DestroyImmediate(existing[i].gameObject);
        }
    }

    static void RemoveDuplicateEventSystems()
    {
        EventSystem[] systems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 1; i < systems.Length; i++)
            Object.DestroyImmediate(systems[i].gameObject);
    }

    static GameObject EnsureRoot(string name)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == name)
                return root;
        }

        return new GameObject(name);
    }

    static GameObject EnsureChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            return existing.gameObject;

        GameObject deep = FindDeepObject(name);
        if (deep != null)
        {
            deep.transform.SetParent(parent, true);
            deep.name = name;
            return deep;
        }

        var created = new GameObject(name);
        created.transform.SetParent(parent, false);
        return created;
    }

    static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    static DoorTransition FindDoor(string name)
    {
        GameObject go = FindDeepObject(name);
        return go != null ? go.GetComponent<DoorTransition>() : null;
    }

    static SceneSpawnPoint FindSpawn(string id)
    {
        SceneSpawnPoint[] points = Object.FindObjectsByType<SceneSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (SceneSpawnPoint point in points)
        {
            if (point != null && point.SpawnId == id)
                return point;
        }

        return null;
    }

    static Transform FindDeep(string name)
    {
        GameObject go = FindDeepObject(name);
        return go != null ? go.transform : null;
    }

    static GameObject FindDeepObject(string name)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == name)
                return root;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                    return child.gameObject;
            }
        }

        return null;
    }

    static string Pos(string name)
    {
        Transform t = FindDeep(name);
        return t != null ? t.position.ToString("F2") : "无";
    }

    static int Count<T>() where T : Object
    {
        return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
    }

    static Vector3 Flatten(Vector3 value)
    {
        value.y = 0f;
        return value.sqrMagnitude < 0.0001f ? Vector3.forward : value.normalized;
    }
}
