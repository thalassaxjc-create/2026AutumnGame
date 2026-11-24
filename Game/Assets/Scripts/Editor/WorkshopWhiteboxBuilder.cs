using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 第三阶段工坊室内白盒。只操作 WorkshopScene，不改 WorldScene、输入系统和现有移动脚本。
/// </summary>
public static class WorkshopWhiteboxBuilder
{
    const string WorkshopScenePath = "Assets/Scenes/WorkshopScene.unity";
    const string WorldScenePath = "Assets/Scenes/WorldScene.unity";
    const string RootName = "WorkshopRoot";
    const string LitShaderPath = "Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader";
    const string MaterialFolder = "Assets/Materials";

    const float RoomWidth = 30f;
    const float RoomDepth = 20f;
    const float WallHeight = 3.5f;
    const float WallThickness = 0.25f;
    const float DoorWidth = 2.8f;
    const float DoorHeight = 3f;
    const float SouthLowWallHeight = 1f;
    const float TableTopY = 0.86f;

    static readonly Vector3 DoorACenter = new Vector3(-9f, 0f, RoomDepth * 0.5f);
    static readonly Vector3 DoorBCenter = new Vector3(11f, 0f, -RoomDepth * 0.5f);
    static readonly Vector3 PlayerSpawn = new Vector3(-9f, 1f, 7.2f);

    static readonly Vector3 BambooAreaCenter = new Vector3(1.5f, 0f, 6f);
    static readonly Vector3 BambooAreaSize = new Vector3(12f, 0f, 7f);
    static readonly Vector3 WeavingAreaCenter = new Vector3(-10.2f, 0f, -3.8f);
    static readonly Vector3 WeavingAreaSize = new Vector3(9f, 0f, 11f);
    static readonly Vector3 LacquerAreaCenter = new Vector3(1.5f, 0f, -4.8f);
    static readonly Vector3 LacquerAreaSize = new Vector3(10f, 0f, 8f);
    static readonly Vector3 ComputerAreaCenter = new Vector3(12.2f, 0f, 6.5f);
    static readonly Vector3 ComputerAreaSize = new Vector3(5.5f, 0f, 5.5f);

    static Materials Palette;

    struct Materials
    {
        public Material Floor;
        public Material Wall;
        public Material Bamboo;
        public Material Weaving;
        public Material Lacquer;
        public Material Computer;
        public Material Device;
        public Material Wood;
        public Material Player;
    }

