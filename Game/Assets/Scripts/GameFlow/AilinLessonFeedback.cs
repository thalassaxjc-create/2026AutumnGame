using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Short remarks from Ailin that drift across the lesson screen.</summary>
public sealed class AilinDanmaku : MonoBehaviour
{
    public static AilinDanmaku Current { get; private set; }
    RectTransform area;
    string[] lines;
    float next;
    int index;

    public static void Play(Transform parent, params string[] lines)
    {
        if (parent == null || lines == null || lines.Length == 0 || Current != null)
            return;
        var go = new GameObject("艾琳弹幕", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var view = go.AddComponent<AilinDanmaku>();
        view.area = rect;
        view.lines = lines;
        view.next = .35f;
        Current = view;
    }

    public static void Stop()
    {
        if (Current == null)
            return;
        var go = Current.gameObject;
        Current = null;
        Destroy(go);
    }

    void OnDestroy()
    {
        if (Current == this)
            Current = null;
    }

    void Update()
    {
        next -= Time.deltaTime;
        if (next > 0f || lines == null || lines.Length == 0)
            return;
        next = 1.7f;
        Spawn(lines[index % lines.Length]);
        index++;
    }

    void Spawn(string line)
    {
        var text = WorkshopStoryUI.Label(area, "弹幕", line, new Rect(1.02f, .62f, .42f, .08f),
            30, new Color32(255, 244, 214, 255), TextAnchor.MiddleLeft);
        var mark = text.gameObject.AddComponent<AilinDanmakuMark>();
        mark.Begin(area, .58f + (index % 3) * .1f);
    }
}

/// <summary>Moves one remark from the right edge to the left.</summary>
public sealed class AilinDanmakuMark : MonoBehaviour
{
    RectTransform rect;
    RectTransform area;
    float y;
    float traveled;

    public void Begin(RectTransform parent, float normalizedY)
    {
        rect = (RectTransform)transform;
        area = parent;
        y = normalizedY;
        rect.anchorMin = rect.anchorMax = new Vector2(1.05f, y);
        rect.pivot = new Vector2(0f, .5f);
        rect.sizeDelta = new Vector2(520f, 46f);
        rect.anchoredPosition = Vector2.zero;
    }

    void Update()
    {
        if (rect == null || area == null)
            return;
        traveled += 280f * Time.deltaTime;
        rect.anchoredPosition = new Vector2(-traveled, 0f);
        if (traveled > area.rect.width + 620f)
            Destroy(gameObject);
    }
}

/// <summary>One line of feedback for Ailin. There is nothing to choose.</summary>
public sealed class AilinLessonTalk : MonoBehaviour
{
    public static AilinLessonTalk Current { get; private set; }
    public static bool IsOpen => Current != null;
    Action closed;

    public static void Show(string line, Action onClosed)
    {
        if (Current != null)
            return;
        AilinDanmaku.Stop();
        var root = new GameObject("艾琳的点评");
        var talk = root.AddComponent<AilinLessonTalk>();
        talk.closed = onClosed;
        Current = talk;
        GameFlowModalState.Acquire(talk);
        WorkshopStoryUI.EnsureEventSystem(root.transform);
        WorkshopStoryUI.ConfigureOverlay(root, 320);
        var plate = WorkshopStoryUI.Panel(root.transform, "点评纸", new Rect(.2f, .34f, .6f, .32f), WorkshopStoryUI.Paper).transform;
        WorkshopStoryUI.Label(plate, "点评正文", line, new Rect(.08f, .28f, .84f, .62f),
            34, WorkshopStoryUI.Ink, TextAnchor.MiddleLeft);
        WorkshopStoryUI.Label(plate, "点评继续", "按 Enter 继续", new Rect(.08f, .08f, .84f, .16f),
            24, new Color32(90, 78, 58, 255), TextAnchor.MiddleLeft);
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        bool key = keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame
            || keyboard.escapeKey.wasPressedThisFrame);
        bool click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        if (key || click)
            Close();
    }

    void Close()
    {
        if (Current == this)
            Current = null;
        GameFlowModalState.Release(this);
        Action done = closed;
        closed = null;
        done?.Invoke();
        Destroy(gameObject);
    }
}
