using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 门口交互提示。只在玩家进入 Trigger 时显示，不跨场景持久化。
/// </summary>
[DisallowMultipleComponent]
public class InteractionPromptUI : MonoBehaviour
{
    static InteractionPromptUI instance;

    [SerializeField] Text label;
    [SerializeField] CanvasGroup canvasGroup;

    void Awake()
    {
        instance = this;
        EnsureLabel();
        HideImmediate();
    }

    void OnEnable()
    {
        instance = this;
    }

    void OnDisable()
    {
        if (instance == this)
            instance = null;
    }

    public static void Show(string message)
    {
        if (instance == null)
            return;
        instance.EnsureLabel();
        instance.label.text = message;
        SetVisible(true);
    }

    public static void Hide()
    {
        SetVisible(false);
    }

    public static void HideImmediate()
    {
        SetVisible(false);
    }

    static void SetVisible(bool visible)
    {
        if (instance == null)
            return;

        instance.EnsureLabel();
        if (instance.canvasGroup != null)
        {
            instance.canvasGroup.alpha = visible ? 1f : 0f;
            instance.canvasGroup.blocksRaycasts = false;
            instance.canvasGroup.interactable = false;
        }

        if (instance.label != null)
            instance.label.enabled = visible;
    }

    void EnsureLabel()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (label != null)
        {
            EnsureChineseFont(label);
            return;
        }

        label = GetComponentInChildren<Text>(true);
        if (label != null)
            EnsureChineseFont(label);
    }

    public static void EnsureChineseFont(Text text)
    {
        if (text == null)
            return;

        Font font = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei", "微软雅黑", "SimHei", "黑体", "Arial Unicode MS", "Arial" },
            28);
        if (font != null)
            text.font = font;
    }
}
