using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class ProjectOneUpgrade
{
    private const string GeneratedFolder = "Assets/_Game/Generated";
    private const string PlankMeshPath = GeneratedFolder + "/CrescentPlank.asset";
    private const string BambooMaterialPath = GeneratedFolder + "/BambooYellow.mat";
    private const string PlankMaterialPath = GeneratedFolder + "/CrescentPlankWood.mat";
    private const string RopeMaterialPath = GeneratedFolder + "/SoftSilkRope.mat";
    private const string WorkbenchMaterialPath = GeneratedFolder + "/WorkbenchWood.mat";
    private const string WorkbenchDarkMaterialPath = GeneratedFolder + "/WorkbenchWoodDark.mat";

    [MenuItem("Tools/Five Pillar Puzzle/Apply Project Two Upgrade")]
    public static void ApplyProjectTwoUpgrade()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "WhiteboxLevel")
        {
            Debug.LogError("Open Assets/Scenes/WhiteboxLevel.unity before applying the project-two upgrade.");
            return;
        }

        EnsureFolder(GeneratedFolder);

        Material bamboo = EnsureMaterial(
            BambooMaterialPath,
            new Color(0.847f, 0.745f, 0.518f),
            0.28f);
        Material plank = EnsureMaterial(
            PlankMaterialPath,
            new Color(0.906f, 0.788f, 0.541f),
            0.22f);
        Material rope = EnsureMaterial(
            RopeMaterialPath,
            new Color(0.949f, 0.902f, 0.784f),
            0.18f);
        Material wood = EnsureMaterial(
            WorkbenchMaterialPath,
            new Color(0.45f, 0.30f, 0.18f),
            0.12f);
        Material darkWood = EnsureMaterial(
            WorkbenchDarkMaterialPath,
            new Color(0.32f, 0.20f, 0.12f),
            0.12f);
        Mesh plankMesh = EnsurePlankMesh();

        Transform puzzle = FindSceneTransform("FivePillarPuzzle");
        Transform pillars = FindSceneTransform("FivePillarPuzzle/FivePillars");
        if (puzzle == null || pillars == null)
        {
            Debug.LogError("The FivePillarPuzzle hierarchy was not found.");
            return;
        }

        ApplyExtendedLevelLayout(puzzle, pillars);
        ApplyPlayerTuning(darkWood);
        ApplyPillarStyle(pillars, bamboo);
        ApplyPlankStyle("FivePillarPuzzle/Section_1/LeftDescentBlock_1", plankMesh, plank, rope);
        ApplyPlankStyle("FivePillarPuzzle/Section_1/RightPullBlock_1", plankMesh, plank, rope);
        ApplyPlankStyle("FivePillarPuzzle/Section_2/LeftDescentBlock_2", plankMesh, plank, rope);
        ApplyPlankStyle("FivePillarPuzzle/Section_2/RightPullBlock_2", plankMesh, plank, rope);
        FitBarrier("FivePillarPuzzle/Section_1/FullWidthBarrier_AfterPull");
        FitBarrier("FivePillarPuzzle/Section_2/FullWidthBarrier_AfterPull");

        foreach (RopeVisual visual in Object.FindObjectsByType<RopeVisual>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (visual.gameObject.scene != scene) continue;
            bool ring = visual.name.StartsWith("Ring_");
            visual.SetStyle(rope, ring ? 0.2f : 0.24f, ring ? 0.055f : 0.09f);
            EditorUtility.SetDirty(visual);
        }

        BuildWorkbench(puzzle, wood);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ProjectOneUpgrade] Crescent planks, soft ropes, bamboo palette and full workbench applied.");
    }

    private static void ApplyPillarStyle(Transform pillars, Material bamboo)
    {
        foreach (MeshRenderer renderer in pillars.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!renderer.name.StartsWith("Pillar_")) continue;
            Undo.RecordObject(renderer, "Apply bamboo pillar material");
            renderer.sharedMaterial = bamboo;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static void ApplyPlayerTuning(Material facingMaterial)
    {
        Transform player = FindSceneTransform("Player");
        if (player == null) return;
        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller == null) return;
        var serialized = new SerializedObject(controller);
        serialized.FindProperty("turnSpeed").floatValue = 14f;
        serialized.FindProperty("swingPumpAcceleration").floatValue = 1.45f;
        serialized.FindProperty("swingDamping").floatValue = 0.997f;
        serialized.FindProperty("swingReleaseForwardBoost").floatValue = 0.7f;
        serialized.FindProperty("swingReleaseUpBoost").floatValue = 2.8f;
        serialized.FindProperty("swingMaxTangentSpeed").floatValue = 5.3f;
        serialized.FindProperty("maxSwingAngle").floatValue = 72f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);

        Transform marker = player.Find("FacingMarker");
        if (marker == null)
        {
            GameObject markerObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            markerObject.name = "FacingMarker";
            marker = markerObject.transform;
            marker.SetParent(player, false);
            Object.DestroyImmediate(markerObject.GetComponent<Collider>());
        }
        marker.localPosition = new Vector3(0f, 0.12f, 0.43f);
        marker.localRotation = Quaternion.identity;
        marker.localScale = new Vector3(0.22f, 0.16f, 0.13f);
        marker.GetComponent<MeshRenderer>().sharedMaterial = facingMaterial;
    }

    private static void ApplyExtendedLevelLayout(Transform puzzle, Transform pillars)
    {
        const float levelCenterZ = 27.5f;
        const float pillarHalfLength = 31.5f;
        for (int pillarIndex = 1; pillarIndex <= 5; pillarIndex++)
        {
            Transform pillar = pillars.Find("Pillar_" + pillarIndex);
            if (pillar == null) continue;
            Undo.RecordObject(pillar, "Lengthen five-pillar table");
            Vector3 position = pillar.position;
            position.z = levelCenterZ;
            pillar.position = position;
            Vector3 scale = pillar.localScale;
            scale.y = pillarHalfLength;
            pillar.localScale = scale;
            EditorUtility.SetDirty(pillar);
        }
        Transform obsoleteSegment = pillars.Find("Pillar_5_SecondShortSegment");
        if (obsoleteSegment != null) obsoleteSegment.gameObject.SetActive(false);

        foreach (RopeVisual ring in puzzle.GetComponentsInChildren<RopeVisual>(true))
        {
            if (!ring.name.StartsWith("Ring_Row")) continue;
            int start = "Ring_Row".Length;
            int end = ring.name.IndexOf('_', start);
            if (end <= start || !int.TryParse(ring.name.Substring(start, end - start), out int row)) continue;
            if (row < 0 || row >= RingObstacleGenerator.Rows.Length) continue;
            ring.SetPlaneZ(RingObstacleGenerator.Rows[row]);
            EditorUtility.SetDirty(ring);
        }

        ConfigureExtendedSection(puzzle.Find("Section_1"), 1, 20f);
        ConfigureExtendedSection(puzzle.Find("Section_2"), 2, 47f);

        Transform player = FindSceneTransform("Player");
        if (player != null)
        {
            player.position = new Vector3(0f, 0.6f, -3f);
            player.rotation = Quaternion.identity;
            EditorUtility.SetDirty(player);
        }
    }

    private static void ConfigureExtendedSection(Transform section, int index, float sectionZ)
    {
        if (section == null) return;

        float descentZ = sectionZ - PullBlockMechanism.PlatformGapHalf;
        float ascentZ = sectionZ + PullBlockMechanism.PlatformGapHalf;
        Transform leftBlock = section.Find("LeftDescentBlock_" + index);
        Transform rightBlock = section.Find("RightPullBlock_" + index);
        Transform pullMarker = section.Find("PullPoint_" + index);
        Transform barrier = section.Find("FullWidthBarrier_AfterPull");
        ClimbableRope descent = section.Find("LeftDescentRope").GetComponent<ClimbableRope>();
        ClimbableRope ascent = section.Find("TransferredAscentRope").GetComponent<ClimbableRope>();
        RopeVisual descentVisual = descent.GetComponent<RopeVisual>();
        RopeVisual ascentVisual = ascent.GetComponent<RopeVisual>();
        RopeVisual pullVisual = section.Find("PullAndCrossPillarRope").GetComponent<RopeVisual>();

        leftBlock.gameObject.SetActive(true);
        rightBlock.gameObject.SetActive(true);
        leftBlock.SetPositionAndRotation(new Vector3(-10.5f, -6.3f, descentZ), Quaternion.identity);
        rightBlock.SetPositionAndRotation(new Vector3(10.5f, -6.3f, sectionZ), Quaternion.identity);

        Vector3[] descentPath =
        {
            new Vector3(-10.5f, -4.95f, descentZ),
            new Vector3(-10.5f, 0.55f, descentZ),
            new Vector3(-7.7f, 0.55f, descentZ)
        };
        Vector3[] ascentPath =
        {
            new Vector3(-10.5f, -4.95f, ascentZ),
            new Vector3(-10.5f, 0.55f, ascentZ),
            new Vector3(-7.7f, 0.55f, ascentZ)
        };
        descent.Configure(descentPath, true);
        descentVisual.SetPoints(new[]
        {
            new Vector3(-10.5f, -5.5f, descentZ),
            new Vector3(-10.5f, 0.2f, descentZ),
            new Vector3(-8.4f, 0.2f, descentZ)
        });
        descent.ConfigureSwingPlatform(leftBlock, descentVisual);
        ascent.Configure(ascentPath, false);
        ascentVisual.SetPoints(new[]
        {
            new Vector3(-10.5f, -5.5f, ascentZ),
            new Vector3(-10.5f, 0.2f, ascentZ),
            new Vector3(-8.4f, 0.2f, ascentZ)
        });

        if (barrier != null)
        {
            barrier.position = new Vector3(0f, 0.5f, sectionZ);
            barrier.gameObject.SetActive(false);
        }
        if (pullMarker != null)
            pullMarker.position = new Vector3(7.7f, 0.05f, sectionZ - 1.95f);

        PullBlockMechanism mechanism = section.GetComponent<PullBlockMechanism>();
        mechanism.Configure(
            rightBlock,
            pullVisual,
            descent,
            ascent,
            barrier != null ? barrier.gameObject : null,
            sectionZ);

        EditorUtility.SetDirty(leftBlock);
        EditorUtility.SetDirty(rightBlock);
        EditorUtility.SetDirty(descent);
        EditorUtility.SetDirty(ascent);
        EditorUtility.SetDirty(mechanism);
    }

    private static void FitBarrier(string path)
    {
        Transform target = FindSceneTransform(path);
        if (target == null) return;
        BoxCollider collider = target.GetComponent<BoxCollider>();
        if (collider == null) return;
        Undo.RecordObject(collider, "Fit rope barrier collider");
        collider.size = new Vector3(17.6f, 1.4f, 0.35f);
        collider.center = Vector3.zero;
        EditorUtility.SetDirty(collider);
    }

    private static void ApplyPlankStyle(
        string path,
        Mesh mesh,
        Material plankMaterial,
        Material ropeMaterial)
    {
        Transform target = FindSceneTransform(path);
        if (target == null)
        {
            Debug.LogWarning("Missing plank object: " + path);
            return;
        }

        Undo.RecordObject(target, "Shape crescent plank");
        Vector3 position = target.position;
        position.y = -6.3f;
        target.position = position;
        target.rotation = Quaternion.identity;
        target.localScale = Vector3.one;

        MeshFilter filter = target.GetComponent<MeshFilter>();
        MeshRenderer renderer = target.GetComponent<MeshRenderer>();
        BoxCollider collider = target.GetComponent<BoxCollider>();
        if (filter != null)
        {
            Undo.RecordObject(filter, "Assign crescent plank mesh");
            filter.sharedMesh = mesh;
            EditorUtility.SetDirty(filter);
        }
        if (renderer != null)
        {
            Undo.RecordObject(renderer, "Assign crescent plank material");
            renderer.sharedMaterial = plankMaterial;
            EditorUtility.SetDirty(renderer);
        }
        if (collider != null)
        {
            Undo.RecordObject(collider, "Fit standing collider to plank");
            // The physical top follows the raised boat rim. It is deliberately
            // broad and level so the capsule can land and stand reliably.
            collider.size = new Vector3(3.15f, 0.38f, 3.35f);
            collider.center = new Vector3(0f, 0.58f, 0f);
            EditorUtility.SetDirty(collider);
        }

        Transform binding = target.Find("RopeBinding");
        if (binding == null)
        {
            GameObject bindingObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bindingObject.name = "RopeBinding";
            binding = bindingObject.transform;
            binding.SetParent(target, false);
            Object.DestroyImmediate(bindingObject.GetComponent<Collider>());
        }
        binding.localPosition = new Vector3(0f, 0.73f, 0f);
        binding.localRotation = Quaternion.Euler(0f, 0f, 90f);
        binding.localScale = new Vector3(0.085f, 0.34f, 0.085f);
        binding.GetComponent<MeshRenderer>().sharedMaterial = ropeMaterial;
        EditorUtility.SetDirty(target);
    }

    private static void BuildWorkbench(Transform puzzle, Material wood)
    {
        Transform existing = puzzle.Find("WeavingWorkbench_ProjectTwo");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        var rootObject = new GameObject("WeavingWorkbench_ProjectTwo");
        Undo.RegisterCreatedObjectUndo(rootObject, "Create weaving workbench");
        Transform root = rootObject.transform;
        root.SetParent(puzzle, false);
        root.localPosition = Vector3.zero;

        Transform frame = NewGroup("WoodenFrame", root);

        // This is the visible workbench from project two, scaled to the five
        // shorter pillars in project one.  Project two keeps the alternative
        // legs, side rails and overhead shuttle rig disabled; they must not be
        // merged into the finished bench.
        AddCube("BambooSupportSurface", frame,
            new Vector3(0f, -4.212001f, 27.5f),
            new Vector3(18f, 0.720001f, 63f), wood);
        AddCube("CenterBeam", frame,
            new Vector3(0f, -11.943016f, 27.5f),
            new Vector3(4.320017f, 4.320017f, 60.48f), wood);

        BuildEndFrame("Frame_Front", frame, -4.000003f, wood);
        BuildEndFrame("Frame_Back", frame, 59.000003f, wood);

        EditorUtility.SetDirty(rootObject);
    }

    private static void BuildEndFrame(string name, Transform parent, float z, Material wood)
    {
        Transform endFrame = NewGroup(name, parent);
        AddCube("TopBeam", endFrame,
            new Vector3(0f, -5.868015f, z),
            new Vector3(20.592012f, 4.320017f, 4.320017f), wood);
        AddCube("BottomBeam", endFrame,
            new Vector3(0f, -18.018017f, z),
            new Vector3(20.592012f, 4.320017f, 4.320017f), wood);
        AddCube("VerticalSupport", endFrame,
            new Vector3(0f, -11.943016f, z),
            new Vector3(4.320017f, 7.829984f, 4.320017f), wood);
    }

    private static Transform NewGroup(string name, Transform parent)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        return gameObject.transform;
    }

    private static GameObject AddCube(
        string name,
        Transform parent,
        Vector3 worldPosition,
        Vector3 worldScale,
        Material material)
    {
        GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gameObject.name = name;
        gameObject.transform.SetParent(parent, true);
        gameObject.transform.position = worldPosition;
        gameObject.transform.rotation = Quaternion.identity;
        gameObject.transform.localScale = worldScale;
        gameObject.GetComponent<MeshRenderer>().sharedMaterial = material;
        Object.DestroyImmediate(gameObject.GetComponent<Collider>());
        return gameObject;
    }

    private static GameObject AddCylinder(
        string name,
        Transform parent,
        Vector3 worldPosition,
        Vector3 worldScale,
        Material material)
    {
        GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        gameObject.name = name;
        gameObject.transform.SetParent(parent, true);
        gameObject.transform.position = worldPosition;
        gameObject.transform.localScale = worldScale;
        gameObject.GetComponent<MeshRenderer>().sharedMaterial = material;
        Object.DestroyImmediate(gameObject.GetComponent<Collider>());
        return gameObject;
    }

    private static GameObject AddBrace(
        string name,
        Transform parent,
        Vector3 from,
        Vector3 to,
        float width,
        Material material)
    {
        GameObject brace = AddCube(name, parent, (from + to) * 0.5f, Vector3.one, material);
        Vector3 direction = to - from;
        brace.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        brace.transform.localScale = new Vector3(width, width, direction.magnitude);
        return brace;
    }

    private static Mesh EnsurePlankMesh()
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(PlankMeshPath);
        Mesh generated = CrescentPlankMesh.Build();
        if (mesh == null)
        {
            AssetDatabase.CreateAsset(generated, PlankMeshPath);
            mesh = generated;
        }
        else
        {
            EditorUtility.CopySerialized(generated, mesh);
            Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(mesh);
        }
        return mesh;
    }

    private static Material EnsureMaterial(string path, Color color, float smoothness)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Transform FindSceneTransform(string path)
    {
        string[] parts = path.Split('/');
        GameObject root = GameObject.Find(parts[0]);
        if (root == null) return null;
        Transform current = root.transform;
        for (int i = 1; i < parts.Length; i++)
        {
            current = current.Find(parts[i]);
            if (current == null) return null;
        }
        return current;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
