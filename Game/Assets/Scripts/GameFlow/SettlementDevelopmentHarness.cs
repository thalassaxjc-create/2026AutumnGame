using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Development-only stand-in for the unfinished order/settlement UI. In Editor or Development builds,
/// F9 simulates order completion and Enter/Esc confirms the return flow.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SettlementExitController))]
public sealed class SettlementDevelopmentHarness : MonoBehaviour
{
    [SerializeField] SettlementExitController exitController;
    [SerializeField] string testMessage = "Development settlement placeholder\nNo economy or inventory logic is performed.";

    GameObject testView;
    bool waitingForConfirmKeysRelease;

    void Awake()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (exitController == null)
            exitController = GetComponent<SettlementExitController>();
        EnsureView();
#endif
    }

    void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || exitController == null)
            return;

        if (!exitController.IsSettlementActive)
        {
            if (keyboard.f9Key.wasPressedThisFrame)
                SimulateOrderCompletion();
            return;
        }

        if (waitingForConfirmKeysRelease)
        {
            if (!keyboard.enterKey.isPressed && !keyboard.numpadEnterKey.isPressed &&
                !keyboard.spaceKey.isPressed && !keyboard.escapeKey.isPressed)
                waitingForConfirmKeysRelease = false;
            return;
        }

        if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame ||
            keyboard.spaceKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
            exitController.ConfirmAndExit();
#endif
    }

    [ContextMenu("Simulate Order Completion")]
    public void SimulateOrderCompletion()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (exitController == null)
            exitController = GetComponent<SettlementExitController>();
        EnsureView();
        if (exitController != null && exitController.BeginSettlement(testView))
            waitingForConfirmKeysRelease = true;
#else
        Debug.LogWarning("SettlementDevelopmentHarness is disabled in non-development builds.", this);
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    void EnsureView()
    {
        if (testView != null)
            return;

        testView = new GameObject("SettlementDevelopmentCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        testView.transform.SetParent(transform, false);

        Canvas canvas = testView.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 330;

        CanvasScaler scaler = testView.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = testView.GetComponent<RectTransform>();
        Image dimmer = CreateImage(canvasRect, "Dimmer", Vector2.zero, Vector2.one,
            new Color(0f, 0f, 0f, 0.62f));
        dimmer.rectTransform.offsetMin = Vector2.zero;
        dimmer.rectTransform.offsetMax = Vector2.zero;

        Image panel = CreateImage(canvasRect, "SettlementPanel", new Vector2(0.28f, 0.22f),
            new Vector2(0.72f, 0.78f), new Color(0.075f, 0.055f, 0.04f, 0.98f));
        panel.rectTransform.offsetMin = Vector2.zero;
        panel.rectTransform.offsetMax = Vector2.zero;

        GameObject labelObject = new GameObject("SettlementText", typeof(RectTransform), typeof(Text));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(panel.transform, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(55f, 45f);
        labelRect.offsetMax = new Vector2(-55f, -45f);

        Text label = labelObject.GetComponent<Text>();
        label.fontSize = 30;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(0.96f, 0.88f, 0.72f, 1f);
        label.raycastTarget = false;
        label.text = testMessage + "\n\nPress Enter, Space, or Esc to close\nPressing repeatedly is safely ignored.";
        InteractionPromptUI.EnsureChineseFont(label);

        testView.SetActive(false);
    }

    static Image CreateImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
#endif
}