    [MenuItem("宣纸帘/生成第三阶段工坊白盒")]
    public static void BuildMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("请先退出 Play Mode，再生成工坊白盒。");
            return;
        }

        Debug.Log(BuildWorkshop());
    }

    [MenuItem("宣纸帘/检查第三阶段工坊白盒")]
    public static void ValidateMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("请先退出 Play Mode，再检查工坊。");
            return;
        }

        if (!OpenWorkshopSceneIfNeeded())
            return;

        Debug.Log(ValidateWorkshop());
    }

    [MenuItem("宣纸帘/切换工坊近景墙体")]
    public static void ToggleNearWallMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("请先退出 Play Mode，再切换墙体。");
            return;
        }

        if (!OpenWorkshopSceneIfNeeded())
            return;

        Transform walls = GameObject.Find(RootName)?.transform.Find("Walls");
        Transform low = walls?.Find("SouthLow");
        Transform full = walls?.Find("SouthFull");
        if (low == null || full == null)
        {
            Debug.LogError("找不到 SouthLow / SouthFull，请先生成第三阶段工坊白盒。");
            return;
        }

        bool showFull = !full.gameObject.activeSelf;
        full.gameObject.SetActive(showFull);
        low.gameObject.SetActive(!showFull);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), WorkshopScenePath);
        Debug.Log(showFull ? "已显示完整南墙。斜俯视可能被挡住。" : "已恢复低南墙，斜俯视可以看到工作台。");
    }

    public static string BuildWorkshop()
    {
        if (!OpenWorkshopSceneIfNeeded())
            return "已取消：当前不能安全打开 WorkshopScene。";

        EnsureFolder(MaterialFolder);
        Palette = CreateMaterials();

        DisableLegacyGround();
        GameObject root = RebuildWorkshopRoot();
        BuildFloor(CreateGroup(root.transform, "Floor"));
        BuildWalls(CreateGroup(root.transform, "Walls"));
        BuildDoors(CreateGroup(root.transform, "Doors"));
        BuildBambooArea(CreateGroup(root.transform, "BambooProcessingArea"));
        BuildWeavingArea(CreateGroup(root.transform, "WeavingArea"));
        BuildLacquerArea(CreateGroup(root.transform, "LacqueringArea"));
        BuildComputerArea(CreateGroup(root.transform, "ComputerArea"));
        BuildToolStorage(CreateGroup(root.transform, "ToolStorage"));
        BuildNavigation(CreateGroup(root.transform, "NavigationSpace"));
        BuildSpawnPoints(CreateGroup(root.transform, "SpawnPoints"));

        GameObject player = EnsurePlayer();
        GameObject cameraGo = EnsureCamera(player);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), WorkshopScenePath);
        AssetDatabase.SaveAssets();

        var report = new StringBuilder();
        report.AppendLine("第三阶段工坊白盒已生成并保存 WorkshopScene。未修改 WorldScene。");
        report.AppendLine(DescribeLayout());
        report.AppendLine(ValidateWorkshop());
        report.AppendLine("摄像机: " + cameraGo.transform.position);
        return report.ToString();
    }

    static bool OpenWorkshopSceneIfNeeded()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.path == WorkshopScenePath)
            return true;

        if (active.path == WorldScenePath)
            EditorSceneManager.SaveOpenScenes();

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(WorkshopScenePath) == null)
        {
            Debug.LogError("找不到 " + WorkshopScenePath);
            return false;
        }

        EditorSceneManager.OpenScene(WorkshopScenePath, OpenSceneMode.Single);
        return true;
    }

    static GameObject RebuildWorkshopRoot()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == RootName)
                Object.DestroyImmediate(root);
        }

        var created = new GameObject(RootName);
        created.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        return created;
    }

    static void DisableLegacyGround()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == "Ground")
                root.SetActive(false);
        }
    }

    static void BuildFloor(Transform parent)
    {
        CreateBox(parent, "WorkshopFloor", new Vector3(0f, -0.5f, 0f), new Vector3(RoomWidth, 1f, RoomDepth), Palette.Floor, true);
        CreateAreaMat(parent, "BambooAreaMat", BambooAreaCenter, BambooAreaSize, Palette.Bamboo);
        CreateAreaMat(parent, "WeavingAreaMat", WeavingAreaCenter, WeavingAreaSize, Palette.Weaving);
        CreateAreaMat(parent, "LacquerAreaMat", LacquerAreaCenter, LacquerAreaSize, Palette.Lacquer);
        CreateAreaMat(parent, "ComputerAreaMat", ComputerAreaCenter, ComputerAreaSize, Palette.Computer);
    }

    static void CreateAreaMat(Transform parent, string name, Vector3 center, Vector3 size, Material material)
    {
        CreateBox(parent, name, new Vector3(center.x, 0.02f, center.z), new Vector3(size.x - 0.2f, 0.04f, size.z - 0.2f), material, false);
    }

    static void BuildWalls(Transform parent)
    {
        float halfW = RoomWidth * 0.5f;
        float halfD = RoomDepth * 0.5f;
        float wallY = WallHeight * 0.5f;
        float zNorth = halfD - WallThickness * 0.5f;
        float zSouth = -halfD + WallThickness * 0.5f;
        float xWest = -halfW + WallThickness * 0.5f;
        float xEast = halfW - WallThickness * 0.5f;

        CreateSplitWall(parent, "North", new Vector3(0f, wallY, zNorth), RoomWidth, WallHeight, DoorACenter.x, DoorWidth, DoorHeight, true, Palette.Wall);
        CreateBox(parent, "WestWall", new Vector3(xWest, wallY, 0f), new Vector3(WallThickness, WallHeight, RoomDepth), Palette.Wall, true);
        CreateBox(parent, "EastWall", new Vector3(xEast, wallY, 0f), new Vector3(WallThickness, WallHeight, RoomDepth), Palette.Wall, true);

        Transform low = CreateGroup(parent, "SouthLow");
        CreateSplitWall(low, "SouthLow", new Vector3(0f, SouthLowWallHeight * 0.5f, zSouth), RoomWidth, SouthLowWallHeight, DoorBCenter.x, DoorWidth, SouthLowWallHeight + 0.1f, false, Palette.Wall);
        low.gameObject.SetActive(true);

        Transform full = CreateGroup(parent, "SouthFull");
        CreateSplitWall(full, "SouthFull", new Vector3(0f, wallY, zSouth), RoomWidth, WallHeight, DoorBCenter.x, DoorWidth, DoorHeight, true, Palette.Wall);
        full.gameObject.SetActive(false);
    }

    static void CreateSplitWall(Transform parent, string prefix, Vector3 center, float length, float height, float doorX, float doorWidth, float doorHeight, bool lintel, Material material)
    {
        float leftWidth = (length * 0.5f + doorX) - doorWidth * 0.5f;
        float rightWidth = (length * 0.5f - doorX) - doorWidth * 0.5f;
        float leftCenterX = -length * 0.5f + leftWidth * 0.5f;
        float rightCenterX = length * 0.5f - rightWidth * 0.5f;

        CreateBox(parent, prefix + "Left", new Vector3(leftCenterX, center.y, center.z), new Vector3(leftWidth, height, WallThickness), material, true);
        CreateBox(parent, prefix + "Right", new Vector3(rightCenterX, center.y, center.z), new Vector3(rightWidth, height, WallThickness), material, true);
        if (!lintel)
            return;

        float lintelHeight = Mathf.Max(0.2f, height - doorHeight);
        CreateBox(parent, prefix + "Lintel", new Vector3(doorX, doorHeight + lintelHeight * 0.5f, center.z), new Vector3(doorWidth, lintelHeight, WallThickness), material, true);
    }

    static void BuildDoors(Transform parent)
    {
        Transform doorA = CreateGroup(parent, "DoorA");
        doorA.position = new Vector3(DoorACenter.x, 0.05f, DoorACenter.z);
        doorA.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
        CreateBox(doorA, "DoorASill", new Vector3(0f, 0.03f, 0f), new Vector3(DoorWidth, 0.06f, 0.6f), Palette.Wood, false);

        Transform doorB = CreateGroup(parent, "DoorB");
        doorB.position = new Vector3(DoorBCenter.x, 0.05f, DoorBCenter.z);
        doorB.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        CreateBox(doorB, "DoorBSill", new Vector3(0f, 0.03f, 0f), new Vector3(DoorWidth, 0.06f, 0.6f), Palette.Wood, false);
    }

    static void BuildBambooArea(Transform parent)
    {
        Vector3 table = new Vector3(0.4f, 0f, 6.6f);
        Transform station = CreateGroup(parent, "Workbench");
        CreateTable(station, "BambooTable", table, 3.4f, 1.35f, TableTopY, Palette.Wood);

        Transform materials = CreateGroup(parent, "Materials");
        Vector3 pile = table + new Vector3(2.35f, 0f, 0.15f);
        for (int i = 0; i < 8; i++)
        {
            float offset = (i - 3.5f) * 0.09f;
            GameObject cane = CreateCylinder(materials, "BambooCane_" + i, pile + new Vector3(offset, 0.16f, 0f), 0.05f, 2.15f, Palette.Bamboo, false);
            cane.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        CreateBox(materials, "BambooPallet", pile + new Vector3(0f, 0.05f, 0f), new Vector3(0.9f, 0.1f, 2.3f), Palette.Wood, true);

        Transform tools = CreateGroup(parent, "Tools");
        CreateBox(tools, "ToolRackPostL", table + new Vector3(-1.9f, 0.9f, 0.7f), new Vector3(0.08f, 1.8f, 0.08f), Palette.Device, true);
        CreateBox(tools, "ToolRackPostR", table + new Vector3(-1.9f, 0.9f, -0.7f), new Vector3(0.08f, 1.8f, 0.08f), Palette.Device, true);
        CreateBox(tools, "ToolShelfLower", table + new Vector3(-1.9f, 0.55f, 0f), new Vector3(0.28f, 0.06f, 1.5f), Palette.Wood, true);
        CreateBox(tools, "ToolShelfUpper", table + new Vector3(-1.9f, 1.15f, 0f), new Vector3(0.28f, 0.06f, 1.5f), Palette.Wood, true);
        CreateBox(tools, "SplittingKnife", table + new Vector3(-1.9f, 1.22f, 0.25f), new Vector3(0.06f, 0.04f, 0.55f), Palette.Device, false);
        GameObject wedge = CreateBox(tools, "SplittingWedge", table + new Vector3(-1.9f, 0.64f, -0.2f), new Vector3(0.08f, 0.14f, 0.22f), Palette.Device, false);
        wedge.transform.rotation = Quaternion.Euler(0f, 18f, 18f);

        CreateInteractionPoint(parent, "BambooInteractionPoint", table + new Vector3(0f, 0.2f, -1.15f), Vector3.forward);
    }

    static void BuildWeavingArea(Transform parent)
    {
        Vector3 loom = new Vector3(-10.4f, 0f, -3.2f);
        Transform frame = CreateGroup(parent, "LoomFrame");
        CreateIBeam(frame, "SupportSouth", loom + new Vector3(0f, 0f, -2.15f));
        CreateIBeam(frame, "SupportNorth", loom + new Vector3(0f, 0f, 2.15f));
        CreateBox(frame, "CrossBeam", loom + new Vector3(0f, 0.72f, 0f), new Vector3(0.12f, 0.12f, 4.2f), Palette.Wood, true);
        CreateBox(frame, "SupportPlane", loom + new Vector3(0f, 1.18f, 0f), new Vector3(1.7f, 0.06f, 4.5f), Palette.Weaving, true);

        Transform strips = CreateGroup(parent, "BambooStrips");
        for (int i = 0; i < 5; i++)
        {
            float x = (i - 2) * 0.22f;
            GameObject strip = CreateCylinder(strips, "Strip_" + (i + 1), loom + new Vector3(x, 1.26f, 0f), 0.032f, 4.1f, Palette.Bamboo, false);
            strip.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        Transform threads = CreateGroup(parent, "Threads");
        for (int i = 0; i < 4; i++)
        {
            float z = -1.4f + i * 0.9f;
            CreateBox(threads, "Thread_" + i, loom + new Vector3(0f, 1.3f, z), new Vector3(1.15f, 0.012f, 0.012f), Palette.Device, false);
        }

        Transform wood = CreateGroup(parent, "WoodChipPlaceholders");
        CreateBox(wood, "WoodChip_A", loom + new Vector3(-0.55f, 1.24f, 0.4f), new Vector3(0.22f, 0.03f, 0.12f), Palette.Wood, false);
        CreateBox(wood, "WoodChip_B", loom + new Vector3(0.5f, 1.24f, -0.55f), new Vector3(0.2f, 0.03f, 0.12f), Palette.Wood, false);

        CreateInteractionPoint(parent, "WeavingInteractionPoint", loom + new Vector3(1.35f, 0.2f, 0f), Vector3.left);
    }

    static void CreateIBeam(Transform parent, string name, Vector3 foot)
    {
        CreateBox(parent, name + "Column", foot + new Vector3(0f, 0.7f, 0f), new Vector3(0.12f, 1.4f, 0.12f), Palette.Wood, true);
        CreateBox(parent, name + "Base", foot + new Vector3(0f, 0.06f, 0f), new Vector3(0.7f, 0.12f, 0.28f), Palette.Wood, true);
        CreateBox(parent, name + "Top", foot + new Vector3(0f, 1.36f, 0f), new Vector3(0.7f, 0.1f, 0.28f), Palette.Wood, true);
    }

    static void BuildLacquerArea(Transform parent)
    {
        Transform lacquer = CreateGroup(parent, "LacquerStation");
        Vector3 table = new Vector3(0.2f, 0f, -3.6f);
        CreateTable(lacquer, "LacquerTable", table, 2.5f, 1.2f, TableTopY, Palette.Wood);
        CreateBox(lacquer, "LacquerTray", table + new Vector3(-0.45f, TableTopY + 0.07f, 0.1f), new Vector3(0.7f, 0.08f, 0.45f), Palette.Lacquer, false);
        CreateBox(lacquer, "BrushHandle", table + new Vector3(0.35f, TableTopY + 0.05f, 0.15f), new Vector3(0.28f, 0.04f, 0.04f), Palette.Wood, false);
        CreateBox(lacquer, "BrushHead", table + new Vector3(0.52f, TableTopY + 0.05f, 0.15f), new Vector3(0.1f, 0.05f, 0.08f), Palette.Device, false);
        CreateInteractionPoint(lacquer, "LacquerInteractionPoint", table + new Vector3(0f, 0.2f, 1.15f), Vector3.back);

        Transform drying = CreateGroup(parent, "DryingStation");
        Vector3 rack = new Vector3(4.4f, 0f, -5.6f);
        CreateBox(drying, "DryingPostL", rack + new Vector3(-0.85f, 0.95f, 0f), new Vector3(0.1f, 1.9f, 0.1f), Palette.Wood, true);
        CreateBox(drying, "DryingPostR", rack + new Vector3(0.85f, 0.95f, 0f), new Vector3(0.1f, 1.9f, 0.1f), Palette.Wood, true);
        CreateBox(drying, "DryingShelf1", rack + new Vector3(0f, 0.55f, 0f), new Vector3(1.8f, 0.06f, 0.45f), Palette.Wood, true);
        CreateBox(drying, "DryingShelf2", rack + new Vector3(0f, 1.05f, 0f), new Vector3(1.8f, 0.06f, 0.45f), Palette.Wood, true);
        CreateBox(drying, "DryingShelf3", rack + new Vector3(0f, 1.55f, 0f), new Vector3(1.8f, 0.06f, 0.45f), Palette.Wood, true);
        CreateBox(drying, "CurtainSheet_A", rack + new Vector3(-0.2f, 1.12f, 0f), new Vector3(1.35f, 0.7f, 0.04f), Palette.Lacquer, false);
        CreateBox(drying, "CurtainSheet_B", rack + new Vector3(0.25f, 1.62f, 0f), new Vector3(1.25f, 0.55f, 0.04f), Palette.Weaving, false);
        CreateInteractionPoint(drying, "DryingInteractionPoint", rack + new Vector3(0f, 0.2f, 1.1f), Vector3.back);
    }

    static void BuildComputerArea(Transform parent)
    {
        Vector3 desk = new Vector3(12.4f, 0f, 6.6f);
        Transform furniture = CreateGroup(parent, "DeskSet");
        CreateTable(furniture, "ComputerDesk", desk, 1.6f, 0.78f, 0.74f, Palette.Computer);
        CreateBox(furniture, "Monitor", desk + new Vector3(0.15f, 1.12f, 0f), new Vector3(0.08f, 0.42f, 0.7f), Palette.Device, false);
        CreateBox(furniture, "MonitorStand", desk + new Vector3(0.1f, 0.84f, 0f), new Vector3(0.12f, 0.16f, 0.18f), Palette.Device, false);
        CreateBox(furniture, "ChairSeat", desk + new Vector3(-1.05f, 0.42f, 0f), new Vector3(0.48f, 0.08f, 0.48f), Palette.Computer, true);
        CreateBox(furniture, "ChairBack", desk + new Vector3(-1.26f, 0.72f, 0f), new Vector3(0.08f, 0.52f, 0.48f), Palette.Computer, true);
        CreateInteractionPoint(parent, "ComputerInteractionPoint", desk + new Vector3(-1.15f, 0.2f, 0f), Vector3.right);
    }

    static void BuildToolStorage(Transform parent)
    {
        Vector3 rack = new Vector3(4.2f, 0f, 9.15f);
        CreateBox(parent, "StorageUprightL", rack + new Vector3(-0.7f, 0.85f, 0f), new Vector3(0.08f, 1.7f, 0.12f), Palette.Wood, true);
        CreateBox(parent, "StorageUprightR", rack + new Vector3(0.7f, 0.85f, 0f), new Vector3(0.08f, 1.7f, 0.12f), Palette.Wood, true);
        CreateBox(parent, "StorageShelf1", rack + new Vector3(0f, 0.45f, 0f), new Vector3(1.5f, 0.06f, 0.38f), Palette.Wood, true);
        CreateBox(parent, "StorageShelf2", rack + new Vector3(0f, 0.95f, 0f), new Vector3(1.5f, 0.06f, 0.38f), Palette.Wood, true);
        CreateBox(parent, "StorageShelf3", rack + new Vector3(0f, 1.45f, 0f), new Vector3(1.5f, 0.06f, 0.38f), Palette.Wood, true);
        CreateBox(parent, "StoredToolA", rack + new Vector3(-0.3f, 1.02f, 0f), new Vector3(0.5f, 0.05f, 0.08f), Palette.Device, false);
        CreateBox(parent, "StoredToolB", rack + new Vector3(0.25f, 1.52f, 0f), new Vector3(0.42f, 0.05f, 0.08f), Palette.Device, false);
    }

    static void BuildNavigation(Transform parent)
    {
        CreateMarker(parent, "CenterLane", new Vector3(0f, 0.05f, 0.8f));
        CreateMarker(parent, "DoorAClearance", new Vector3(DoorACenter.x, 0.05f, 8.4f));
        CreateMarker(parent, "DoorBClearance", new Vector3(DoorBCenter.x, 0.05f, -8.4f));
    }

    static void BuildSpawnPoints(Transform parent)
    {
        CreateMarker(parent, "Spawn_DoorAInside", new Vector3(PlayerSpawn.x, 0.05f, PlayerSpawn.z));
        CreateMarker(parent, "Spawn_DoorBInside", new Vector3(DoorBCenter.x, 0.05f, -7.2f));
    }

    static GameObject EnsurePlayer()
    {
        GameObject player = FindRoot("Player");
        if (player == null)
        {
            player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
        }

        player.transform.SetPositionAndRotation(PlayerSpawn, Quaternion.identity);
        player.transform.localScale = Vector3.one;
        player.tag = "Player";
        DestroyComponentIfPresent<CapsuleCollider>(player);
        DestroyComponentIfPresent<Rigidbody>(player);

        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller == null)
            controller = player.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.45f;
        controller.center = Vector3.zero;
        controller.slopeLimit = 45f;
        controller.stepOffset = 0.3f;
        controller.skinWidth = 0.08f;
        controller.minMoveDistance = 0f;

        if (player.GetComponent<PlayerController>() == null)
            player.AddComponent<PlayerController>();

        Renderer renderer = player.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = Palette.Player;
        return player;
    }

    static GameObject EnsureCamera(GameObject player)
    {
        GameObject cameraGo = FindRoot("Main Camera");
        if (cameraGo == null)
            cameraGo = new GameObject("Main Camera");

        cameraGo.tag = "MainCamera";
        Camera cameraComponent = cameraGo.GetComponent<Camera>();
        if (cameraComponent == null)
            cameraComponent = cameraGo.AddComponent<Camera>();
        cameraComponent.clearFlags = CameraClearFlags.Skybox;
        cameraComponent.depth = -1f;

        if (cameraGo.GetComponent<AudioListener>() == null)
            cameraGo.AddComponent<AudioListener>();
        EnsureUrpComponent(cameraGo, "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData");

        CameraFollow follow = cameraGo.GetComponent<CameraFollow>();
        if (follow == null)
            follow = cameraGo.AddComponent<CameraFollow>();
        follow.ApplyGameplayView(player.transform);

        PlayerController playerController = player.GetComponent<PlayerController>();
        SerializedObject serializedPlayer = new SerializedObject(playerController);
        serializedPlayer.FindProperty("moveSpeed").floatValue = 7f;
        serializedPlayer.FindProperty("gameplayCamera").objectReferenceValue = cameraComponent;
        serializedPlayer.ApplyModifiedPropertiesWithoutUndo();

        AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (AudioListener listener in listeners)
        {
            if (listener != null && listener.gameObject != cameraGo)
                Object.DestroyImmediate(listener);
        }

        return cameraGo;
    }

    public static string ValidateWorkshop()
    {
        var report = new StringBuilder();
        GameObject root = FindRoot(RootName);
        GameObject player = FindRoot("Player");
        GameObject cameraGo = FindRoot("Main Camera");
        report.AppendLine("校验:");
        report.AppendLine("- 当前场景: " + SceneManager.GetActiveScene().path);
        report.AppendLine("- Player 数量: " + CountRoots("Player"));
        report.AppendLine("- Main Camera 数量: " + CountRoots("Main Camera"));
        report.AppendLine("- WorkshopRoot: " + (root != null));
        report.AppendLine("- 旧 Ground 激活: " + LegacyGroundActive());

        string[] groups = { "Floor", "Walls", "Doors", "BambooProcessingArea", "WeavingArea", "LacqueringArea", "ComputerArea", "ToolStorage", "NavigationSpace", "SpawnPoints" };
        if (root != null)
        {
            foreach (string group in groups)
            {
                Transform child = root.transform.Find(group);
                report.AppendLine("- " + group + ": " + (child != null ? child.childCount + " 个子对象" : "缺失"));
            }
        }

        if (player != null)
        {
            report.AppendLine("- 玩家位置: " + player.transform.position);
            report.AppendLine("- PlayerController: " + (player.GetComponent<PlayerController>() != null));
            report.AppendLine("- CharacterController: " + (player.GetComponent<CharacterController>() != null));
        }

        if (cameraGo != null)
        {
            CameraFollow follow = cameraGo.GetComponent<CameraFollow>();
            report.AppendLine("- CameraFollow: " + (follow != null));
            report.AppendLine("- CameraFollow 目标: " + (follow != null && follow.Target != null ? follow.Target.name : "空"));
            report.AppendLine("- 摄像机位置: " + cameraGo.transform.position);
        }

        report.AppendLine("- DoorA: " + (GameObject.Find("DoorA") != null));
        report.AppendLine("- DoorB: " + (GameObject.Find("DoorB") != null));
        report.AppendLine("- BambooInteractionPoint: " + (GameObject.Find("BambooInteractionPoint") != null));
        report.AppendLine("- WeavingInteractionPoint: " + (GameObject.Find("WeavingInteractionPoint") != null));
        report.AppendLine("- LacquerInteractionPoint: " + (GameObject.Find("LacquerInteractionPoint") != null));
        report.AppendLine("- DryingInteractionPoint: " + (GameObject.Find("DryingInteractionPoint") != null));
        report.AppendLine("- ComputerInteractionPoint: " + (GameObject.Find("ComputerInteractionPoint") != null));
        report.AppendLine("- 近景低墙激活: " + (root != null && root.transform.Find("Walls/SouthLow") != null && root.transform.Find("Walls/SouthLow").gameObject.activeSelf));

        CharacterController character = player != null ? player.GetComponent<CharacterController>() : null;
        if (character != null)
            character.enabled = false;
        try
        {
            Physics.SyncTransforms();
            report.AppendLine(CheckSpawn());
            report.AppendLine(CheckDoor("门A", new Vector3(DoorACenter.x, 1.1f, 12.2f), new Vector3(DoorACenter.x, 1.1f, 7.2f)));
            report.AppendLine(CheckDoor("门B", new Vector3(DoorBCenter.x, 1.1f, -12.2f), new Vector3(DoorBCenter.x, 1.1f, -7.2f)));
            report.AppendLine(CheckPath("入口到篾竹", PlayerSpawn, new Vector3(0.4f, 1.2f, 5.3f)));
            report.AppendLine(CheckPath("入口到编帘", PlayerSpawn, new Vector3(-8.8f, 1.2f, -3.2f)));
            report.AppendLine(CheckPath("入口到漆帘", PlayerSpawn, new Vector3(0.2f, 1.2f, -2.3f)));
            report.AppendLine(CheckPath("入口到电脑", PlayerSpawn, new Vector3(10.8f, 1.2f, 6.6f)));
            report.AppendLine(CheckPath("中央通道", new Vector3(-2f, 1.2f, 0.8f), new Vector3(5f, 1.2f, 0.8f)));
        }
        finally
        {
            if (character != null)
                character.enabled = true;
        }

        return report.ToString().TrimEnd();
    }

    static string DescribeLayout()
    {
        var report = new StringBuilder();
        report.AppendLine("布局（按文字平面关系，未使用草图文件）:");
        report.AppendLine("- 房间: 30 x 20，中心(0,0,0)，地面顶面 Y=0，墙高 " + WallHeight);
        report.AppendLine("- 门A: 上墙靠左 " + DoorACenter + "，宽 " + DoorWidth + "，高 " + DoorHeight);
        report.AppendLine("- 门B: 下墙靠右 " + DoorBCenter + "，宽 " + DoorWidth);
        report.AppendLine("- 篾竹区: 中心 " + BambooAreaCenter + "，范围 " + BambooAreaSize);
        report.AppendLine("- 编帘区: 中心 " + WeavingAreaCenter + "，范围 " + WeavingAreaSize);
        report.AppendLine("- 漆帘区: 中心 " + LacquerAreaCenter + "，范围 " + LacquerAreaSize);
        report.AppendLine("- 电脑区: 中心 " + ComputerAreaCenter + "，范围 " + ComputerAreaSize);
        report.AppendLine("- 玩家出生点: " + PlayerSpawn + "（门A内侧）");
        report.AppendLine("- 南墙默认低墙 " + SouthLowWallHeight + "，完整南墙默认隐藏");
        return report.ToString().TrimEnd();
    }

    static string CheckSpawn()
    {
        bool overlap = Physics.CheckCapsule(PlayerSpawn + Vector3.up * 0.45f, PlayerSpawn + Vector3.up * 1.5f, 0.4f, ~0, QueryTriggerInteraction.Ignore);
        bool ground = Physics.Raycast(PlayerSpawn + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit, 2.2f);
        return "- 出生点地面: " + (ground ? hit.collider.name + " Y=" + hit.point.y.ToString("0.###") : "无") + "，胶囊重叠: " + overlap;
    }

    static string CheckDoor(string label, Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        bool blocked = Physics.CapsuleCast(from, from + Vector3.up * 1.2f, 0.4f, delta.normalized, out RaycastHit hit, delta.magnitude);
        return "- " + label + " 可通过: " + (!blocked) + (blocked ? " 阻挡=" + hit.collider.name : string.Empty);
    }

    static string CheckPath(string label, Vector3 from, Vector3 to)
    {
        int blocked = 0;
        int grounded = 0;
        int samples = 0;
        int steps = Mathf.Max(4, Mathf.RoundToInt(Vector3.Distance(from, to) / 1.6f));
        for (int i = 0; i <= steps; i++)
        {
            samples++;
            Vector3 p = Vector3.Lerp(from, to, i / (float)steps);
            p.y = 1.2f;
            if (Physics.CheckSphere(p, 0.28f, ~0, QueryTriggerInteraction.Ignore))
                blocked++;
            if (Physics.Raycast(p + Vector3.up * 1.4f, Vector3.down, out RaycastHit hit, 3f) && hit.point.y > -0.05f)
                grounded++;
        }

        return "- " + label + " 着地 " + grounded + "/" + samples + "，穿模采样 " + blocked;
    }

    static void CreateTable(Transform parent, string name, Vector3 center, float width, float depth, float topY, Material material)
    {
        Transform table = CreateGroup(parent, name);
        CreateBox(table, "Top", new Vector3(center.x, topY, center.z), new Vector3(width, 0.1f, depth), material, true);
        float leg = 0.09f;
        float insetX = width * 0.5f - 0.12f;
        float insetZ = depth * 0.5f - 0.12f;
        float legY = topY * 0.5f;
        float[] xs = { -insetX, insetX };
        float[] zs = { -insetZ, insetZ };
        int index = 0;
        foreach (float x in xs)
        {
            foreach (float z in zs)
            {
                CreateBox(table, "Leg_" + index, new Vector3(center.x + x, legY, center.z + z), new Vector3(leg, topY, leg), material, false);
                index++;
            }
        }
    }

    static GameObject CreateBox(Transform parent, string name, Vector3 center, Vector3 size, Material material, bool collider)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.position = center;
        box.transform.localScale = size;
        AssignMaterial(box, material);
        if (!collider)
            Object.DestroyImmediate(box.GetComponent<Collider>());
        return box;
    }

    static GameObject CreateCylinder(Transform parent, string name, Vector3 center, float radius, float height, Material material, bool collider)
    {
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = name;
        cylinder.transform.SetParent(parent, false);
        cylinder.transform.position = center;
        cylinder.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        AssignMaterial(cylinder, material);
        if (!collider)
            Object.DestroyImmediate(cylinder.GetComponent<Collider>());
        return cylinder;
    }

    static void CreateInteractionPoint(Transform parent, string name, Vector3 position, Vector3 look)
    {
        var marker = new GameObject(name);
        marker.transform.SetParent(parent, false);
        marker.transform.position = position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.0001f)
            marker.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        InteractionPoint point = marker.AddComponent<InteractionPoint>();
        point.SetPointId(name);
    }

    static void CreateMarker(Transform parent, string name, Vector3 position)
    {
        var marker = new GameObject(name);
        marker.transform.SetParent(parent, false);
        marker.transform.position = position;
    }

    static Transform CreateGroup(Transform parent, string name)
    {
        var group = new GameObject(name);
        group.transform.SetParent(parent, false);
        group.transform.localPosition = Vector3.zero;
        group.transform.localRotation = Quaternion.identity;
        return group.transform;
    }

    static void AssignMaterial(GameObject target, Material material)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
    }

    static Materials CreateMaterials()
    {
        return new Materials
        {
            Floor = GetOrCreateLitMaterial(MaterialFolder + "/WorkshopInteriorFloor.mat", new Color(0.78f, 0.78f, 0.76f)),
            Wall = GetOrCreateLitMaterial(MaterialFolder + "/WorkshopInteriorWall.mat", new Color(0.88f, 0.88f, 0.86f)),
            Bamboo = GetOrCreateLitMaterial(MaterialFolder + "/WorkshopInteriorBamboo.mat", new Color(0.86f, 0.80f, 0.68f)),
            Weaving = GetOrCreateLitMaterial(MaterialFolder + "/WorkshopInteriorWeaving.mat", new Color(0.72f, 0.78f, 0.70f)),
            Lacquer = GetOrCreateLitMaterial(MaterialFolder + "/WorkshopInteriorLacquer.mat", new Color(0.76f, 0.64f, 0.52f)),
            Computer = GetOrCreateLitMaterial(MaterialFolder + "/WorkshopInteriorComputer.mat", new Color(0.70f, 0.76f, 0.82f)),
            Device = GetOrCreateLitMaterial(MaterialFolder + "/WorkshopInteriorDevice.mat", new Color(0.62f, 0.62f, 0.62f)),
            Wood = GetOrCreateLitMaterial(MaterialFolder + "/WorkshopInteriorWood.mat", new Color(0.70f, 0.60f, 0.48f)),
            Player = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/WhiteboxPlayer.mat")
                     ?? GetOrCreateLitMaterial(MaterialFolder + "/WhiteboxPlayer.mat", new Color(0.78f, 0.78f, 0.82f))
        };
    }

    static Material GetOrCreateLitMaterial(string assetPath, Color color)
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(LitShaderPath);
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            throw new InvalidOperationException("找不到 URP Lit shader。");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (material == null)
        {
            material = new Material(shader);
            ApplyFlatColor(material, color);
            AssetDatabase.CreateAsset(material, assetPath);
        }
        else
        {
            if (material.shader != shader)
                material.shader = shader;
            ApplyFlatColor(material, color);
            EditorUtility.SetDirty(material);
        }

        return material;
    }

    static void ApplyFlatColor(Material material, Color color)
    {
        material.color = color;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.08f);
    }

    static void EnsureUrpComponent(GameObject target, string fullName)
    {
        Type type = FindType(fullName);
        if (type == null || target.GetComponent(type) != null)
            return;
        target.AddComponent(type);
    }

    static Type FindType(string fullName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName);
            if (type != null)
                return type;
        }

        return null;
    }

    static void DestroyComponentIfPresent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component != null)
            Object.DestroyImmediate(component);
    }

    static bool LegacyGroundActive()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == "Ground")
                return root.activeSelf;
        }

        return false;
    }

    static GameObject FindRoot(string name)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == name)
                return root;
        }

        return null;
    }

    static int CountRoots(string name)
    {
        int count = 0;
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == name)
                count++;
        }

        return count;
    }

    static void EnsureFolder(string assetPath)
    {
        assetPath = assetPath.Replace('\\', '/');
        if (AssetDatabase.IsValidFolder(assetPath))
            return;

        string parent = Path.GetDirectoryName(assetPath);
        if (string.IsNullOrEmpty(parent))
            return;

        parent = parent.Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(assetPath));
    }
}
