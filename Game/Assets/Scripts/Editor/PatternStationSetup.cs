using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PatternStationSetup
{
    const string StationName = "PatternStation";
    const string MaterialFolder = "Assets/Materials/";

    [MenuItem("宣纸帘/配置纹样台")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("请先退出 Play Mode 再配置纹样台。");

        Material wood = LoadMaterial("WorkshopInteriorWood");
        Material bamboo = LoadMaterial("WorkshopInteriorBamboo");
        Material paper = LoadMaterial("WorkshopInteriorFloor");
        Material jade = LoadMaterial("WorkshopInteriorWeaving");
        Material vermilion = LoadMaterial("WorkshopInteriorLacquer");
        string path = SceneTransitionState.WorkshopScenePath;
        Scene scene = SceneManager.GetSceneByPath(path);
        bool openedForSetup = !scene.isLoaded;
        if (openedForSetup)
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

        try
        {
            Transform root = null;
            foreach (var candidate in scene.GetRootGameObjects())
            {
                if (candidate.name == "WorkshopRoot")
                    root = candidate.transform;
            }
            if (root == null)
                throw new InvalidOperationException("WorkshopScene 缺少 WorkshopRoot。");
            if (root.Find(StationName) != null)
            {
                if (root.Find(StationName).GetComponentInChildren<PatternStationTrigger>() == null)
                    throw new InvalidOperationException("发现同名工位，请先核对 PatternStation。");
                return;
            }
            if (scene.isDirty)
                throw new InvalidOperationException("请先保存当前工坊场景的修改，再配置纹样台。");

            var bench = new GameObject(StationName);
            SceneManager.MoveGameObjectToScene(bench, scene);
            Undo.RegisterCreatedObjectUndo(bench, "放置纹样台");
            Transform station = bench.transform;
            station.SetParent(root, false);
            station.localPosition = new Vector3(11.7f, 0f, 1f);

            Piece(station, "长木台面", PrimitiveType.Cube, new(0, .86f, 0), new(1.25f, .10f, 2.45f), wood, true);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    Piece(station, "桌腿", PrimitiveType.Cube, new(x * .50f, .41f, z * 1.03f),
                        new(.12f, .82f, .13f), wood, true);
            Piece(station, "阴干素帘", PrimitiveType.Cube, new(-.04f, .925f, -.30f), new(.91f, .031f, 1.17f), bamboo);
            Piece(station, "上夹", PrimitiveType.Cube, new(-.04f, .96f, .29f), new(.99f, .075f, .07f), wood);
            Piece(station, "下夹", PrimitiveType.Cube, new(-.04f, .96f, -.89f), new(.99f, .075f, .07f), wood);
            Piece(station, "纹样纸", PrimitiveType.Cube, new(-.20f, .916f, .74f), new(.54f, .019f, .53f), paper);
            Piece(station, "青线梭", PrimitiveType.Cube, new(.33f, .955f, .54f), new(.18f, .09f, .23f), jade);
            Piece(station, "朱线梭", PrimitiveType.Cube, new(.33f, .955f, .88f), new(.18f, .09f, .23f), vermilion);
            var point = new GameObject("PatternInteractionPoint");
            point.transform.SetParent(station, false);
            point.transform.localPosition = new Vector3(-1.5f, .2f, 0f);
            point.AddComponent<PatternStationTrigger>();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("无法保存纹样台所在的 WorkshopScene。");
            Debug.Log("独立纹样台已保存到 WorkshopScene。");
        }
        finally
        {
            if (openedForSetup && !scene.isDirty)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    static Material LoadMaterial(string name)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + name + ".mat");
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
