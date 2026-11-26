using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>轻量剧情对话：支持多句、两项选择与生产阶段闲聊。</summary>
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
    string[] storyMessages = Array.Empty<string>();
    string[] storyChoices = Array.Empty<string>();
    string[] casualMessages = Array.Empty<string>();
    string[] activeMessages = Array.Empty<string>();
    bool activeCompletesStory;
    bool isOpen;
    bool waitingForOpeningPressRelease;
    bool awaitingChoice;
    int messageIndex;
    int selectedChoice;
    WorkshopOrder.Phase? storyPhase;

    public bool IsOpen => isOpen;
    public bool IsAwaitingChoice => awaitingChoice;
    public int MessageIndex => messageIndex;
    public int SelectedChoice => selectedChoice;

    public void ConfigureStory(WorkshopOrder.Phase phase, string message) =>
        ConfigureStory(phase, new[] { message });

    public void ConfigureStory(WorkshopOrder.Phase phase, string[] messages, string[] choices = null)
    {
        storyPhase = phase;
        storyMessages = messages ?? Array.Empty<string>();
        storyChoices = choices ?? Array.Empty<string>();
        closeHint = "E / Enter 继续    Esc 跳过";
    }

    public void ConfigureCasual(params string[] messages) => casualMessages = messages ?? Array.Empty<string>();

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
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            CloseDialogue();
            return;
        }

        if (awaitingChoice)
        {
            if (keyboard != null && (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame))
            {
                selectedChoice = 0;
                ConfirmChoice();
                return;
            }
            if (keyboard != null && storyChoices.Length > 1 &&
                (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame))
            {
                selectedChoice = 1;
                ConfirmChoice();
                return;
            }
            if (keyboard != null && (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame ||
                keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame))
            {
                selectedChoice = storyChoices.Length <= 1 ? 0 : 1 - selectedChoice;
                RenderCurrent();
                return;
            }
            if (WasContinuePressed(keyboard))
                ConfirmChoice();
            return;
        }

        if (WasContinuePressed(keyboard))
            AdvanceDialogue();
    }

    public bool TryBeginDialogue(NpcDialogueInteraction source, Action onDialogueClosed)
    {
        if (isOpen)
            return false;
        EnsureView();
        var order = WorkshopOrder.Current;
        bool casual = casualMessages.Length > 0 && order?.CanExplore == true && order.CurrentPhase != storyPhase;
        activeMessages = casual ? casualMessages : storyMessages.Length > 0 ? storyMessages : new[] { placeholderMessage };
        activeCompletesStory = !casual;
        messageIndex = 0;
        selectedChoice = 0;
        awaitingChoice = false;
        closedCallback = onDialogueClosed;
        RenderCurrent();
        dialogueRoot.SetActive(true);
        isOpen = true;
        CacheInteractAction();
        waitingForOpeningPressRelease = IsInteractPressed();
        return true;
    }

    void AdvanceDialogue()
    {
        if (messageIndex + 1 < activeMessages.Length)
        {
            messageIndex++;
            RenderCurrent();
            return;
        }
        if (activeCompletesStory && storyChoices.Length > 0)
        {
            awaitingChoice = true;
            RenderCurrent();
            return;
        }
        CloseDialogue();
    }

    void ConfirmChoice()
    {
        if (!awaitingChoice)
            return;
        awaitingChoice = false;
        CloseDialogue();
    }

    void RenderCurrent()
    {
        if (messageLabel == null)
            return;
        if (!awaitingChoice)
        {
            string message = activeMessages.Length == 0 ? placeholderMessage :
                activeMessages[Mathf.Clamp(messageIndex, 0, activeMessages.Length - 1)];
            messageLabel.text = message + "\n\n<size=22>" + closeHint + "</size>";
            return;
        }

        string options = "请选择：\n";
        for (int i = 0; i < storyChoices.Length; i++)
            options += (i == selectedChoice ? "▶ " : "   ") + $"[{i + 1}] {storyChoices[i]}\n";
        messageLabel.text = options + "\n<size=22>W/S 或方向键选择，E / Enter 确认</size>";
    }

    public void CloseDialogue() => CloseDialogue(true);

    void CloseDialogue(bool confirmed)
    {
        if (!isOpen)
            return;
        isOpen = false;
        waitingForOpeningPressRelease = false;
        awaitingChoice = false;
        if (dialogueRoot != null)
            dialogueRoot.SetActive(false);
        Action callback = closedCallback;
        closedCallback = null;
        callback?.Invoke();
        if (confirmed && activeCompletesStory && storyPhase.HasValue)
            WorkshopOrder.Current?.CloseIntroduction(storyPhase.Value, selectedChoice);
    }

    void CacheInteractAction()
    {
        if (interactAction != null || InputSystem.actions == null)
            return;
        interactAction = InputSystem.actions.FindAction("Player/Interact", false);
        if (interactAction != null && !interactAction.enabled)
            interactAction.Enable();
    }

    bool IsInteractPressed() => interactAction != null ? interactAction.IsPressed() :
        Keyboard.current != null && Keyboard.current.eKey.isPressed;

    bool WasContinuePressed(Keyboard keyboard)
    {
        bool pressed = interactAction != null ? interactAction.WasPressedThisFrame() :
            keyboard != null && keyboard.eKey.wasPressedThisFrame;
        return pressed || keyboard != null && (keyboard.enterKey.wasPressedThisFrame ||
            keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame);
    }

    void EnsureView()
    {
        if (dialogueRoot != null)
            return;
        dialogueRoot = new GameObject("StoryDialogueCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        dialogueRoot.transform.SetParent(transform, false);
        Canvas canvas = dialogueRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 320;
        CanvasScaler scaler = dialogueRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;

        RectTransform canvasRect = dialogueRoot.GetComponent<RectTransform>();
        Image dimmer = CreateImage(canvasRect, "Dimmer", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, .48f));
        dimmer.rectTransform.offsetMin = dimmer.rectTransform.offsetMax = Vector2.zero;
        Image panel = CreateImage(canvasRect, "DialoguePanel", new Vector2(.14f, .07f), new Vector2(.86f, .39f),
            new Color(.08f, .065f, .05f, .98f));
        panel.rectTransform.offsetMin = panel.rectTransform.offsetMax = Vector2.zero;

        var labelObject = new GameObject("Message", typeof(RectTransform), typeof(Text));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(panel.transform, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(48f, 30f);
        labelRect.offsetMax = new Vector2(-48f, -30f);
        messageLabel = labelObject.GetComponent<Text>();
        messageLabel.fontSize = 30;
        messageLabel.alignment = TextAnchor.MiddleCenter;
        messageLabel.color = new Color(.95f, .9f, .8f, 1f);
        messageLabel.raycastTarget = false;
        messageLabel.supportRichText = true;
        InteractionPromptUI.EnsureChineseFont(messageLabel);
        dialogueRoot.SetActive(false);
    }

    static Image CreateImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
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
