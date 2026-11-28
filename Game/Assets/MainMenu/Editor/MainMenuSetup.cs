using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Creates the isolated main menu from the supplied PNG assets.</summary>
public static class MainMenuSetup
{
    const string Folder = "Assets/MainMenu/";
    public const string ScenePath = Folder + "MainMenu.unity";
    public const string PrefabPath = Folder + "MainMenu.prefab";

    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null ||
            AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            throw new InvalidOperationException("MainMenu 已存在，请直接编辑现有场景或预制体。");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("MainMenu", typeof(RectTransform));
        root.SetActive(false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(16000, 9000);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        root.AddComponent<GraphicRaycaster>();

        Texture2D paper = Import("半熟生宣5k.png", false);
        var background = Image(root.transform, "纸纹背景", paper);
        background.raycastTarget = true;
        background.color = Color.white;
        var cover = background.gameObject.AddComponent<AspectRatioFitter>();
        cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        cover.aspectRatio = (float)paper.width / paper.height;

        var board = new GameObject("设计画板 16000×9000", typeof(RectTransform)).GetComponent<RectTransform>();
        board.SetParent(root.transform, false);
        board.anchorMin = board.anchorMax = board.pivot = new Vector2(.5f, .5f);
        board.sizeDelta = new Vector2(16000, 9000);
        board.gameObject.AddComponent<RectMask2D>();

        // Bounds measured against the full reference. PNG transparent margins are
        // retained on disk and excluded from the displayed UV rectangle.
        Art(board, "右侧插画", "start.illustration.png", new Rect(9802, -522, 6197.76f, 9523.2f),
            new Rect(1894, 0, 1614, 2480));
        Art(board, "标题", "title.png", new Rect(780, 630, 7054.3f, 4417.6f),
            new Rect(421, 428, 2143, 1342));
        var start = MenuButton(board, "开始游戏", "开始游戏图标.png", new Rect(1710, 5850, 2869, 1066.3f));
        var load = MenuButton(board, "读取游戏", "读取图标.png", new Rect(5520, 5850, 2878.4f, 1069.8f));
        var quit = MenuButton(board, "退出游戏", "退出游戏图标.png", new Rect(3750, 7340, 2860, 1063));
        load.interactable = false;

        var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        events.transform.SetParent(root.transform, false);
        var controller = root.AddComponent<MainMenuController>();
        var serialized = new SerializedObject(controller);
        serialized.FindProperty("startButton").objectReferenceValue = start;
        serialized.FindProperty("loadButton").objectReferenceValue = load;
        serialized.FindProperty("quitButton").objectReferenceValue = quit;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        root.SetActive(true);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        PrefabUtility.InstantiatePrefab(prefab, scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new InvalidOperationException("MainMenu 场景保存失败。");

        var scenes = EditorBuildSettings.scenes;
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
            .Concat(scenes.Where(item => item.path != ScenePath)).ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("MainMenu scene and prefab created. Scene list begins with MainMenu.");
    }

    static Texture2D Import(string file, bool alpha)
    {
        string path = Folder + "Art/" + file;
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaIsTransparency = alpha;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = file == "半熟生宣5k.png" ? 8192 : 4096;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static RawImage Image(Transform parent, string name, Texture texture)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        image.transform.SetParent(parent, false);
        image.texture = texture;
        image.raycastTarget = false;
        return image;
    }

    static RawImage Art(Transform parent, string name, string file, Rect bounds, Rect source)
    {
        var texture = Import(file, true);
        var image = Image(parent, name, texture);
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(bounds.x, -bounds.y);
        rect.sizeDelta = bounds.size;
        image.uvRect = new Rect(source.x / texture.width, 1 - source.yMax / texture.height,
            source.width / texture.width, source.height / texture.height);
        return image;
    }

    static Button MenuButton(Transform parent, string name, string file, Rect bounds)
    {
        var image = Art(parent, name, file, bounds, new Rect(805, 683, 1913, 711));
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        var colors = button.colors;
        colors.highlightedColor = new Color(.97f, .97f, .97f);
        colors.pressedColor = new Color(.94f, .94f, .94f);
        colors.disabledColor = Color.white;
        colors.selectedColor = Color.white;
        colors.fadeDuration = .08f;
        button.colors = colors;
        return button;
    }
}
