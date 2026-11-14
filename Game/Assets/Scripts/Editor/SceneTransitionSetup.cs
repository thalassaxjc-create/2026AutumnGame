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
        report.AppendLine(ConfigureHomeScene());
        EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);
        AssetDatabase.SaveAssets();
        report.AppendLine("已保存三个场景，并重新打开 WorldScene。");
        report.AppendLine(ValidateCurrentWorld());
        return report.ToString();
    }

    public static string ValidateTransitions()
    {
        var report = new StringBuilder();
        report.AppendLine(OpenAndValidate(WorldScenePath, ValidateCurrentWorld));
        report.AppendLine(OpenAndValidate(WorkshopScenePath, ValidateCurrentWorkshop));
        report.AppendLine(OpenAndValidate(SceneTransitionState.HomeScenePath, ValidateCurrentHome));
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

        Transform north = FindDeep("WorkshopNorthMarker");
        Transform south = FindDeep("WorkshopSouthMarker");
        if (north == null || south == null)
            return "WorldScene: 找不到工坊北门或南门标记，已中止该场景配置。";

        ConfigureWorldDoor(
            root,
            "WorkshopNorthEntranceTrigger",
            "WorkshopNorthSpawn",
            north,
            SceneTransitionState.WorkshopSceneName,
            SceneTransitionState.SpawnWorkshopEntrance,
            SceneTransitionState.SpawnWorkshopNorth);
        ConfigureWorldDoor(
            root,
            "WorkshopSouthEntranceTrigger",
            "WorkshopSouthSpawn",
            south,
            SceneTransitionState.WorkshopSceneName,
            SceneTransitionState.SpawnWorkshopEntranceSouth,
            SceneTransitionState.SpawnWorkshopSouth);

        Transform homeDoor = FindDeep("HomeEntranceMarker");
        Transform homeEastDoor = FindDeep("HomeEastEntranceMarker");
        if (homeDoor == null || homeEastDoor == null)
            return "WorldScene: 找不到家西门或东门标记，已中止该场景配置。";
        ConfigureWorldDoor(
            root,
            "HomeEntranceTrigger",
            "HomeExteriorSpawn",
            homeDoor,
            SceneTransitionState.HomeSceneName,
            SceneTransitionState.SpawnHomeEntrance,
            SceneTransitionState.SpawnHomeExterior);
        ConfigureWorldDoor(
            root,
            "HomeEastEntranceTrigger",
            "HomeEastExteriorSpawn",
            homeEastDoor,
            SceneTransitionState.HomeSceneName,
            SceneTransitionState.SpawnHomeEntranceEast,
            SceneTransitionState.SpawnHomeExteriorEast);

        DestroyIfFound("WorkshopEntranceTrigger");
        DestroyIfFound("WorkshopExteriorSpawn");
        ApplyGameplayCamera();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), WorldScenePath);
        return DescribeWorld();
    }

    static void ConfigureWorldDoor(Transform root, string triggerName, string spawnName, Transform marker, string targetScene, string interiorSpawnId, string exteriorSpawnId)
    {
        Vector3 outward = Flatten(marker.forward);
        Vector3 doorPosition = marker.position;
        doorPosition.y = 0f;

        GameObject trigger = EnsureChild(root, triggerName);
        Vector3 triggerSize = Mathf.Abs(Vector3.Dot(outward, Vector3.forward)) > 0.7f
            ? new Vector3(2.6f, 2.2f, 1.6f)
            : new Vector3(1.6f, 2.2f, 2.6f);
        trigger.transform.SetPositionAndRotation(doorPosition + outward * 1.05f + Vector3.up * 1.1f, marker.rotation);
        ConfigureBoxTrigger(trigger, triggerSize);
        DoorTransition door = EnsureComponent<DoorTransition>(trigger);
        door.Configure(targetScene, interiorSpawnId, "");

        GameObject spawn = EnsureChild(root, spawnName);
        Vector3 spawnPos = doorPosition + outward * 4.6f + Vector3.up * 1f;
        spawnPos = NudgeSpawn(spawnPos, outward);
        spawn.transform.SetPositionAndRotation(spawnPos, marker.rotation);
        SceneSpawnPoint spawnPoint = EnsureComponent<SceneSpawnPoint>(spawn);
        spawnPoint.SetSpawnId(exteriorSpawnId);
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

        ConfigureExit(root, "WorkshopExitTriggerA", doorA, SceneTransitionState.SpawnWorkshopNorth, "");
        ConfigureExit(root, "WorkshopExitTriggerB", doorB, SceneTransitionState.SpawnWorkshopSouth, "");
        ConfigureInteriorSpawn(root, "WorkshopEntranceSpawn", doorA, FindDeep("Spawn_DoorAInside"), SceneTransitionState.SpawnWorkshopEntrance);
        ConfigureInteriorSpawn(root, "WorkshopEntranceSpawnSouth", doorB, FindDeep("Spawn_DoorBInside"), SceneTransitionState.SpawnWorkshopEntranceSouth);
        ApplyGameplayCamera();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), WorkshopScenePath);
        return DescribeWorkshop();
    }

    static string ConfigureHomeScene()
    {
        EditorSceneManager.OpenScene(SceneTransitionState.HomeScenePath, OpenSceneMode.Single);
        Transform root = EnsureRoot(RootName).transform;
        EnsureManager(root);
        EnsurePromptCanvas();
        RemoveDuplicateEventSystems();

        Transform door = FindDeep("HomeDoor");
        Transform doorEast = FindDeep("HomeDoorEast");
        if (door == null || doorEast == null)
            return "HomeScene: 找不到 HomeDoor 或 HomeDoorEast，已中止该场景配置。";

        DestroyIfFound("WorkshopExitTriggerA");
        DestroyIfFound("WorkshopExitTriggerB");
        DestroyIfFound("WorkshopEntranceSpawn");
        DestroyIfFound("WorkshopEntranceSpawnSouth");
        ConfigureExit(root, "HomeExitTrigger", door, SceneTransitionState.SpawnHomeExterior, "");
        ConfigureExit(root, "HomeEastExitTrigger", doorEast, SceneTransitionState.SpawnHomeExteriorEast, "");
        ConfigureInteriorSpawn(root, "HomeEntranceSpawn", door, FindDeep("Spawn_HomeInside"), SceneTransitionState.SpawnHomeEntrance);
        ConfigureInteriorSpawn(root, "HomeEastEntranceSpawn", doorEast, FindDeep("Spawn_HomeInsideEast"), SceneTransitionState.SpawnHomeEntranceEast);
        ApplyGameplayCamera();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), SceneTransitionState.HomeScenePath);
        return "HomeScene 已配置。西门Exit=" + Pos("HomeExitTrigger")
            + " 东门Exit=" + Pos("HomeEastExitTrigger")
            + " 西门Spawn=" + Pos("HomeEntranceSpawn")
            + " 东门Spawn=" + Pos("HomeEastEntranceSpawn");
    }

    static void ConfigureExit(Transform root, string name, Transform door, string spawnId, string prompt)
    {
        Vector3 inward = Flatten(door.forward);
        GameObject trigger = EnsureChild(root, name);
        Vector3 triggerSize = Mathf.Abs(Vector3.Dot(inward, Vector3.forward)) > 0.7f
            ? new Vector3(2.4f, 2.3f, 1.5f)
            : new Vector3(1.5f, 2.3f, 2.4f);
        trigger.transform.SetPositionAndRotation(door.position + inward * 1.05f + Vector3.up * 1.1f, door.rotation);
        ConfigureBoxTrigger(trigger, triggerSize);
        DoorTransition doorTransition = EnsureComponent<DoorTransition>(trigger);
        doorTransition.Configure(SceneTransitionState.WorldSceneName, spawnId, prompt);
    }

    static void ConfigureInteriorSpawn(Transform root, string name, Transform door, Transform existingInside, string spawnId)
    {
        Vector3 inward = Flatten(door.forward);
        GameObject spawn = EnsureChild(root, name);
        Vector3 spawnPos = door.position + inward * 3.8f;
        if (existingInside != null)
        {
            Vector3 inside = existingInside.position;
            inside.y = 1f;
            if (Vector3.Dot(inside - door.position, inward) >= 3.2f)
                spawnPos = inside;
        }

        spawnPos.y = 1f;
        spawnPos = NudgeSpawn(spawnPos, inward);
        spawn.transform.SetPositionAndRotation(spawnPos, Quaternion.LookRotation(inward, Vector3.up));
        SceneSpawnPoint spawnPoint = EnsureComponent<SceneSpawnPoint>(spawn);
        spawnPoint.SetSpawnId(spawnId);
    }

    static void ApplyGameplayCamera()
    {
        GameObject cameraGo = GameObject.Find("Main Camera");
        GameObject player = GameObject.Find("Player");
        if (cameraGo == null || player == null)
            return;

        CameraFollow follow = cameraGo.GetComponent<CameraFollow>();
        if (follow == null)
            follow = cameraGo.AddComponent<CameraFollow>();
        follow.ApplyGameplayView(player.transform);

        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller == null)
            return;

        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("moveSpeed").floatValue = 7f;
        SerializedProperty gameplayCamera = serialized.FindProperty("gameplayCamera");
        if (gameplayCamera.objectReferenceValue == null)
            gameplayCamera.objectReferenceValue = cameraGo.GetComponent<Camera>();
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void DestroyIfFound(string name)
    {
        GameObject leftover = FindDeepObject(name);
        if (leftover != null)
            Object.DestroyImmediate(leftover);
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
        report.AppendLine("- 北门进入: " + DescribeTrigger(FindDoor("WorkshopNorthEntranceTrigger")));
        report.AppendLine("- 北门出生: " + DescribeSpawn(FindSpawn(SceneTransitionState.SpawnWorkshopNorth)));
        report.AppendLine("- 南门进入: " + DescribeTrigger(FindDoor("WorkshopSouthEntranceTrigger")));
        report.AppendLine("- 南门出生: " + DescribeSpawn(FindSpawn(SceneTransitionState.SpawnWorkshopSouth)));
        report.AppendLine("- Interact 动作存在: " + (UnityEngine.InputSystem.InputSystem.actions != null && UnityEngine.InputSystem.InputSystem.actions.FindAction("Player/Interact", false) != null));
        report.AppendLine("- 北门出生重叠: " + SpawnOverlaps(FindSpawn(SceneTransitionState.SpawnWorkshopNorth)));
        report.AppendLine("- 南门出生重叠: " + SpawnOverlaps(FindSpawn(SceneTransitionState.SpawnWorkshopSouth)));
        report.AppendLine("- 家西门进入: " + DescribeTrigger(FindDoor("HomeEntranceTrigger")));
        report.AppendLine("- 家西门出生: " + DescribeSpawn(FindSpawn(SceneTransitionState.SpawnHomeExterior)));
        report.AppendLine("- 家西门出生重叠: " + SpawnOverlaps(FindSpawn(SceneTransitionState.SpawnHomeExterior)));
        report.AppendLine("- 家东门进入: " + DescribeTrigger(FindDoor("HomeEastEntranceTrigger")));
        report.AppendLine("- 家东门出生: " + DescribeSpawn(FindSpawn(SceneTransitionState.SpawnHomeExteriorEast)));
        report.AppendLine("- 家东门出生重叠: " + SpawnOverlaps(FindSpawn(SceneTransitionState.SpawnHomeExteriorEast)));
        report.AppendLine("- Build Index World/Workshop/Home: "
            + SceneUtility.GetBuildIndexByScenePath(WorldScenePath) + "/"
            + SceneUtility.GetBuildIndexByScenePath(WorkshopScenePath) + "/"
            + SceneUtility.GetBuildIndexByScenePath(SceneTransitionState.HomeScenePath));
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
        report.AppendLine("- 左上角出门: " + DescribeTrigger(FindDoor("WorkshopExitTriggerA")));
        report.AppendLine("- 右下角出门: " + DescribeTrigger(FindDoor("WorkshopExitTriggerB")));
        report.AppendLine("- 北门内出生: " + DescribeSpawn(FindSpawn(SceneTransitionState.SpawnWorkshopEntrance)));
        report.AppendLine("- 南门内出生: " + DescribeSpawn(FindSpawn(SceneTransitionState.SpawnWorkshopEntranceSouth)));
        report.AppendLine("- 北门内重叠: " + SpawnOverlaps(FindSpawn(SceneTransitionState.SpawnWorkshopEntrance)));
        report.AppendLine("- 南门内重叠: " + SpawnOverlaps(FindSpawn(SceneTransitionState.SpawnWorkshopEntranceSouth)));
        return report.ToString().TrimEnd();
    }

    static string ValidateCurrentHome()
    {
        var report = new StringBuilder();
        report.AppendLine("HomeScene 校验:");
        report.AppendLine("- 路径: " + SceneManager.GetActiveScene().path);
        report.AppendLine("- Player: " + Count<PlayerController>() + "  Camera: " + Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
        report.AppendLine("- SceneTransitionManager: " + Count<SceneTransitionManager>());
        report.AppendLine("- 西门出门: " + DescribeTrigger(FindDoor("HomeExitTrigger")));
        report.AppendLine("- 东门出门: " + DescribeTrigger(FindDoor("HomeEastExitTrigger")));
        report.AppendLine("- 西门内出生: " + DescribeSpawn(FindSpawn(SceneTransitionState.SpawnHomeEntrance)));
        report.AppendLine("- 东门内出生: " + DescribeSpawn(FindSpawn(SceneTransitionState.SpawnHomeEntranceEast)));
        report.AppendLine("- 西门内重叠: " + SpawnOverlaps(FindSpawn(SceneTransitionState.SpawnHomeEntrance)));
        report.AppendLine("- 东门内重叠: " + SpawnOverlaps(FindSpawn(SceneTransitionState.SpawnHomeEntranceEast)));
        return report.ToString().TrimEnd();
    }

    static string DescribeWorld()
    {
        return "WorldScene 已配置。北门出生=" + Pos("WorkshopNorthSpawn")
            + " 南门出生=" + Pos("WorkshopSouthSpawn");
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
        text.text = "";
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
