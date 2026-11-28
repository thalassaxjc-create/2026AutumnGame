using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Installs only the player's visual child; movement, camera and collision stay on Player.</summary>
public static class PlayerVisualSetup
{
    public const string ModelPath = "Assets/Art/Characters/Protagonist/character1.fbx";
    public const string PrefabPath = "Assets/Art/Characters/Protagonist/ProtagonistVisual.prefab";
    const string MaterialPath = "Assets/Art/Characters/Protagonist/ProtagonistFallback.mat";
    const string Evidence = "Logs/BambooHarvest/PlayerVisual";
    public static readonly string[] ScenePaths = { SceneTransitionState.WorkshopScenePath,
        SceneTransitionState.HomeScenePath, SceneTransitionState.WorldScenePath };

    [MenuItem("宣纸帘/配置主角人物外观")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("请先停止游戏再配置人物外观。");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("场景有未保存的编辑，请先保存再配置主角。");
        Directory.CreateDirectory(Evidence);
        foreach (string path in ScenePaths)
        {
            string backup = Evidence + "/before-" + Path.GetFileName(path);
            if (!File.Exists(backup)) File.Copy(path, backup);
        }
        GameObject prefab = BuildPrefab();
        foreach (string path in ScenePaths)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var players = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PlayerController>(true)).ToArray();
                if (players.Length != 1) throw new InvalidOperationException(path + " 必须只有一个主角。");
                PlayerController player = players[0];
                Transform visual = player.transform.Find("ProtagonistVisual");
                if (visual == null)
                    visual = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, player.transform)).transform;
                visual.name = "ProtagonistVisual";
                CharacterController controller = player.GetComponent<CharacterController>();
                visual.localScale = Vector3.one * (controller.height / 2f);
                visual.localRotation = Quaternion.identity;
                visual.localPosition = controller.center - Vector3.up * controller.height * .5f;
                MeshRenderer capsule = player.GetComponent<MeshRenderer>();
                if (capsule != null) capsule.enabled = false;
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("无法保存 " + path);
            }
            finally { if (opened && !scene.isDirty) EditorSceneManager.CloseScene(scene, true); }
        }
        AssetDatabase.SaveAssets();
        CapturePreview(prefab);
        File.WriteAllText(Evidence + "/setup-result.txt", "Configured player visual in WorkshopScene, HomeScene and WorldScene.\nSource texture missing: shared fallback material. No locomotion clips supplied; source bow actions are not imported.\n");
        Debug.Log("[PlayerVisual] 三个场景的主角外观已保存，移动、交互和碰撞组件保持不变。");
    }

    static GameObject BuildPrefab()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null) throw new InvalidOperationException("主角 FBX 尚未导入。");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/WhiteboxPlayer.mat"));
            material.name = "ProtagonistFallback";
            material.SetColor("_BaseColor", new Color(.72f, .68f, .59f));
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        var root = new GameObject("ProtagonistVisual");
        try
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            // Keep the imported bind transforms. No retargeting or automatic weight changes.
            foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true))
            {
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = false;
                animator.enabled = false;
            }
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                if (renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = false;
            }
            Bounds bounds = MeshBounds(instance);
            if (bounds.size.y < .001f || !float.IsFinite(bounds.size.y))
                throw new InvalidOperationException("主角模型高度无效。");
            float scale = 2f / bounds.size.y;
            instance.transform.localScale *= scale;
            instance.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
            File.WriteAllText(Evidence + "/model-bounds.txt", "Imported: " + bounds + "\nScale: " + scale + "\nNormalized: " + MeshBounds(instance));
            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    public static Bounds MeshBounds(GameObject root)
    {
        Bounds bounds = default;
        bool found = false;
        foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mesh = new Mesh();
            try
            {
                // Bake in renderer-local coordinates, then apply its transform exactly once.
                renderer.BakeMesh(mesh, true);
                foreach (Vector3 vertex in mesh.vertices)
                {
                    Vector3 point = root.transform.parent != null
                        ? root.transform.parent.InverseTransformPoint(renderer.transform.TransformPoint(vertex))
                        : renderer.transform.TransformPoint(vertex);
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                    else bounds.Encapsulate(point);
                }
            }
            finally { Object.DestroyImmediate(mesh); }
        }
        if (!found) throw new InvalidOperationException("主角模型没有蒙皮网格。");
        return bounds;
    }

    static void CapturePreview(GameObject prefab)
    {
        var preview = new PreviewRenderUtility();
        Texture2D image = null;
        try
        {
            GameObject instance = Object.Instantiate(prefab);
            preview.AddSingleGO(instance);
            preview.camera.nearClipPlane = .01f;
            preview.camera.farClipPlane = 30;
            preview.camera.fieldOfView = 30;
            preview.camera.transform.position = new Vector3(0, 1.1f, 5.5f);
            preview.camera.transform.LookAt(new Vector3(0, 1, 0));
            preview.lights[0].intensity = 1.2f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35, 145, 0);
            preview.lights[1].intensity = .8f;
            preview.ambientColor = new Color(.35f, .35f, .35f);
            preview.BeginStaticPreview(new Rect(0, 0, 512, 768));
            preview.Render(true);
            image = preview.EndStaticPreview();
            File.WriteAllBytes(Evidence + "/protagonist-preview.png", image.EncodeToPNG());
        }
        finally
        {
            if (image != null) Object.DestroyImmediate(image);
            preview.Cleanup();
        }
    }
}

public sealed class ProtagonistModelImporter : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if (assetPath != PlayerVisualSetup.ModelPath) return;
        var importer = (ModelImporter)assetImporter;
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.addCollider = false;
        importer.isReadable = false;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
    }
}
