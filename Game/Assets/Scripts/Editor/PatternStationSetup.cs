using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PatternStationSetup
{
    [MenuItem("宣纸帘/配置纹样台")]
    public static void Configure() =>
        SaveStation<PatternStationTrigger>("PatternStation", null, new Vector3(11.7f, 0f, 1f), PlacePatternBench);

    [MenuItem("宣纸帘/配置朱边台")]
    public static void ConfigureVermilionTrim() =>
        SaveStation<VermilionTrimStation>("VermilionTrimBench", "LacqueringArea", new Vector3(2.85f, 0f, -3.6f), PlaceTrimBench);

    static void SaveStation<T>(string name, string parentName, Vector3 position, Action<Transform> place) where T : Component
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("请先退出 Play Mode 再配置工位。");

        string path = SceneTransitionState.WorkshopScenePath;
        Scene scene = SceneManager.GetSceneByPath(path);
        bool openedForSetup = !scene.isLoaded;
        if (openedForSetup)
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

        try
        {
            Transform root = null;
            foreach (var candidate in scene.GetRootGameObjects())
                if (candidate.name == "WorkshopRoot")
                    root = candidate.transform;
            Transform parent = parentName == null ? root : root?.Find(parentName);
            if (parent == null)
                throw new InvalidOperationException($"WorkshopScene 缺少 {parentName ?? "WorkshopRoot"}。");

            Transform existing = parent.Find(name);
            if (existing != null)
            {
                if (existing.GetComponentInChildren<T>() == null)
                    throw new InvalidOperationException($"发现同名工位，请先核对 {name}。");
                return;
            }
            if (scene.isDirty)
                throw new InvalidOperationException("请先保存当前工坊场景，再添加工位。");

            var bench = new GameObject(name);
            SceneManager.MoveGameObjectToScene(bench, scene);
            Undo.RegisterCreatedObjectUndo(bench, "放置工位");
            bench.transform.SetParent(parent, false);
            bench.transform.localPosition = position;
            place(bench.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("无法保存 WorkshopScene。");
            Debug.Log($"{name} 已保存到 WorkshopScene。");
        }
        finally
        {
            if (openedForSetup && !scene.isDirty)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    static void PlacePatternBench(Transform station)
    {
        Material wood = LoadMaterial("WorkshopInteriorWood");
        Material bamboo = LoadMaterial("WorkshopInteriorBamboo");
        Material paper = LoadMaterial("WorkshopInteriorFloor");
        Material thread = LoadMaterial("WorkshopInteriorWeaving");
        Table(station, 1.25f, 2.45f, wood);
        Piece(station, "阴干素帘", PrimitiveType.Cube, new(-.04f, .925f, -.30f), new(.91f, .031f, 1.17f), bamboo);
        Piece(station, "上夹", PrimitiveType.Cube, new(-.04f, .96f, .29f), new(.99f, .075f, .07f), wood);
        Piece(station, "下夹", PrimitiveType.Cube, new(-.04f, .96f, -.89f), new(.99f, .075f, .07f), wood);
        Piece(station, "纹样纸", PrimitiveType.Cube, new(-.20f, .916f, .74f), new(.54f, .019f, .53f), paper);
        Piece(station, "线梭", PrimitiveType.Cube, new(.33f, .955f, .54f), new(.18f, .09f, .23f), thread);
        InteractionPoint(station, "PatternInteractionPoint", new Vector3(-1.5f, .2f, 0f)).AddComponent<PatternStationTrigger>();
    }

    static void PlaceTrimBench(Transform station)
    {
        Material wood = LoadMaterial("WorkshopInteriorWood");
        Material bamboo = LoadMaterial("WorkshopInteriorBamboo");
        Material silk = LoadMaterial("MarketBrick");
        Table(station, 1.67f, 1.22f, wood);
        Piece(station, "待包边竹帘", PrimitiveType.Cube, new(-.16f, .936f, -.04f), new(1.07f, .051f, .74f), bamboo);
        for (int side = -1; side <= 1; side += 2)
        {
            Piece(station, "朱绸横边", PrimitiveType.Cube, new(-.16f, .975f, -.04f + side * .355f),
                new(1.12f, .031f, .054f), silk);
            Piece(station, "朱绸竖边", PrimitiveType.Cube, new(-.16f + side * .533f, .975f, -.04f),
                new(.055f, .031f, .70f), silk);
        }
        Piece(station, "红绸卷", PrimitiveType.Cylinder, new(.59f, 1.00f, .20f), new(.19f, .088f, .19f), silk);
        Piece(station, "压边木片", PrimitiveType.Cube, new(.57f, .958f, -.28f), new(.12f, .044f, .29f), wood);
        InteractionPoint(station, "VermilionTrimInteractionPoint", new Vector3(0f, .2f, 1.21f))
            .AddComponent<VermilionTrimStation>();
    }

    static void Table(Transform station, float width, float depth, Material wood)
    {
        Piece(station, "木台面", PrimitiveType.Cube, new(0, .86f, 0), new(width, .10f, depth), wood, true);
        for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
                Piece(station, "桌腿", PrimitiveType.Cube, new(x * (width / 2f - .14f), .41f, z * (depth / 2f - .19f)),
                    new(.12f, .82f, .13f), wood, true);
    }

    static GameObject InteractionPoint(Transform station, string name, Vector3 position)
    {
        var point = new GameObject(name);
        point.transform.SetParent(station, false);
        point.transform.localPosition = position;
        return point;
    }

    static Material LoadMaterial(string name)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/" + name + ".mat");
        return material != null ? material : throw new InvalidOperationException("缺少工坊材质 " + name);
    }

    static void Piece(Transform root, string name, PrimitiveType shape, Vector3 position, Vector3 scale,
        Material material, bool solid = false)
    {
        var piece = GameObject.CreatePrimitive(shape);
        piece.name = name;
        piece.transform.SetParent(root, false);
        piece.transform.localPosition = position;
        piece.transform.localScale = scale;
        piece.GetComponent<Renderer>().sharedMaterial = material;
        if (!solid)
            UnityEngine.Object.DestroyImmediate(piece.GetComponent<Collider>());
    }
}
