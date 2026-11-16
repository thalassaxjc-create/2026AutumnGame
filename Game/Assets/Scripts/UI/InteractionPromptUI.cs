using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 场景内共用的交互提示，不跨场景持久化。
/// </summary>
[DisallowMultipleComponent]
public class InteractionPromptUI : MonoBehaviour
{
    static InteractionPromptUI instance;
    static Font sharedChineseFont;

    public static bool IsVisible => instance != null && instance.label != null && instance.label.enabled;
    public static string CurrentMessage => IsVisible ? instance.label.text : string.Empty;

    [SerializeField] Text label;
    [SerializeField] CanvasGroup canvasGroup;

    void Awake()
    {
        instance = this;
        EnsureLabel();
        EnsureChineseFont(label);
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
        if (instance == null || GameFlowModalState.IsInteractionBlocked)
            return;
        instance.EnsureLabel();
        if (instance.label == null)
            return;

        EnsureChineseFont(instance.label);
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
        if (label == null)
            label = GetComponentInChildren<Text>(true);
    }

    public static void EnsureChineseFont(Text text)
    {
        if (text == null)
            return;

        // 所有提示、NPC 名字和背包文字共用一个字体。这个 API 每次都会创建原生资源，
        // 不能放在每帧 Show/Hide 的路径上反复创建。
        if (sharedChineseFont == null)
        {
            sharedChineseFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei", "微软雅黑", "SimHei", "黑体", "Arial Unicode MS", "Arial" },
                28);
            if (sharedChineseFont == null)
                return;

            sharedChineseFont.hideFlags = HideFlags.HideAndDontSave;

#if UNITY_EDITOR
            // 场景生成工具也会调用此方法；在静态引用丢失前释放编辑器中的原生字体。
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ReleaseChineseFont;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ReleaseChineseFont;
            UnityEditor.EditorApplication.quitting -= ReleaseChineseFont;
            UnityEditor.EditorApplication.quitting += ReleaseChineseFont;
#else
            Application.quitting -= ReleaseChineseFont;
            Application.quitting += ReleaseChineseFont;
#endif
        }

        if (text.font != sharedChineseFont)
            text.font = sharedChineseFont;
    }

    static void ReleaseChineseFont()
    {
        if (sharedChineseFont == null)
            return;

#if UNITY_EDITOR
        DestroyImmediate(sharedChineseFont);
#else
        Destroy(sharedChineseFont);
#endif
        sharedChineseFont = null;
    }
}
