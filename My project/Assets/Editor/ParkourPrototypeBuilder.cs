using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class ParkourPrototypeBuilder
{
    public static void Extend()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/WhiteboxLevel_ParkourPrototype.unity") throw new System.InvalidOperationException("Open prototype only.");
        Transform puzzle = null;
        foreach (var root in scene.GetRootGameObjects()) if (root.name == "FivePillarPuzzle") puzzle = root.transform;
        if (puzzle == null) throw new System.Exception("Missing puzzle");
        foreach (var component in puzzle.GetComponentsInChildren<Component>(true))
        {
            if (component == null) throw new System.Exception("Missing component");
            Undo.RecordObject(component, "Extend parkour prototype");
        }
        foreach (var root in scene.GetRootGameObjects()) foreach (var player in root.GetComponentsInChildren<PlayerController>(true))
        {
            foreach (var c in player.GetComponents<Component>()) Undo.RecordObject(c, "Enable prototype control");
            var so = new SerializedObject(player); so.FindProperty("parkourEnabled").boolValue = true; so.ApplyModifiedProperties();
            player.transform.position = new Vector3(0f, .6f, -3f);
        }
        var level = puzzle.GetComponent<FivePillarLevel>();
        var settings = new SerializedObject(level);
        settings.FindProperty("useParkourRules").boolValue = true;
        settings.FindProperty("randomizeOnPlay").boolValue = false;
        settings.ApplyModifiedProperties();
        var pillars = puzzle.Find("FivePillars");
        for (int i = 1; i <= 5; i++)
        {
            var t = pillars.Find("Pillar_" + i);
            var p = t.position; p.z = 86f; t.position = p;
            var s = t.localScale; s.y = 90f; t.localScale = s;
        }
        var old = pillars.Find("Pillar_5_SecondShortSegment"); if (old != null) old.gameObject.SetActive(false);
        var configure = typeof(ProjectOneUpgrade).GetMethod("ConfigureExtendedSection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        configure.Invoke(null, new object[] { puzzle.Find("Section_1"), 1, 58f });
        configure.Invoke(null, new object[] { puzzle.Find("Section_2"), 2, 124f });
        var frame = puzzle.Find("WeavingWorkbench_ProjectTwo/WoodenFrame");
        var support = frame.Find("BambooSupportSurface"); var position = support.position; position.z = 86f; support.position = position;
        var scale = support.localScale; scale.z = 180f; support.localScale = scale;
        var beam = frame.Find("CenterBeam"); position = beam.position; position.z = 86f; beam.position = position;
        scale = beam.localScale; scale.z = 172.8f; beam.localScale = scale;
        foreach (Transform t in frame.Find("Frame_Back")) { position = t.position; position.z = 176f; t.position = position; }
        foreach (var t in puzzle.GetComponentsInChildren<Transform>(true)) if (t.name.Contains("Finish")) { position = t.position; position.z = 172f; t.position = position; }
        EditorSceneManager.SaveScene(scene);
    }
}
