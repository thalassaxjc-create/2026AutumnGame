using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 家里室内白盒：客厅、厨房、卧室。西门对应工坊路，东门对应村口路。
/// </summary>
public static class HomeWhiteboxBuilder
{
    const string HomeScenePath = "Assets/Scenes/HomeScene.unity";
    const string WorkshopScenePath = "Assets/Scenes/WorkshopScene.unity";
    const string RootName = "HomeRoot";
    const string LitShaderPath = "Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader";
    const string MaterialFolder = "Assets/Materials";

    const float WallHeight = 3.5f;
    const float WallThickness = 0.3f;
    const float DoorWidth = 2.8f;
    const float DoorHeight = 3f;
    const float SouthLowWallHeight = 1f;

    static readonly Vector3 LivingCenter = Vector3.zero;
    static readonly Vector3 LivingSize = new Vector3(18f, 0f, 14f);
    static readonly Vector3 KitchenCenter = new Vector3(0f, 0f, 11f);
    static readonly Vector3 KitchenSize = new Vector3(12f, 0f, 8f);
    static readonly Vector3 BedroomCenter = new Vector3(-2f, 0f, -12.5f);
    static readonly Vector3 BedroomSize = new Vector3(10f, 0f, 11f);
    static readonly Vector3 HomeDoorCenter = new Vector3(-LivingSize.x * 0.5f, 0f, 0f);
    static readonly Vector3 HomeDoorEastCenter = new Vector3(LivingSize.x * 0.5f, 0f, 0f);
    static readonly Vector3 PlayerSpawn = new Vector3(-6.2f, 1f, 0f);
    static readonly Vector3 PlayerSpawnEast = new Vector3(6.2f, 1f, 0f);

    static Materials Palette;

    struct Materials
    {
        public Material Floor;
        public Material Living;
        public Material Kitchen;
        public Material Bedroom;
        public Material Wall;
        public Material Wood;
        public Material Fabric;
        public Material Player;
    }

