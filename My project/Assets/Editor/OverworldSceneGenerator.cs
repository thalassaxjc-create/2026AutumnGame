using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class OverworldSceneGenerator
{
    public const string ScenePath = "Assets/Scenes/Level_Overworld_01.unity";

    private const string PlayerPrefabPath = "Assets/_Game/Prefabs/Player.prefab";
    private const string GeneratedRoot = "Assets/_Game/Overworld";
    private const string MaterialRoot = GeneratedRoot + "/Materials";
    private const string PrefabRoot = GeneratedRoot + "/Prefabs";
    private const string BambooPrefabPath = PrefabRoot + "/BambooStalk.prefab";

    private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

    [MenuItem("Tools/Overworld/Generate Level Overworld 01")]
    public static void GenerateFromMenu()
    {
        bool sceneExists = File.Exists(ScenePath);
        if (sceneExists && !EditorUtility.DisplayDialog(
                "Replace generated overworld?",
                "Level_Overworld_01 already exists. Replace only this generated scene? Existing generated materials and prefabs will be reused without modification.",
                "Replace Scene",
                "Cancel"))
        {
            return;
        }

        GenerateScene();
    }

    public static void GenerateFromCommandLine()
    {
        try
        {
            GenerateScene();
            Debug.Log("Overworld generation and validation completed successfully: " + ScenePath);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
            return;
        }

        EditorApplication.Exit(0);
    }

    private static void GenerateScene()
    {
        EnsureFolder("Assets/_Game", "Overworld");
        EnsureFolder(GeneratedRoot, "Materials");
        EnsureFolder(GeneratedRoot, "Prefabs");
        EnsureMaterials();
        GameObject bambooPrefab = EnsureBambooPrefab();

        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (playerPrefab == null || playerPrefab.GetComponent<PlayerController>() == null)
        {
            throw new InvalidOperationException(
                "Expected the existing Player prefab with PlayerController at " + PlayerPrefabPath);
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "Level_Overworld_01";

        GameObject sceneRoot = CreateEmpty("Level_Overworld_01", null);
        Transform environment = CreateEmpty("Environment", sceneRoot.transform).transform;
        Transform gameplay = CreateEmpty("Gameplay", sceneRoot.transform).transform;
        Transform lighting = CreateEmpty("Lighting", sceneRoot.transform).transform;
        Transform cameras = CreateEmpty("Cameras", sceneRoot.transform).transform;
        CreateEmpty("Managers", sceneRoot.transform);

        Transform ground = CreateEmpty("Ground", environment).transform;
        Transform paths = CreateEmpty("Paths", environment).transform;
        Transform bambooGrove = CreateEmpty("BambooGrove", environment).transform;
        Transform workshop = CreateEmpty("Workshop", environment).transform;
        Transform entrance = CreateEmpty("LevelEntrance", environment).transform;

        BuildGround(ground);
        BuildPaths(paths);
        BuildSpawnArea(paths);
        BuildBambooGrove(bambooGrove, bambooPrefab, scene);
        BuildWorkshop(workshop);
        BuildLevelEntrance(entrance);

        Transform spawn = CreateEmpty("PlayerSpawn", gameplay).transform;
        spawn.position = new Vector3(0f, 0.6f, -11.2f);
        GameObject spawnMarker = CreatePrimitive(
            "PlayerSpawnMarker",
            PrimitiveType.Cylinder,
            spawn,
            new Vector3(0f, -0.55f, 0f),
            new Vector3(1.3f, 0.025f, 1.3f),
            Materials["Important"],
            false);
        spawnMarker.transform.localPosition = new Vector3(0f, -0.55f, 0f);

        GameObject player = PrefabUtility.InstantiatePrefab(playerPrefab, scene) as GameObject;
        if (player == null)
        {
            throw new InvalidOperationException("Could not instantiate the existing player prefab.");
        }

        player.name = "Player";
        player.transform.SetParent(gameplay, false);
        player.transform.position = spawn.position;
        player.transform.rotation = Quaternion.identity;
        // The existing controller's ground-check dimensions are authored for the
        // 0.5-scale player instances used by the project's existing scenes.
        player.transform.localScale = Vector3.one * 0.5f;

        Light sun = BuildLighting(lighting);
        BuildCamera(cameras, player.transform);
        BuildTrigger(gameplay, sun);

        ConfigureSceneRenderSettings(sun);
        ValidateGeneratedScene(scene, player, sun);

        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            throw new IOException("Unity could not save the generated scene at " + ScenePath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeGameObject = sceneRoot;
        Debug.Log("Generated overworld whitebox: " + ScenePath);
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static void EnsureMaterials()
    {
        Materials.Clear();
        Materials["Ground"] = GetOrCreateMaterial("Ground", new Color(0.37f, 0.45f, 0.31f));
        Materials["Path"] = GetOrCreateMaterial("Path", new Color(0.70f, 0.58f, 0.40f));
        Materials["Bamboo"] = GetOrCreateMaterial("Bamboo", new Color(0.22f, 0.55f, 0.23f));
        Materials["BambooDark"] = GetOrCreateMaterial("BambooDark", new Color(0.12f, 0.34f, 0.13f));
        Materials["Wood"] = GetOrCreateMaterial("Wood", new Color(0.43f, 0.25f, 0.12f));
        Materials["WoodLight"] = GetOrCreateMaterial("WoodLight", new Color(0.66f, 0.43f, 0.21f));
        Materials["Paper"] = GetOrCreateMaterial("Paper", new Color(0.91f, 0.88f, 0.73f));
        Materials["Stone"] = GetOrCreateMaterial("Stone", new Color(0.38f, 0.40f, 0.43f));
        Materials["Important"] = GetOrCreateMaterial("Important", new Color(0.95f, 0.49f, 0.12f));
        Materials["Entrance"] = GetOrCreateMaterial("Entrance", new Color(0.46f, 0.12f, 0.09f));
    }

    private static Material GetOrCreateMaterial(string name, Color color)
    {
        string path = MaterialRoot + "/Overworld_" + name + ".mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            return existing;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            throw new InvalidOperationException("No URP Lit or Standard shader is available.");
        }

        Material material = new Material(shader) { name = "Overworld_" + name };
        material.color = color;
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.08f);
        }

        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static GameObject EnsureBambooPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(BambooPrefabPath);
        if (existing != null)
        {
            return existing;
        }

        GameObject root = new GameObject("BambooStalk");
        CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0f, 0.5f, 0f);
        collider.height = 1f;
        collider.radius = 0.18f;

        CreatePrimitive(
            "Stalk",
            PrimitiveType.Cylinder,
            root.transform,
            new Vector3(0f, 0.5f, 0f),
            new Vector3(0.28f, 0.5f, 0.28f),
            Materials["Bamboo"],
            false);

        for (int i = 1; i <= 3; i++)
        {
            CreatePrimitive(
                "Node_" + i,
                PrimitiveType.Cylinder,
                root.transform,
                new Vector3(0f, i * 0.25f, 0f),
                new Vector3(0.32f, 0.018f, 0.32f),
                Materials["BambooDark"],
                false);
        }

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, BambooPrefabPath);
        Object.DestroyImmediate(root);
        if (saved == null)
        {
            throw new IOException("Could not create bamboo prefab at " + BambooPrefabPath);
        }

        return saved;
    }

    private static void BuildGround(Transform parent)
    {
        CreatePrimitive(
            "Ground_Base",
            PrimitiveType.Cube,
            parent,
            new Vector3(0f, -0.3f, 0f),
            new Vector3(40f, 0.6f, 30f),
            Materials["Ground"]);

        CreatePrimitive("Boundary_West", PrimitiveType.Cube, parent, new Vector3(-20.25f, 0.75f, 0f), new Vector3(0.5f, 1.5f, 30f), Materials["Wood"]);
        CreatePrimitive("Boundary_East", PrimitiveType.Cube, parent, new Vector3(20.25f, 0.75f, 0f), new Vector3(0.5f, 1.5f, 30f), Materials["Wood"]);
        CreatePrimitive("Boundary_South", PrimitiveType.Cube, parent, new Vector3(0f, 0.75f, -15.25f), new Vector3(40f, 1.5f, 0.5f), Materials["Wood"]);
        CreatePrimitive("Boundary_North_Left", PrimitiveType.Cube, parent, new Vector3(-6.5f, 0.75f, 15.25f), new Vector3(27f, 1.5f, 0.5f), Materials["Wood"]);
        CreatePrimitive("Boundary_North_Right", PrimitiveType.Cube, parent, new Vector3(16f, 0.75f, 15.25f), new Vector3(8f, 1.5f, 0.5f), Materials["Wood"]);
    }

    private static void BuildPaths(Transform parent)
    {
        CreatePath("Spawn_Plaza", parent, new Vector3(0f, -11.2f), new Vector2(7f, 4.8f));
        CreatePath("Spawn_To_Junction", parent, new Vector3(0f, -7.2f), new Vector2(3.2f, 6.5f));
        CreatePath("Central_Junction", parent, new Vector3(0f, -3.7f), new Vector2(5.5f, 4f));
        CreatePath("Grove_Approach", parent, new Vector3(-4.5f, -3.2f), new Vector2(7f, 2.8f));
        CreatePath("Grove_Path", parent, new Vector3(-9.5f, 1.8f), new Vector2(2.8f, 11.5f));
        CreatePath("Workshop_Approach", parent, new Vector3(5.2f, -3.2f), new Vector2(8.2f, 3f));
        CreatePath("Workshop_Courtyard", parent, new Vector3(9f, 1.5f), new Vector2(11.5f, 8f));
        CreatePath("Entrance_Path", parent, new Vector3(9f, 8.6f), new Vector2(3.2f, 8.5f));
        CreatePath("Entrance_Plaza", parent, new Vector3(9f, 12.2f), new Vector2(7f, 4.3f));
    }

    private static void CreatePath(string name, Transform parent, Vector3 xz, Vector2 size)
    {
        CreatePrimitive(
            name,
            PrimitiveType.Cube,
            parent,
            new Vector3(xz.x, 0.025f, xz.y),
            new Vector3(size.x, 0.05f, size.y),
            Materials["Path"]);
    }

    private static void BuildSpawnArea(Transform parent)
    {
        Transform landmark = CreateEmpty("SpawnLandmark", parent).transform;
        CreatePrimitive("Marker_Post", PrimitiveType.Cylinder, landmark, new Vector3(-2.2f, 1.3f, -11.3f), new Vector3(0.28f, 1.3f, 0.28f), Materials["Wood"]);
        CreatePrimitive("Marker_Top", PrimitiveType.Cube, landmark, new Vector3(-2.2f, 2.75f, -11.3f), new Vector3(2.1f, 0.32f, 0.4f), Materials["Important"]);
        CreatePrimitive("Marker_Arrow", PrimitiveType.Cube, landmark, new Vector3(-1.25f, 2.75f, -11.3f), new Vector3(0.65f, 0.7f, 0.28f), Materials["Important"], false, Quaternion.Euler(0f, 0f, 45f));
    }

    private static void BuildBambooGrove(Transform parent, GameObject bambooPrefab, Scene scene)
    {
        Transform clusters = CreateEmpty("BambooClusters", parent).transform;
        System.Random random = new System.Random(2701);
        int created = 0;

        while (created < 34)
        {
            float x = Mathf.Lerp(-17.5f, -3.2f, (float)random.NextDouble());
            float z = Mathf.Lerp(-4.8f, 8.5f, (float)random.NextDouble());
            bool blocksGrovePath = Mathf.Abs(x + 9.5f) < 1.9f;
            bool blocksApproach = z < -1.8f && x > -8.2f;
            if (blocksGrovePath || blocksApproach)
            {
                continue;
            }

            GameObject stalk = PrefabUtility.InstantiatePrefab(bambooPrefab, scene) as GameObject;
            if (stalk == null)
            {
                throw new InvalidOperationException("Could not instantiate BambooStalk prefab.");
            }

            stalk.name = "BambooStalk_" + (created + 1).ToString("00");
            stalk.transform.SetParent(clusters, false);
            stalk.transform.position = new Vector3(x, 0.05f, z);
            float height = Mathf.Lerp(4.2f, 7.5f, (float)random.NextDouble());
            float width = Mathf.Lerp(0.78f, 1.18f, (float)random.NextDouble());
            stalk.transform.localScale = new Vector3(width, height, width);
            stalk.transform.rotation = Quaternion.Euler(
                Mathf.Lerp(-2f, 2f, (float)random.NextDouble()),
                Mathf.Lerp(0f, 360f, (float)random.NextDouble()),
                Mathf.Lerp(-2f, 2f, (float)random.NextDouble()));
            stalk.isStatic = true;
            created++;
        }

        BuildGroveSign(parent);
    }

    private static void BuildGroveSign(Transform parent)
    {
        Transform sign = CreateEmpty("BambooGroveLandmark", parent).transform;
        CreatePrimitive("Post_Left", PrimitiveType.Cylinder, sign, new Vector3(-11.1f, 1.1f, -3.7f), new Vector3(0.18f, 1.1f, 0.18f), Materials["Wood"]);
        CreatePrimitive("Post_Right", PrimitiveType.Cylinder, sign, new Vector3(-7.9f, 1.1f, -3.7f), new Vector3(0.18f, 1.1f, 0.18f), Materials["Wood"]);
        CreatePrimitive("Header", PrimitiveType.Cube, sign, new Vector3(-9.5f, 2.25f, -3.7f), new Vector3(3.8f, 0.35f, 0.45f), Materials["BambooDark"]);
    }

    private static void BuildWorkshop(Transform parent)
    {
        Transform structure = CreateEmpty("OpenWorkshopStructure", parent).transform;
        CreatePrimitive("WorkshopPlatform", PrimitiveType.Cube, structure, new Vector3(9f, 0.12f, 1.5f), new Vector3(12f, 0.24f, 9f), Materials["WoodLight"]);

        Vector3[] posts =
        {
            new Vector3(3.5f, 2.5f, -2.4f),
            new Vector3(14.5f, 2.5f, -2.4f),
            new Vector3(3.5f, 2.5f, 5.4f),
            new Vector3(14.5f, 2.5f, 5.4f)
        };
        for (int i = 0; i < posts.Length; i++)
        {
            CreatePrimitive("RoofPost_" + (i + 1), PrimitiveType.Cube, structure, posts[i], new Vector3(0.35f, 5f, 0.35f), Materials["Wood"]);
        }

        CreatePrimitive("RoofBeam_Front", PrimitiveType.Cube, structure, new Vector3(9f, 4.9f, -2.4f), new Vector3(12f, 0.35f, 0.4f), Materials["Wood"]);
        CreatePrimitive("RoofBeam_Back", PrimitiveType.Cube, structure, new Vector3(9f, 4.9f, 5.4f), new Vector3(12f, 0.35f, 0.4f), Materials["Wood"]);
        CreatePrimitive("RoofBeam_Left", PrimitiveType.Cube, structure, new Vector3(3.5f, 4.9f, 1.5f), new Vector3(0.4f, 0.35f, 8.2f), Materials["Wood"]);
        CreatePrimitive("RoofBeam_Right", PrimitiveType.Cube, structure, new Vector3(14.5f, 4.9f, 1.5f), new Vector3(0.4f, 0.35f, 8.2f), Materials["Wood"]);

        BuildWorkbench(parent);
        BuildScreenFrame(parent);
        BuildBambooStorage(parent);
        BuildDryingRacks(parent);
    }

    private static void BuildWorkbench(Transform parent)
    {
        Transform bench = CreateEmpty("PaperScreen_Workbench", parent).transform;
        CreatePrimitive("Top", PrimitiveType.Cube, bench, new Vector3(7.1f, 1.25f, -0.2f), new Vector3(5.2f, 0.35f, 2.3f), Materials["WoodLight"]);
        Vector3[] legs =
        {
            new Vector3(5f, 0.62f, -1f),
            new Vector3(9.2f, 0.62f, -1f),
            new Vector3(5f, 0.62f, 0.6f),
            new Vector3(9.2f, 0.62f, 0.6f)
        };
        for (int i = 0; i < legs.Length; i++)
        {
            CreatePrimitive("Leg_" + (i + 1), PrimitiveType.Cube, bench, legs[i], new Vector3(0.35f, 1.25f, 0.35f), Materials["Wood"]);
        }

        CreatePrimitive("PaperPulpSheet", PrimitiveType.Cube, bench, new Vector3(7.1f, 1.46f, -0.2f), new Vector3(3.8f, 0.06f, 1.45f), Materials["Paper"], false);
    }

    private static void BuildScreenFrame(Transform parent)
    {
        Transform frame = CreateEmpty("Rectangular_BambooScreenFrame", parent).transform;
        const float centerX = 11.7f;
        const float centerY = 2.25f;
        const float z = -0.2f;
        CreatePrimitive("Frame_Left", PrimitiveType.Cube, frame, new Vector3(centerX - 2.1f, centerY, z), new Vector3(0.22f, 3.2f, 0.28f), Materials["BambooDark"]);
        CreatePrimitive("Frame_Right", PrimitiveType.Cube, frame, new Vector3(centerX + 2.1f, centerY, z), new Vector3(0.22f, 3.2f, 0.28f), Materials["BambooDark"]);
        CreatePrimitive("Frame_Top", PrimitiveType.Cube, frame, new Vector3(centerX, centerY + 1.6f, z), new Vector3(4.4f, 0.22f, 0.28f), Materials["BambooDark"]);
        CreatePrimitive("Frame_Bottom", PrimitiveType.Cube, frame, new Vector3(centerX, centerY - 1.6f, z), new Vector3(4.4f, 0.22f, 0.28f), Materials["BambooDark"]);
        for (int i = 0; i < 13; i++)
        {
            float x = centerX - 1.8f + i * 0.3f;
            CreatePrimitive("ScreenSlat_" + (i + 1).ToString("00"), PrimitiveType.Cube, frame, new Vector3(x, centerY, z), new Vector3(0.055f, 2.9f, 0.08f), Materials["Bamboo"], false);
        }
    }

    private static void BuildBambooStorage(Transform parent)
    {
        Transform storage = CreateEmpty("BambooMaterialStorage", parent).transform;
        CreatePrimitive("Rack_Base", PrimitiveType.Cube, storage, new Vector3(12.4f, 0.45f, 3.6f), new Vector3(4.5f, 0.35f, 1.8f), Materials["Wood"]);
        for (int i = 0; i < 9; i++)
        {
            float y = 0.8f + (i / 3) * 0.32f;
            float z = 3.15f + (i % 3) * 0.42f;
            CreatePrimitive(
                "StoredBamboo_" + (i + 1).ToString("00"),
                PrimitiveType.Cylinder,
                storage,
                new Vector3(12.4f, y, z),
                new Vector3(0.13f, 1.7f, 0.13f),
                Materials["Bamboo"],
                true,
                Quaternion.Euler(0f, 0f, 90f));
        }
    }

    private static void BuildDryingRacks(Transform parent)
    {
        Transform racks = CreateEmpty("PaperDryingRacks", parent).transform;
        for (int rackIndex = 0; rackIndex < 2; rackIndex++)
        {
            float x = 5.2f + rackIndex * 3.1f;
            float z = 3.6f;
            Transform rack = CreateEmpty("DryingRack_" + (rackIndex + 1), racks).transform;
            CreatePrimitive("Post_Left", PrimitiveType.Cube, rack, new Vector3(x - 1.1f, 1.6f, z), new Vector3(0.22f, 3.2f, 0.22f), Materials["Wood"]);
            CreatePrimitive("Post_Right", PrimitiveType.Cube, rack, new Vector3(x + 1.1f, 1.6f, z), new Vector3(0.22f, 3.2f, 0.22f), Materials["Wood"]);
            CreatePrimitive("TopBeam", PrimitiveType.Cube, rack, new Vector3(x, 3.15f, z), new Vector3(2.5f, 0.22f, 0.22f), Materials["Wood"]);
            CreatePrimitive("DryingPaper", PrimitiveType.Cube, rack, new Vector3(x, 2.05f, z), new Vector3(1.85f, 1.7f, 0.06f), Materials["Paper"], false);
        }
    }

    private static void BuildLevelEntrance(Transform parent)
    {
        Transform gate = CreateEmpty("CeremonialLevelGate", parent).transform;
        CreatePrimitive("StoneStep", PrimitiveType.Cube, gate, new Vector3(9f, 0.25f, 12.2f), new Vector3(6.4f, 0.5f, 4f), Materials["Stone"]);
        CreatePrimitive("GatePost_Left", PrimitiveType.Cube, gate, new Vector3(6.8f, 2.6f, 12.8f), new Vector3(0.65f, 5.2f, 0.65f), Materials["Entrance"]);
        CreatePrimitive("GatePost_Right", PrimitiveType.Cube, gate, new Vector3(11.2f, 2.6f, 12.8f), new Vector3(0.65f, 5.2f, 0.65f), Materials["Entrance"]);
        CreatePrimitive("GateBeam_Main", PrimitiveType.Cube, gate, new Vector3(9f, 5.15f, 12.8f), new Vector3(6.3f, 0.6f, 0.8f), Materials["Entrance"]);
        CreatePrimitive("GateBeam_Upper", PrimitiveType.Cube, gate, new Vector3(9f, 5.85f, 12.8f), new Vector3(7.3f, 0.32f, 1f), Materials["Wood"]);
        CreatePrimitive("DestinationMarker", PrimitiveType.Cylinder, gate, new Vector3(9f, 0.55f, 11.7f), new Vector3(1.5f, 0.08f, 1.5f), Materials["Important"], false);
        CreatePrimitive("Lantern_Left", PrimitiveType.Cube, gate, new Vector3(7.55f, 3.75f, 12.35f), new Vector3(0.5f, 0.75f, 0.5f), Materials["Important"], false);
        CreatePrimitive("Lantern_Right", PrimitiveType.Cube, gate, new Vector3(10.45f, 3.75f, 12.35f), new Vector3(0.5f, 0.75f, 0.5f), Materials["Important"], false);

        Transform futureAnchor = CreateEmpty("FutureGameplayLevelAnchor", parent).transform;
        futureAnchor.position = new Vector3(9f, 0.6f, 14.2f);
    }

    private static Light BuildLighting(Transform parent)
    {
        GameObject lightObject = CreateEmpty("Primary Directional Light", parent);
        lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.93f, 0.81f);
        light.intensity = 1.25f;
        light.shadows = LightShadows.Soft;
        return light;
    }

    private static void BuildCamera(Transform parent, Transform target)
    {
        GameObject cameraObject = CreateEmpty("Main Camera", parent);
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 62f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 150f;
        camera.clearFlags = CameraClearFlags.Skybox;
        cameraObject.AddComponent<AudioListener>();
        OverworldCameraController controller = cameraObject.AddComponent<OverworldCameraController>();
        controller.Configure(target);
    }

    private static void BuildTrigger(Transform parent, Light sun)
    {
        GameObject triggerObject = CreateEmpty("LevelEntryTrigger", parent);
        triggerObject.transform.position = new Vector3(9f, 1.65f, 11.55f);
        BoxCollider collider = triggerObject.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(4.2f, 3.2f, 2.8f);
        Rigidbody body = triggerObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        LevelEntryTrigger trigger = triggerObject.AddComponent<LevelEntryTrigger>();
        trigger.Configure(sun, 1.75f, 0.22f);
    }

    private static void ConfigureSceneRenderSettings(Light sun)
    {
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.48f, 0.51f, 0.45f, 1f);
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.fog = false;
    }

    private static void ValidateGeneratedScene(Scene scene, GameObject player, Light sun)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            throw new InvalidOperationException("Generated scene is not valid and loaded.");
        }

        if (player.GetComponent<Rigidbody>() == null || player.GetComponent<Collider>() == null || player.GetComponent<PlayerController>() == null)
        {
            throw new InvalidOperationException("Generated player is missing its existing 3D movement or physics components.");
        }

        LevelEntryTrigger trigger = Object.FindFirstObjectByType<LevelEntryTrigger>();
        if (trigger == null || !trigger.GetComponent<BoxCollider>().isTrigger || !trigger.GetComponent<Rigidbody>().isKinematic)
        {
            throw new InvalidOperationException("Level entry trigger is not configured for 3D physics.");
        }

        if (sun == null || Object.FindFirstObjectByType<OverworldCameraController>() == null)
        {
            throw new InvalidOperationException("Generated lighting or camera setup is incomplete.");
        }

        Transform root = scene.GetRootGameObjects()[0].transform;
        string[] requiredPaths =
        {
            "Environment/Ground",
            "Environment/Paths",
            "Environment/BambooGrove",
            "Environment/Workshop",
            "Environment/LevelEntrance",
            "Gameplay/PlayerSpawn",
            "Gameplay/Player",
            "Gameplay/LevelEntryTrigger",
            "Lighting",
            "Cameras",
            "Managers"
        };

        foreach (string requiredPath in requiredPaths)
        {
            if (root.Find(requiredPath) == null)
            {
                throw new InvalidOperationException("Generated hierarchy is missing " + requiredPath);
            }
        }
    }

    private static GameObject CreateEmpty(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name);
        if (parent != null)
        {
            gameObject.transform.SetParent(parent, false);
        }

        return gameObject;
    }

    private static GameObject CreatePrimitive(
        string name,
        PrimitiveType primitiveType,
        Transform parent,
        Vector3 position,
        Vector3 scale,
        Material material,
        bool keepCollider = true,
        Quaternion? rotation = null)
    {
        GameObject gameObject = GameObject.CreatePrimitive(primitiveType);
        gameObject.name = name;
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.position = position;
        gameObject.transform.rotation = rotation ?? Quaternion.identity;
        gameObject.transform.localScale = scale;

        Renderer renderer = gameObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }

        Collider collider = gameObject.GetComponent<Collider>();
        if (!keepCollider && collider != null)
        {
            Object.DestroyImmediate(collider);
        }

        if (keepCollider)
        {
            gameObject.isStatic = true;
        }

        return gameObject;
    }
}
