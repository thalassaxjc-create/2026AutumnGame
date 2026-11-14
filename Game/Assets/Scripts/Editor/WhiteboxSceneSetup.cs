using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 创建第一阶段白盒场景。只在菜单中执行，进入 Play Mode 时不会自动生成对象。
/// </summary>
public static class WhiteboxSceneSetup
{
    const string WorldScenePath = "Assets/Scenes/WorldScene.unity";
    const string WorkshopScenePath = "Assets/Scenes/WorkshopScene.unity";
    const string GroundMaterialPath = "Assets/Materials/WhiteboxGround.mat";
    const string PlayerMaterialPath = "Assets/Materials/WhiteboxPlayer.mat";
    const string LitShaderPath = "Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader";

    const string GroundName = "Ground";
    const string PlayerName = "Player";
    const string CameraName = "Main Camera";
    const string LightName = "Directional Light";
    const string WorkshopRootName = "WorkshopRoot";

    [MenuItem("宣纸帘/初始化第一阶段场景")]
    public static void SetupPhaseOneMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("请先退出 Play Mode，再初始化第一阶段场景。");
            return;
        }

        Debug.Log(SetupPhaseOne());
    }

    [MenuItem("宣纸帘/检查第一阶段场景")]
    public static void ValidateMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("请先退出 Play Mode，再检查场景。");
            return;
        }

        string previousPath = SceneManager.GetActiveScene().path;
        Debug.Log(ValidateSavedScenes());
        if (!string.IsNullOrEmpty(previousPath))
            EditorSceneManager.OpenScene(previousPath, OpenSceneMode.Single);
    }

    public static string SetupPhaseOne()
    {
        EditorSceneManager.SaveOpenScenes();
        EnsureFolders();

        Material groundMaterial = GetOrCreateLitMaterial(GroundMaterialPath, new Color(0.55f, 0.55f, 0.55f));
        Material playerMaterial = GetOrCreateLitMaterial(PlayerMaterialPath, new Color(0.78f, 0.78f, 0.82f));

        var report = new StringBuilder();
        report.AppendLine(SetupWorldScene(groundMaterial, playerMaterial));
        report.AppendLine(SetupWorkshopScene(groundMaterial));
        report.AppendLine(EnsureBuildSettings());

        EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        report.AppendLine("已重新打开 WorldScene。重复执行本菜单会重置白盒对象的变换和组件，不会额外复制同名对象。");
        string text = report.ToString();
        Debug.Log(text);
        return text;
    }

    public static string ValidateSavedScenes()
    {
        var report = new StringBuilder();
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(WorldScenePath) == null)
            report.AppendLine("缺少 " + WorldScenePath);
        else
        {
            EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);
            report.AppendLine(ValidateWorldScene());
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(WorkshopScenePath) == null)
            report.AppendLine("缺少 " + WorkshopScenePath);
        else
        {
            EditorSceneManager.OpenScene(WorkshopScenePath, OpenSceneMode.Single);
            report.AppendLine(ValidateWorkshopScene());
        }

        report.AppendLine(DescribeBuildSettings());
        return report.ToString();
    }

    static string SetupWorldScene(Material groundMaterial, Material playerMaterial)
    {
        OpenOrCreate(WorldScenePath);
        ApplyEnvironment();

        GameObject ground = EnsurePrimitive(GroundName, PrimitiveType.Cube);
        ConfigureGround(ground, new Vector3(40f, 1f, 40f), groundMaterial);

        GameObject player = EnsurePrimitive(PlayerName, PrimitiveType.Capsule);
        ConfigurePlayer(player, playerMaterial);

        GameObject cameraGo = EnsureCamera();
        GameObject lightGo = EnsureDirectionalLight();
        RemoveRoot(WorkshopRootName);

        ConfigureWorldCamera(cameraGo, player.transform);
        WirePlayerCamera(player, cameraGo.GetComponent<Camera>());
        EnsureSingleAudioListener(cameraGo);
        EnsureUrpComponent(cameraGo, "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData");
        EnsureUrpComponent(lightGo, "UnityEngine.Rendering.Universal.UniversalAdditionalLightData");

        RenderSettings.sun = lightGo.GetComponent<Light>();
        SaveActiveScene(WorldScenePath);
        return ValidateWorldScene();
    }

    static string SetupWorkshopScene(Material groundMaterial)
    {
        OpenOrCreate(WorkshopScenePath);
        ApplyEnvironment();

        GameObject ground = EnsurePrimitive(GroundName, PrimitiveType.Cube);
        ConfigureGround(ground, new Vector3(20f, 1f, 20f), groundMaterial);

        GameObject cameraGo = EnsureCamera();
        GameObject lightGo = EnsureDirectionalLight();
        GameObject root = EnsureRoot(WorkshopRootName);
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;

        RemoveRoot(PlayerName);
        DestroyComponentIfPresent<CameraFollow>(cameraGo);
        DestroyComponentIfPresent<PlayerController>(cameraGo);

        ConfigureOverviewCamera(cameraGo, 14f);
        EnsureSingleAudioListener(cameraGo);
        EnsureUrpComponent(cameraGo, "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData");
        EnsureUrpComponent(lightGo, "UnityEngine.Rendering.Universal.UniversalAdditionalLightData");

        RenderSettings.sun = lightGo.GetComponent<Light>();
        SaveActiveScene(WorkshopScenePath);
        return ValidateWorkshopScene();
    }

    static void ConfigureGround(GameObject ground, Vector3 scale, Material material)
    {
        ground.transform.SetPositionAndRotation(new Vector3(0f, -0.5f, 0f), Quaternion.identity);
        ground.transform.localScale = scale;
        ground.isStatic = true;
        ground.tag = "Untagged";

        BoxCollider collider = ground.GetComponent<BoxCollider>();
        if (collider == null)
            collider = ground.AddComponent<BoxCollider>();
        collider.isTrigger = false;
        collider.center = Vector3.zero;
        collider.size = Vector3.one;

        Renderer renderer = ground.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        renderer.receiveShadows = true;
    }

    static void ConfigurePlayer(GameObject player, Material material)
    {
        player.transform.SetPositionAndRotation(new Vector3(0f, 1f, 0f), Quaternion.identity);
        player.transform.localScale = Vector3.one;
        player.isStatic = false;
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
        controller.detectCollisions = true;

        if (player.GetComponent<PlayerController>() == null)
            player.AddComponent<PlayerController>();

        Renderer renderer = player.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
    }

    static void ConfigureWorldCamera(GameObject cameraGo, Transform target)
    {
        Camera cameraComponent = cameraGo.GetComponent<Camera>();
        ConfigurePerspectiveCamera(cameraComponent);
        cameraGo.tag = "MainCamera";

        CameraFollow follow = cameraGo.GetComponent<CameraFollow>();
        if (follow == null)
            follow = cameraGo.AddComponent<CameraFollow>();

        SerializedObject serializedFollow = new SerializedObject(follow);
        serializedFollow.FindProperty("target").objectReferenceValue = target;
        serializedFollow.ApplyModifiedPropertiesWithoutUndo();
        follow.ApplyGameplayView(target);
    }

    static void WirePlayerCamera(GameObject player, Camera cameraComponent)
    {
        PlayerController controller = player.GetComponent<PlayerController>();
        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("moveSpeed").floatValue = 7f;
        serializedController.FindProperty("turnSpeed").floatValue = 540f;
        serializedController.FindProperty("gameplayCamera").objectReferenceValue = cameraComponent;
        serializedController.ApplyModifiedPropertiesWithoutUndo();
    }

    static void ConfigureOverviewCamera(GameObject cameraGo, float span)
    {
        Camera cameraComponent = cameraGo.GetComponent<Camera>();
        ConfigurePerspectiveCamera(cameraComponent);
        cameraGo.tag = "MainCamera";
        cameraGo.transform.position = new Vector3(0f, span, -span);
        cameraGo.transform.LookAt(Vector3.zero);
    }

    static void ConfigurePerspectiveCamera(Camera cameraComponent)
    {
        cameraComponent.orthographic = false;
        cameraComponent.fieldOfView = 40f;
        cameraComponent.nearClipPlane = 0.2f;
        cameraComponent.farClipPlane = 400f;
        cameraComponent.clearFlags = CameraClearFlags.Skybox;
        cameraComponent.depth = -1f;
    }

    static GameObject EnsureDirectionalLight()
    {
        GameObject lightGo = EnsureRoot(LightName);
        Light lightComponent = lightGo.GetComponent<Light>();
        if (lightComponent == null)
            lightComponent = lightGo.AddComponent<Light>();

        lightComponent.type = LightType.Directional;
        lightComponent.color = Color.white;
        lightComponent.intensity = 2f;
        lightComponent.shadows = LightShadows.Soft;
        lightComponent.lightmapBakeType = LightmapBakeType.Realtime;
        lightComponent.useColorTemperature = true;
        lightComponent.colorTemperature = 5000f;
        lightGo.transform.SetPositionAndRotation(new Vector3(0f, 3f, 0f), Quaternion.Euler(50f, -30f, 0f));
        return lightGo;
    }

    static GameObject EnsureCamera()
    {
        GameObject cameraGo = EnsureRoot(CameraName);
        if (cameraGo.GetComponent<Camera>() == null)
            cameraGo.AddComponent<Camera>();
        if (cameraGo.GetComponent<AudioListener>() == null)
            cameraGo.AddComponent<AudioListener>();
        return cameraGo;
    }

    static void EnsureSingleAudioListener(GameObject cameraGo)
    {
        AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (AudioListener listener in listeners)
        {
            if (listener != null && listener.gameObject != cameraGo)
                Object.DestroyImmediate(listener);
        }

        if (cameraGo.GetComponent<AudioListener>() == null)
            cameraGo.AddComponent<AudioListener>();
    }

    static void ApplyEnvironment()
    {
        if (RenderSettings.skybox == null)
        {
            Material skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            if (skybox != null)
                RenderSettings.skybox = skybox;
        }

        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.fog = false;
    }

    static string EnsureBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(scene => scene.path == WorldScenePath || scene.path == WorkshopScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(WorkshopScenePath, true));
        scenes.Insert(0, new EditorBuildSettingsScene(WorldScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        return DescribeBuildSettings();
    }

    static string DescribeBuildSettings()
    {
        var report = new StringBuilder();
        report.AppendLine("Build Settings:");
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            report.AppendLine("- " + (scene.enabled ? "enabled" : "disabled") + " " + scene.path);
        return report.ToString().TrimEnd();
    }

    static string ValidateWorldScene()
    {
        var report = new StringBuilder();
        report.AppendLine("WorldScene:");
        report.AppendLine(ValidateGround(new Vector3(40f, 1f, 40f)));

        GameObject player = FindRoot(PlayerName);
        GameObject cameraGo = FindRoot(CameraName);
        report.AppendLine("- Player 数量: " + CountRoots(PlayerName));
        report.AppendLine("- Main Camera 数量: " + CountRoots(CameraName));
        report.AppendLine("- Directional Light 数量: " + CountRoots(LightName));

        if (player == null)
        {
            report.AppendLine("- 缺少 Player");
            return report.ToString().TrimEnd();
        }

        CharacterController controller = player.GetComponent<CharacterController>();
        PlayerController playerController = player.GetComponent<PlayerController>();
        bool hasBlockingCollider = player.GetComponent<CapsuleCollider>() != null || player.GetComponent<Rigidbody>() != null;
        report.AppendLine("- Player 位置: " + player.transform.position);
        report.AppendLine("- CharacterController: " + (controller != null));
        report.AppendLine("- 额外胶囊碰撞或刚体: " + hasBlockingCollider);
        report.AppendLine("- PlayerController: " + (playerController != null));

        if (playerController != null)
        {
            SerializedObject serializedController = new SerializedObject(playerController);
            Object cameraReference = serializedController.FindProperty("gameplayCamera").objectReferenceValue;
            report.AppendLine("- Player 摄像机引用: " + (cameraReference != null ? cameraReference.name : "空"));
            report.AppendLine("- moveSpeed: " + serializedController.FindProperty("moveSpeed").floatValue);
        }

        if (cameraGo == null)
        {
            report.AppendLine("- 缺少 Main Camera");
            return report.ToString().TrimEnd();
        }

        Camera cameraComponent = cameraGo.GetComponent<Camera>();
        CameraFollow follow = cameraGo.GetComponent<CameraFollow>();
        report.AppendLine("- 透视投影: " + (cameraComponent != null && !cameraComponent.orthographic));
        report.AppendLine("- 视野: " + (cameraComponent != null ? cameraComponent.fieldOfView.ToString("0.#") : "无"));
        report.AppendLine("- CameraFollow: " + (follow != null));
        if (follow != null)
        {
            SerializedObject serializedFollow = new SerializedObject(follow);
            Object target = serializedFollow.FindProperty("target").objectReferenceValue;
            float distance = serializedFollow.FindProperty("horizontalDistance").floatValue;
            float height = serializedFollow.FindProperty("height").floatValue;
            report.AppendLine("- 跟随目标: " + (target != null ? target.name : "空"));
            report.AppendLine("- 水平距离/高度: " + distance.ToString("0.##") + " / " + height.ToString("0.##"));
        }

        return report.ToString().TrimEnd();
    }

    static string ValidateWorkshopScene()
    {
        var report = new StringBuilder();
        report.AppendLine("WorkshopScene:");
        report.AppendLine(ValidateGround(new Vector3(20f, 1f, 20f)));
        report.AppendLine("- Player 数量: " + CountRoots(PlayerName));
        report.AppendLine("- Main Camera 数量: " + CountRoots(CameraName));
        report.AppendLine("- Directional Light 数量: " + CountRoots(LightName));
        report.AppendLine("- WorkshopRoot 数量: " + CountRoots(WorkshopRootName));

        GameObject cameraGo = FindRoot(CameraName);
        if (cameraGo != null)
        {
            Camera cameraComponent = cameraGo.GetComponent<Camera>();
            report.AppendLine("- 透视投影: " + (cameraComponent != null && !cameraComponent.orthographic));
            report.AppendLine("- CameraFollow: " + (cameraGo.GetComponent<CameraFollow>() != null));
        }

        return report.ToString().TrimEnd();
    }

    static string ValidateGround(Vector3 expectedScale)
    {
        GameObject ground = FindRoot(GroundName);
        if (ground == null)
            return "- 缺少 Ground";

        BoxCollider collider = ground.GetComponent<BoxCollider>();
        Renderer renderer = ground.GetComponent<Renderer>();
        float top = renderer != null ? renderer.bounds.max.y : float.NaN;
        bool scaleMatches = Vector3.Distance(ground.transform.localScale, expectedScale) < 0.001f;
        return "- Ground 数量: " + CountRoots(GroundName)
            + "，缩放正确: " + scaleMatches
            + "，碰撞体: " + (collider != null && !collider.isTrigger)
            + "，顶面 Y: " + top.ToString("0.###");
    }

    static void OpenOrCreate(string assetPath)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(assetPath) != null)
        {
            EditorSceneManager.OpenScene(assetPath, OpenSceneMode.Single);
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, assetPath);
    }

    static void SaveActiveScene(string assetPath)
    {
        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, assetPath);
    }

    static GameObject EnsurePrimitive(string name, PrimitiveType type)
    {
        GameObject keep = null;
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name != name)
                continue;

            if (keep == null && root.GetComponent<MeshFilter>() != null)
                keep = root;
            else
                Object.DestroyImmediate(root);
        }

        if (keep != null)
            return keep;

        GameObject created = GameObject.CreatePrimitive(type);
        created.name = name;
        return created;
    }

    static GameObject EnsureRoot(string name)
    {
        GameObject keep = null;
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name != name)
                continue;

            if (keep == null)
                keep = root;
            else
                Object.DestroyImmediate(root);
        }

        return keep != null ? keep : new GameObject(name);
    }

    static void RemoveRoot(string name)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == name)
                Object.DestroyImmediate(root);
        }
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

    static void DestroyComponentIfPresent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component != null)
            Object.DestroyImmediate(component);
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
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName);
            if (type != null)
                return type;
        }

        return null;
    }

    static Material GetOrCreateLitMaterial(string assetPath, Color color)
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(LitShaderPath);
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            throw new InvalidOperationException("找不到 URP Lit shader，无法创建白盒材质。");

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

    static void EnsureFolders()
    {
        EnsureFolder("Assets/Scenes");
        EnsureFolder("Assets/Scripts");
        EnsureFolder("Assets/Scripts/Player");
        EnsureFolder("Assets/Scripts/Camera");
        EnsureFolder("Assets/Scripts/Interaction");
        EnsureFolder("Assets/Scripts/SceneManagement");
        EnsureFolder("Assets/Scripts/Editor");
        EnsureFolder("Assets/Prefabs");
        EnsureFolder("Assets/Materials");
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