    [MenuItem("宣纸帘/生成家里室内白盒")]
    public static void BuildMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("请先退出 Play Mode，再生成家里室内。");
            return;
        }

        Debug.Log(BuildHome());
    }

    public static string BuildHome()
    {
        if (!EnsureHomeScene())
            return "已取消：无法创建或打开 HomeScene。";

        EnsureFolder(MaterialFolder);
        Palette = CreateMaterials();
        DisableLegacyGround();
        GameObject root = RebuildHomeRoot();

        BuildFloors(CreateGroup(root.transform, "Floors"));
        BuildWalls(CreateGroup(root.transform, "Walls"));
        BuildDoors(CreateGroup(root.transform, "Doors"));
        BuildLivingRoom(CreateGroup(root.transform, "LivingRoom"));
        BuildKitchen(CreateGroup(root.transform, "Kitchen"));
        BuildBedroom(CreateGroup(root.transform, "Bedroom"));
        BuildSpawnPoints(CreateGroup(root.transform, "SpawnPoints"));

        GameObject player = EnsurePlayer();
        GameObject cameraGo = EnsureCamera(player);
        EnsureDirectionalLight();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), HomeScenePath);
        AddToBuildSettings();
        AssetDatabase.SaveAssets();

        var report = new StringBuilder();
        report.AppendLine("家里室内白盒已生成并保存 HomeScene。");
        report.AppendLine("- 客厅 " + LivingCenter + " 尺寸 " + LivingSize + "，西门 " + HomeDoorCenter + "，东门 " + HomeDoorEastCenter);
        report.AppendLine("- 厨房 " + KitchenCenter + "，卧室 " + BedroomCenter);
        report.AppendLine("- 玩家出生点: " + PlayerSpawn);
        report.AppendLine("- HomeDoor: " + (GameObject.Find("HomeDoor") != null));
        report.AppendLine("- HomeDoorEast: " + (GameObject.Find("HomeDoorEast") != null));
        report.AppendLine("- 摄像机: " + cameraGo.transform.position);
        return report.ToString();
    }

    static bool EnsureHomeScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(HomeScenePath) == null)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(WorkshopScenePath) == null)
            {
                Debug.LogError("找不到 WorkshopScene，无法复制出 HomeScene。");
                return false;
            }

            EditorSceneManager.SaveOpenScenes();
            if (!AssetDatabase.CopyAsset(WorkshopScenePath, HomeScenePath))
            {
                Debug.LogError("复制 HomeScene 失败。");
                return false;
            }

            AssetDatabase.Refresh();
        }

        EditorSceneManager.SaveOpenScenes();
        EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Single);
        return true;
    }

    static void AddToBuildSettings()
    {
        EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
        for (int i = 0; i < current.Length; i++)
        {
            if (current[i].path == HomeScenePath)
            {
                if (!current[i].enabled)
                {
                    current[i].enabled = true;
                    EditorBuildSettings.scenes = current;
                }

                return;
            }
        }

        var next = new EditorBuildSettingsScene[current.Length + 1];
        Array.Copy(current, next, current.Length);
        next[current.Length] = new EditorBuildSettingsScene(HomeScenePath, true);
        EditorBuildSettings.scenes = next;
    }

    static GameObject RebuildHomeRoot()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == RootName || root.name == "WorkshopRoot")
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

    static void BuildFloors(Transform parent)
    {
        CreateBox(parent, "LivingFloor", new Vector3(LivingCenter.x, -0.5f, LivingCenter.z), new Vector3(LivingSize.x, 1f, LivingSize.z), Palette.Floor, true);
        CreateBox(parent, "KitchenFloor", new Vector3(KitchenCenter.x, -0.5f, KitchenCenter.z), new Vector3(KitchenSize.x, 1f, KitchenSize.z), Palette.Floor, true);
        CreateBox(parent, "BedroomFloor", new Vector3(BedroomCenter.x, -0.5f, BedroomCenter.z), new Vector3(BedroomSize.x, 1f, BedroomSize.z), Palette.Floor, true);
        CreateBox(parent, "LivingMat", new Vector3(0f, 0.02f, 0f), new Vector3(LivingSize.x - 0.6f, 0.04f, LivingSize.z - 0.6f), Palette.Living, false);
        CreateBox(parent, "KitchenMat", new Vector3(KitchenCenter.x, 0.02f, KitchenCenter.z), new Vector3(KitchenSize.x - 0.5f, 0.04f, KitchenSize.z - 0.5f), Palette.Kitchen, false);
        CreateBox(parent, "BedroomMat", new Vector3(BedroomCenter.x, 0.02f, BedroomCenter.z), new Vector3(BedroomSize.x - 0.5f, 0.04f, BedroomSize.z - 0.5f), Palette.Bedroom, false);
    }

    static void BuildWalls(Transform parent)
    {
        float wallY = WallHeight * 0.5f;
        float livingHalfX = LivingSize.x * 0.5f;
        float livingHalfZ = LivingSize.z * 0.5f;
        float kitchenHalfX = KitchenSize.x * 0.5f;
        float kitchenHalfZ = KitchenSize.z * 0.5f;
        float bedHalfX = BedroomSize.x * 0.5f;
        float bedHalfZ = BedroomSize.z * 0.5f;

        float livingWest = LivingCenter.x - livingHalfX + WallThickness * 0.5f;
        float livingEast = LivingCenter.x + livingHalfX - WallThickness * 0.5f;
        float livingNorth = LivingCenter.z + livingHalfZ - WallThickness * 0.5f;
        float livingSouth = LivingCenter.z - livingHalfZ + WallThickness * 0.5f;

        CreateSplitWall(parent, "LivingWest", new Vector3(livingWest, wallY, 0f), LivingSize.z, WallHeight, 0f, DoorWidth, DoorHeight, true, true, Palette.Wall);
        CreateSplitWall(parent, "LivingEast", new Vector3(livingEast, wallY, 0f), LivingSize.z, WallHeight, 0f, DoorWidth, DoorHeight, true, true, Palette.Wall);
        CreateSplitWall(parent, "LivingNorth", new Vector3(0f, wallY, livingNorth), LivingSize.x, WallHeight, KitchenCenter.x, 4f, DoorHeight, false, false, Palette.Wall);
        CreateSplitWall(parent, "LivingSouth", new Vector3(0f, wallY, livingSouth), LivingSize.x, WallHeight, BedroomCenter.x, 2.8f, DoorHeight, false, false, Palette.Wall);

        float kitchenWest = KitchenCenter.x - kitchenHalfX + WallThickness * 0.5f;
        float kitchenEast = KitchenCenter.x + kitchenHalfX - WallThickness * 0.5f;
        float kitchenNorth = KitchenCenter.z + kitchenHalfZ - WallThickness * 0.5f;
        CreateBox(parent, "KitchenWest", new Vector3(kitchenWest, wallY, KitchenCenter.z), new Vector3(WallThickness, WallHeight, KitchenSize.z), Palette.Wall, true);
        CreateBox(parent, "KitchenEast", new Vector3(kitchenEast, wallY, KitchenCenter.z), new Vector3(WallThickness, WallHeight, KitchenSize.z), Palette.Wall, true);
        CreateBox(parent, "KitchenNorth", new Vector3(KitchenCenter.x, wallY, kitchenNorth), new Vector3(KitchenSize.x, WallHeight, WallThickness), Palette.Wall, true);

        float bedWest = BedroomCenter.x - bedHalfX + WallThickness * 0.5f;
        float bedEast = BedroomCenter.x + bedHalfX - WallThickness * 0.5f;
        float bedSouth = BedroomCenter.z - bedHalfZ + WallThickness * 0.5f;
        CreateBox(parent, "BedroomWest", new Vector3(bedWest, wallY, BedroomCenter.z), new Vector3(WallThickness, WallHeight, BedroomSize.z), Palette.Wall, true);
        CreateBox(parent, "BedroomEast", new Vector3(bedEast, wallY, BedroomCenter.z), new Vector3(WallThickness, WallHeight, BedroomSize.z), Palette.Wall, true);

        Transform low = CreateGroup(parent, "BedroomSouthLow");
        CreateBox(low, "BedroomSouthLowWall", new Vector3(BedroomCenter.x, SouthLowWallHeight * 0.5f, bedSouth), new Vector3(BedroomSize.x, SouthLowWallHeight, WallThickness), Palette.Wall, true);
        Transform full = CreateGroup(parent, "BedroomSouthFull");
        CreateBox(full, "BedroomSouthFullWall", new Vector3(BedroomCenter.x, wallY, bedSouth), new Vector3(BedroomSize.x, WallHeight, WallThickness), Palette.Wall, true);
        full.gameObject.SetActive(false);
    }

    static void CreateSplitWall(Transform parent, string prefix, Vector3 center, float length, float height, float doorOffset, float doorWidth, float doorHeight, bool alongZ, bool lintel, Material material)
    {
        float leftWidth = (length * 0.5f + doorOffset) - doorWidth * 0.5f;
        float rightWidth = (length * 0.5f - doorOffset) - doorWidth * 0.5f;
        if (alongZ)
        {
            float leftCenterZ = center.z - length * 0.5f + leftWidth * 0.5f;
            float rightCenterZ = center.z + length * 0.5f - rightWidth * 0.5f;
            if (leftWidth > 0.2f)
                CreateBox(parent, prefix + "Left", new Vector3(center.x, center.y, leftCenterZ), new Vector3(WallThickness, height, leftWidth), material, true);
            if (rightWidth > 0.2f)
                CreateBox(parent, prefix + "Right", new Vector3(center.x, center.y, rightCenterZ), new Vector3(WallThickness, height, rightWidth), material, true);
            if (lintel)
            {
                float lintelHeight = Mathf.Max(0.2f, height - doorHeight);
                CreateBox(parent, prefix + "Lintel", new Vector3(center.x, doorHeight + lintelHeight * 0.5f, center.z + doorOffset), new Vector3(WallThickness, lintelHeight, doorWidth), material, true);
            }
        }
        else
        {
            float leftCenterX = center.x - length * 0.5f + leftWidth * 0.5f;
            float rightCenterX = center.x + length * 0.5f - rightWidth * 0.5f;
            if (leftWidth > 0.2f)
                CreateBox(parent, prefix + "Left", new Vector3(leftCenterX, center.y, center.z), new Vector3(leftWidth, height, WallThickness), material, true);
            if (rightWidth > 0.2f)
                CreateBox(parent, prefix + "Right", new Vector3(rightCenterX, center.y, center.z), new Vector3(rightWidth, height, WallThickness), material, true);
            if (lintel)
            {
                float lintelHeight = Mathf.Max(0.2f, height - doorHeight);
                CreateBox(parent, prefix + "Lintel", new Vector3(center.x + doorOffset, doorHeight + lintelHeight * 0.5f, center.z), new Vector3(doorWidth, lintelHeight, WallThickness), material, true);
            }
        }
    }

    static void BuildDoors(Transform parent)
    {
        Transform door = CreateGroup(parent, "HomeDoor");
        door.position = new Vector3(HomeDoorCenter.x, 0.05f, HomeDoorCenter.z);
        door.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
        CreateBox(door, "HomeDoorSill", new Vector3(0f, 0.03f, 0f), new Vector3(DoorWidth, 0.06f, 0.6f), Palette.Wood, false);

        Transform doorEast = CreateGroup(parent, "HomeDoorEast");
        doorEast.position = new Vector3(HomeDoorEastCenter.x, 0.05f, HomeDoorEastCenter.z);
        doorEast.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
        CreateBox(doorEast, "HomeDoorEastSill", new Vector3(0f, 0.03f, 0f), new Vector3(DoorWidth, 0.06f, 0.6f), Palette.Wood, false);
    }

    static void BuildLivingRoom(Transform parent)
    {
        CreateTable(parent, "DiningTable", Vector3.zero, 2.4f, 1.4f, 0.78f, Palette.Wood);
        CreateStool(parent, "StoolNorth", new Vector3(0f, 0f, 1.85f));
        CreateStool(parent, "StoolSouth", new Vector3(0f, 0f, -1.85f));
        CreateStool(parent, "StoolEast", new Vector3(1.85f, 0f, 0f));
        CreateStool(parent, "StoolWest", new Vector3(-1.85f, 0f, 0f));
        CreateBox(parent, "SideCabinet", new Vector3(6.4f, 0.55f, 5.2f), new Vector3(1.6f, 1.1f, 0.55f), Palette.Wood, true);
    }

    static void BuildKitchen(Transform parent)
    {
        Vector3 c = KitchenCenter;
        CreateBox(parent, "CounterBack", c + new Vector3(0f, 0.45f, 2.6f), new Vector3(8.5f, 0.9f, 0.7f), Palette.Wood, true);
        CreateBox(parent, "Stove", c + new Vector3(-2.2f, 0.95f, 2.55f), new Vector3(1.2f, 0.12f, 0.55f), Palette.Fabric, true);
        CreateBox(parent, "Sink", c + new Vector3(1.6f, 0.92f, 2.55f), new Vector3(1.1f, 0.16f, 0.5f), Palette.Kitchen, false);
        CreateBox(parent, "KitchenShelf", c + new Vector3(4.4f, 1.1f, 0.2f), new Vector3(0.4f, 2.2f, 2.4f), Palette.Wood, true);
        CreateBox(parent, "CrockA", c + new Vector3(-3.4f, 1.05f, 2.55f), new Vector3(0.28f, 0.32f, 0.28f), Palette.Kitchen, false);
        CreateBox(parent, "CrockB", c + new Vector3(2.6f, 1.02f, 2.55f), new Vector3(0.22f, 0.26f, 0.22f), Palette.Wood, false);
    }

    static void BuildBedroom(Transform parent)
    {
        Vector3 c = BedroomCenter;
        CreateBox(parent, "BedBase", c + new Vector3(0f, 0.28f, -2.4f), new Vector3(2.2f, 0.36f, 3.4f), Palette.Wood, true);
        CreateBox(parent, "Mattress", c + new Vector3(0f, 0.52f, -2.4f), new Vector3(2.05f, 0.18f, 3.2f), Palette.Fabric, false);
        CreateBox(parent, "Pillow", c + new Vector3(0f, 0.68f, -3.6f), new Vector3(1.1f, 0.16f, 0.5f), Palette.Bedroom, false);
        CreateBox(parent, "Wardrobe", c + new Vector3(3.4f, 1.1f, 0.4f), new Vector3(0.7f, 2.2f, 2.2f), Palette.Wood, true);
        CreateBox(parent, "Nightstand", c + new Vector3(-1.6f, 0.35f, -3.5f), new Vector3(0.6f, 0.7f, 0.5f), Palette.Wood, true);
    }

    static void BuildSpawnPoints(Transform parent)
    {
        CreateMarker(parent, "Spawn_HomeInside", PlayerSpawn + new Vector3(0f, -0.95f, 0f));
        CreateMarker(parent, "Spawn_HomeInsideEast", PlayerSpawnEast + new Vector3(0f, -0.95f, 0f));
    }

    static void CreateTable(Transform parent, string name, Vector3 center, float width, float depth, float topY, Material material)
    {
        Transform table = CreateGroup(parent, name);
        CreateBox(table, "Top", new Vector3(center.x, topY, center.z), new Vector3(width, 0.1f, depth), material, true);
        float leg = 0.09f;
        float insetX = width * 0.5f - 0.12f;
        float insetZ = depth * 0.5f - 0.12f;
        float legY = topY * 0.5f;
        CreateBox(table, "Leg_0", new Vector3(center.x - insetX, legY, center.z - insetZ), new Vector3(leg, topY, leg), material, false);
        CreateBox(table, "Leg_1", new Vector3(center.x - insetX, legY, center.z + insetZ), new Vector3(leg, topY, leg), material, false);
        CreateBox(table, "Leg_2", new Vector3(center.x + insetX, legY, center.z - insetZ), new Vector3(leg, topY, leg), material, false);
        CreateBox(table, "Leg_3", new Vector3(center.x + insetX, legY, center.z + insetZ), new Vector3(leg, topY, leg), material, false);
    }

    static void CreateStool(Transform parent, string name, Vector3 position)
    {
        CreateBox(parent, name + "Seat", position + Vector3.up * 0.42f, new Vector3(0.48f, 0.1f, 0.48f), Palette.Wood, false);
        CreateBox(parent, name + "Leg", position + Vector3.up * 0.2f, new Vector3(0.12f, 0.4f, 0.12f), Palette.Wood, false);
    }

    static GameObject EnsurePlayer()
    {
        GameObject player = FindRoot("Player");
        if (player == null)
        {
            player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
        }

        player.transform.SetPositionAndRotation(PlayerSpawn, Quaternion.LookRotation(Vector3.right, Vector3.up));
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
        return cameraGo;
    }

    static void EnsureDirectionalLight()
    {
        GameObject lightGo = FindRoot("Directional Light");
        if (lightGo == null)
            lightGo = new GameObject("Directional Light");
        Light lightComponent = lightGo.GetComponent<Light>();
        if (lightComponent == null)
            lightComponent = lightGo.AddComponent<Light>();
        lightComponent.type = LightType.Directional;
        lightComponent.color = Color.white;
        lightComponent.intensity = 2f;
        lightComponent.shadows = LightShadows.Soft;
        lightGo.transform.SetPositionAndRotation(new Vector3(0f, 3f, 0f), Quaternion.Euler(50f, -30f, 0f));
    }

    static GameObject CreateBox(Transform parent, string name, Vector3 center, Vector3 size, Material material, bool collider)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.position = center;
        box.transform.localScale = size;
        Renderer renderer = box.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
        if (!collider)
            Object.DestroyImmediate(box.GetComponent<Collider>());
        return box;
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
        return group.transform;
    }

    static Materials CreateMaterials()
    {
        return new Materials
        {
            Floor = GetOrCreateLitMaterial(MaterialFolder + "/HomeInteriorFloor.mat", new Color(0.80f, 0.78f, 0.74f)),
            Living = GetOrCreateLitMaterial(MaterialFolder + "/HomeInteriorLiving.mat", new Color(0.84f, 0.80f, 0.72f)),
            Kitchen = GetOrCreateLitMaterial(MaterialFolder + "/HomeInteriorKitchen.mat", new Color(0.78f, 0.82f, 0.76f)),
            Bedroom = GetOrCreateLitMaterial(MaterialFolder + "/HomeInteriorBedroom.mat", new Color(0.82f, 0.76f, 0.72f)),
            Wall = GetOrCreateLitMaterial(MaterialFolder + "/HomeInteriorWall.mat", new Color(0.90f, 0.88f, 0.84f)),
            Wood = GetOrCreateLitMaterial(MaterialFolder + "/HomeInteriorWood.mat", new Color(0.70f, 0.58f, 0.44f)),
            Fabric = GetOrCreateLitMaterial(MaterialFolder + "/HomeInteriorFabric.mat", new Color(0.62f, 0.48f, 0.42f)),
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

    static GameObject FindRoot(string name)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == name)
                return root;
        }

        return null;
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
