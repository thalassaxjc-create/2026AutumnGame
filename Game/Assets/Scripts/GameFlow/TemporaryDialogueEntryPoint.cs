using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Development-only dialogue placeholder. Replace this component with a MonoBehaviour that implements
/// IDialogueEntryPoint when the real dialogue/order system is ready.
/// </summary>
[DisallowMultipleComponent]
public sealed class TemporaryDialogueEntryPoint : MonoBehaviour, IDialogueEntryPoint
{
    [SerializeField, TextArea(2, 5)]
    string placeholderMessage = "Temporary dialogue entry point.\nThe order/dialogue system can be connected here.";
    [SerializeField] string closeHint = "Press E, Enter, or Esc to close";

    GameObject dialogueRoot;
    Text messageLabel;
    Action closedCallback;
    InputAction interactAction;
    bool isOpen;
    bool waitingForOpeningPressRelease;
    WorkshopOrder.Phase? storyPhase;

    public bool IsOpen => isOpen;

    public void ConfigureStory(WorkshopOrder.Phase phase, string message)
    {
        storyPhase = phase;
        placeholderMessage = message;
        closeHint = "按 E、Enter 或 Esc 继续";
    }

    void Update()
    {
        if (!isOpen)
            return;

        CacheInteractAction();
        if (waitingForOpeningPressRelease)
        {
            if (!IsInteractPressed())
                waitingForOpeningPressRelease = false;
            return;
        }

        Keyboard keyboard = Keyboard.current;
        bool closeRequested = WasInteractPressedThisFrame();
        if (keyboard != null)
        {
            closeRequested |= keyboard.enterKey.wasPressedThisFrame;
            closeRequested |= keyboard.numpadEnterKey.wasPressedThisFrame;
            closeRequested |= keyboard.escapeKey.wasPressedThisFrame;
        }

        if (closeRequested)
            CloseDialogue();
    }

    public bool TryBeginDialogue(NpcDialogueInteraction source, Action onDialogueClosed)
    {
        if (isOpen)
            return false;

        EnsureView();
        closedCallback = onDialogueClosed;
        messageLabel.text = placeholderMessage + "\n\n" + closeHint;
        dialogueRoot.SetActive(true);
        isOpen = true;

        CacheInteractAction();
        waitingForOpeningPressRelease = IsInteractPressed();
        return true;
    }

    public void CloseDialogue() => CloseDialogue(true);

    void CloseDialogue(bool confirmed)
    {
        if (!isOpen)
            return;

        isOpen = false;
        waitingForOpeningPressRelease = false;
        if (dialogueRoot != null)
            dialogueRoot.SetActive(false);

        Action callback = closedCallback;
        closedCallback = null;
        callback?.Invoke();
        if (confirmed && storyPhase.HasValue)
            WorkshopOrder.Current?.CloseIntroduction(storyPhase.Value);
    }

    void CacheInteractAction()
    {
        if (interactAction != null || InputSystem.actions == null)
            return;

        interactAction = InputSystem.actions.FindAction("Player/Interact", false);
        if (interactAction != null && !interactAction.enabled)
            interactAction.Enable();
    }

    bool IsInteractPressed()
    {
        if (interactAction != null)
            return interactAction.IsPressed();
        return Keyboard.current != null && Keyboard.current.eKey.isPressed;
    }

    bool WasInteractPressedThisFrame()
    {
        if (interactAction != null)
            return interactAction.WasPressedThisFrame();
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
    }

    void EnsureView()
    {
        if (dialogueRoot != null)
            return;

        dialogueRoot = new GameObject("TemporaryDialogueCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        dialogueRoot.transform.SetParent(transform, false);

        Canvas canvas = dialogueRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 320;

        CanvasScaler scaler = dialogueRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = dialogueRoot.GetComponent<RectTransform>();
        Image dimmer = CreateImage(canvasRect, "Dimmer", Vector2.zero, Vector2.one,
            new Color(0f, 0f, 0f, 0.48f));
        dimmer.rectTransform.offsetMin = Vector2.zero;
        dimmer.rectTransform.offsetMax = Vector2.zero;

        Image panel = CreateImage(canvasRect, "DialoguePanel", new Vector2(0.18f, 0.08f),
            new Vector2(0.82f, 0.36f), new Color(0.08f, 0.065f, 0.05f, 0.98f));
        panel.rectTransform.offsetMin = Vector2.zero;
        panel.rectTransform.offsetMax = Vector2.zero;

        GameObject labelObject = new GameObject("Message", typeof(RectTransform), typeof(Text));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(panel.transform, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(48f, 34f);
        labelRect.offsetMax = new Vector2(-48f, -34f);

        messageLabel = labelObject.GetComponent<Text>();
        messageLabel.fontSize = 30;
        messageLabel.alignment = TextAnchor.MiddleCenter;
        messageLabel.color = new Color(0.95f, 0.9f, 0.8f, 1f);
        messageLabel.raycastTarget = false;
        InteractionPromptUI.EnsureChineseFont(messageLabel);

        dialogueRoot.SetActive(false);
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

    void OnDisable()
    {
        if (isOpen)
            CloseDialogue(false);
    }
}
