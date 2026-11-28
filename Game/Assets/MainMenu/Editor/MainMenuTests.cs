using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class MainMenuTests
{
    [UnityTearDown]
    public IEnumerator RestorePlayMode()
    {
        if (Application.isPlaying)
            yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator QuitButtonStopsEditorPlayMode()
    {
        EditorSceneManager.OpenScene(MainMenuSetup.ScenePath);
        yield return new EnterPlayMode();
        yield return Frames(2);
        yield return new ClickQuitAndExit();
        Assert.That(EditorApplication.isPlaying, Is.False);
    }

    sealed class ClickQuitAndExit : IEditModeTestYieldInstruction
    {
        public bool ExpectDomainReload => false;
        public bool ExpectedPlaymodeState => false;

        public IEnumerator Perform()
        {
            var controller = Object.FindFirstObjectByType<MainMenuController>();
            Click(controller.GetComponentsInChildren<Button>().Single(button => button.name == "退出游戏"));
            while (EditorApplication.isPlaying)
                yield return null;
        }
    }

    [Test]
    public void SceneReferencesButtonsAndBuildListAreReady()
    {
        EditorSceneManager.OpenScene(MainMenuSetup.ScenePath);
        var controller = Object.FindFirstObjectByType<MainMenuController>();
        Assert.That(controller, Is.Not.Null);
        var buttons = controller.GetComponentsInChildren<Button>();
        Assert.That(buttons.Length, Is.EqualTo(3));
        Assert.That(buttons.Single(button => button.name == "读取游戏").interactable, Is.False);
        Assert.That(buttons.Single(button => button.name == "读取游戏").colors.disabledColor, Is.EqualTo(Color.white));
        Assert.That(EditorBuildSettings.scenes[0].path, Is.EqualTo(MainMenuSetup.ScenePath));
        Assert.That(EditorBuildSettings.scenes.Any(scene => scene.path == SceneTransitionState.WorldScenePath && scene.enabled), Is.True);
        Assert.That(controller.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        Assert.That(controller.GetComponentsInChildren<EventSystem>().Length, Is.EqualTo(1));
        Assert.That(controller.transform.GetChild(0).name, Is.EqualTo("纸纹背景"));
    }

    [Test]
    public void MenuFitsThreeAspectRatiosAndRenders()
    {
        EditorSceneManager.OpenScene(MainMenuSetup.ScenePath);
        var controller = Object.FindFirstObjectByType<MainMenuController>();
        var canvas = controller.GetComponent<Canvas>();
        var camera = new GameObject("Menu verification camera").AddComponent<Camera>();
        var board = (RectTransform)controller.transform.GetChild(1);
        try
        {
            foreach (Vector2Int dimensions in new[] { new Vector2Int(1920,1080), new Vector2Int(1280,1024), new Vector2Int(2560,1080) })
                Render(canvas, camera, board, dimensions);
        }
        finally
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            Object.DestroyImmediate(camera.gameObject);
        }
    }

    static void Render(Canvas canvas, Camera camera, RectTransform board, Vector2Int size)
    {
        var render = new RenderTexture(size.x, size.y, 24);
        Texture2D image = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            render.Create();
            camera.targetTexture = render;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.magenta;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            board.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector3 point = camera.WorldToViewportPoint(corner);
                Assert.That(point.x, Is.InRange(-.002f, 1.002f));
                Assert.That(point.y, Is.InRange(-.002f, 1.002f));
            }
            var paper = (RectTransform)canvas.transform.GetChild(0);
            paper.GetWorldCorners(corners);
            Assert.That(camera.WorldToViewportPoint(corners[0]).x, Is.LessThanOrEqualTo(.002f));
            Assert.That(camera.WorldToViewportPoint(corners[0]).y, Is.LessThanOrEqualTo(.002f));
            Assert.That(camera.WorldToViewportPoint(corners[2]).x, Is.GreaterThanOrEqualTo(.998f));
            Assert.That(camera.WorldToViewportPoint(corners[2]).y, Is.GreaterThanOrEqualTo(.998f));
            camera.Render();
            RenderTexture.active = render;
            image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0,0,size.x,size.y),0,0);
            image.Apply();
            Directory.CreateDirectory("Logs/BambooHarvest");
            File.WriteAllBytes($"Logs/BambooHarvest/mainmenu-{size.x}x{size.y}.png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            if (image != null) Object.DestroyImmediate(image);
            render.Release();
            Object.DestroyImmediate(render);
        }
    }

    [UnityTest]
    public IEnumerator StartButtonLoadsWorldAndStartsExistingOpening()
    {
        EditorSceneManager.OpenScene(MainMenuSetup.ScenePath);
        yield return new EnterPlayMode();
        yield return Frames(2);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
        Assert.That(WorkshopOrder.Current, Is.Null);
        Assert.That(Cursor.visible, Is.True);
        Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
        var controller = Object.FindFirstObjectByType<MainMenuController>();
        var load = controller.GetComponentsInChildren<Button>().Single(button => button.name == "读取游戏");
        Click(load);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
        var start = controller.GetComponentsInChildren<Button>().Single(button => button.name == "开始游戏");
        Click(start);
        Click(start);
        var deadline = EditorApplication.timeSinceStartup + 45;
        while (SceneManager.GetActiveScene().name != SceneTransitionState.WorldSceneName || WorkshopOrder.Current == null)
        {
            Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline), "等待正式主线初始化");
            yield return null;
        }
        Assert.That(Object.FindFirstObjectByType<PlayerController>(), Is.Not.Null);
        Assert.That(Object.FindObjectsByType<WorkshopOrderGuide>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        Assert.That(Object.FindFirstObjectByType<OpeningSequence>(), Is.Not.Null);
        Assert.That(WorkshopOrder.Current.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.Opening));
        Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        Assert.That(Object.FindFirstObjectByType<MainMenuController>(), Is.Null);
        Object.FindFirstObjectByType<OpeningSequence>().CompleteImmediately();
        Assert.That(WorkshopOrder.Current.CurrentPhase, Is.EqualTo(WorkshopOrder.Phase.Village));
        Assert.That(Object.FindFirstObjectByType<PlayerController>().enabled, Is.True);
        yield return new ExitPlayMode();
    }

    static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var events = EventSystem.current;
        Assert.That(events, Is.Not.Null);
        var rect = (RectTransform)button.transform;
        var pointer = new PointerEventData(events)
        {
            position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
        };
        var hits = new List<RaycastResult>();
        events.RaycastAll(pointer,hits);
        Assert.That(hits.Count, Is.GreaterThan(0));
        Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.EqualTo(button));
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }

    static IEnumerator Frames(int count)
    {
        int target = Time.frameCount + count;
        while (Time.frameCount < target)
            yield return null;
    }
}
