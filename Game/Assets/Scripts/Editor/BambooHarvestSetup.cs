using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>只挂载竹林交互，不重新生成场景或更改竹子的位置。</summary>
public static class BambooHarvestSetup
{
    const string PreviewKey = "BambooHarvest.StartPreview";

    [InitializeOnLoadMethod]
    static void RegisterPreview()
    {
        EditorApplication.playModeStateChanged -= OnPreviewPlayMode;
        EditorApplication.playModeStateChanged += OnPreviewPlayMode;
    }

    [MenuItem("宣纸帘/试玩竹林采集")]
    public static void PreviewHarvest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        EditorSceneManager.OpenScene(SceneTransitionState.WorldScenePath);
        SessionState.SetBool(PreviewKey, true);
        EditorApplication.isPlaying = true;
    }

    static void OnPreviewPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PreviewKey, false))
            return;
        SessionState.EraseBool(PreviewKey);
        var bamboo = UnityEngine.Object.FindObjectsByType<ChopableBamboo>(FindObjectsSortMode.None)
            .OrderBy(b => Vector3.Distance(b.transform.position, BambooHarvest.PathAnchor)).FirstOrDefault();
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        if (bamboo == null || player == null)
            return;
        CharacterController controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.position = bamboo.transform.position - Vector3.forward * 1.5f + Vector3.up * 0.1f;
        controller.enabled = true;
        Physics.SyncTransforms();
        if (Camera.main != null)
            Camera.main.GetComponent<CameraFollow>()?.SnapToTarget();
        Type gameView = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        if (gameView != null)
            EditorWindow.GetWindow(gameView).Focus();
        Debug.Log("竹林试玩已就绪：靠近 Bamboo_33，连续按 8 次空格砍断，再靠近掉落物按空格拾取。");
    }

    [MenuItem("宣纸帘/配置竹林砍伐")]
    public static void ConfigureWorldScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("请先退出 Play Mode。");

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != SceneTransitionState.WorldScenePath)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            scene = EditorSceneManager.OpenScene(SceneTransitionState.WorldScenePath);
        }

        GameObject forest = GameObject.Find("BambooForest");
        if (forest == null)
            throw new InvalidOperationException("WorldScene 缺少 BambooForest。");

        int count = BambooHarvest.EnsureHarvestStalks(forest.transform, BambooHarvest.HarvestCount);
        if (count != BambooHarvest.HarvestCount)
            throw new InvalidOperationException("竹林不足六根可砍竹子，未保存场景。");

        foreach (ChopableBamboo bamboo in forest.GetComponentsInChildren<ChopableBamboo>())
        {
            var settings = new SerializedObject(bamboo);
            settings.FindProperty("chopsToFell").intValue = 8;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        if (UnityEngine.Object.FindFirstObjectByType<InteractionPromptUI>() == null)
            throw new InvalidOperationException("WorldScene 缺少 InteractionPromptUI。");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("竹林砍伐已配置：" + string.Join(", ", forest.GetComponentsInChildren<ChopableBamboo>()
            .Where(bamboo => bamboo.enabled).Select(bamboo => bamboo.name)) + "；每根 8 刀，空格拾取完整青竹。 ");
    }
}
