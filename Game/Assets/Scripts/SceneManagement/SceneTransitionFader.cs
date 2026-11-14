using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 跨场景黑场。切场景前先淡出，加载完成再淡入，避免画面卡住一帧。
/// </summary>
[DefaultExecutionOrder(-300)]
[DisallowMultipleComponent]
public class SceneTransitionFader : MonoBehaviour
{
    const float FadeOutSeconds = 0.12f;
    const float FadeInSeconds = 0.18f;

    static SceneTransitionFader instance;
    CanvasGroup group;
    bool running;

    public static void Go(int buildIndex)
    {
        Ensure().StartCoroutine(instance.Run(buildIndex));
    }

    public static void HideImmediate()
    {
        if (instance == null || instance.group == null)
            return;
        instance.group.alpha = 0f;
    }

    static SceneTransitionFader Ensure()
    {
        if (instance != null)
            return instance;

        var root = new GameObject("SceneTransitionFader");
        DontDestroyOnLoad(root);
        instance = root.AddComponent<SceneTransitionFader>();
        instance.BuildOverlay();
        return instance;
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        if (group == null)
            BuildOverlay();
    }

    IEnumerator Run(int buildIndex)
    {
        if (running)
            yield break;

        running = true;
        yield return FadeTo(1f, FadeOutSeconds);

        AsyncOperation operation = SceneManager.LoadSceneAsync(buildIndex);
        if (operation == null)
        {
            SceneTransitionState.FailLoad();
            yield return FadeTo(0f, FadeInSeconds);
            running = false;
            yield break;
        }

        operation.allowSceneActivation = true;
        while (!operation.isDone)
            yield return null;

        yield return null;
        yield return FadeTo(0f, FadeInSeconds);
        SceneTransitionState.FinishLoad();
        running = false;
    }

    IEnumerator FadeTo(float target, float duration)
    {
        if (group == null)
            yield break;

        float start = group.alpha;
        if (duration <= 0.001f)
        {
            group.alpha = target;
            yield break;
        }

        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(time / duration));
            yield return null;
        }

        group.alpha = target;
    }

    void BuildOverlay()
    {
        Canvas canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;

        if (gameObject.GetComponent<CanvasScaler>() == null)
        {
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        group = gameObject.GetComponent<CanvasGroup>();
        if (group == null)
            group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        group.ignoreParentGroups = true;

        Transform existing = transform.Find("Fade");
        GameObject fadeGo = existing != null ? existing.gameObject : new GameObject("Fade");
        fadeGo.transform.SetParent(transform, false);
        Image image = fadeGo.GetComponent<Image>();
        if (image == null)
            image = fadeGo.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
