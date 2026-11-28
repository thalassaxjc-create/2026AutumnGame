using UnityEngine;

[DisallowMultipleComponent]
public sealed class PatternStationTrigger : MonoBehaviour
{
    const string Prompt = "按 E 在纹样台选样";
    [SerializeField, Min(.2f)] float interactionDistance = 1.8f;
    PlayerController player;
    CurtainPatternView view;
    bool playerWasEnabled;
    bool cursorWasVisible;
    CursorLockMode previousCursorLock;

    public bool IsOpen { get; private set; }

    void Update()
    {
        WorkshopOrder order = WorkshopOrder.Current;
        bool available = WorkshopPracticeMode.Enabled || order?.CanUsePatternStation == true;
        if (IsOpen)
        {
            if (SceneTransitionState.IsLoading || !available)
                Close(false);
            return;
        }
        if (player == null)
            player = FindFirstObjectByType<PlayerController>();
        bool inRange = available && player != null && player.isActiveAndEnabled &&
            !SceneTransitionState.IsLocked && !GameFlowModalState.IsInteractionBlocked && InRange();
        if (!inRange)
        {
            HidePrompt();
            return;
        }
        InteractionPromptUI.Show(CurrentPrompt());
        if (SceneTransitionManager.TryConsumeInteractPress())
            Open(WorkshopPracticeMode.Enabled ? null : order);
    }

    public void OpenForPractice()
    {
        if (WorkshopPracticeMode.Enabled && !SceneTransitionState.IsLocked && !GameFlowModalState.IsInteractionBlocked)
        {
            if (player == null)
                player = FindFirstObjectByType<PlayerController>();
            if (player != null)
                Open(null);
        }
    }

    bool InRange()
    {
        Vector3 displacement = player.transform.position - transform.position;
        displacement.y = 0f;
        return displacement.sqrMagnitude <= interactionDistance * interactionDistance;
    }

    void Open(WorkshopOrder order)
    {
        if (view == null)
        {
            var root = new GameObject("纹样台界面", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            view = root.AddComponent<CurtainPatternView>();
        }
        WorkshopStoryUI.EnsureEventSystem(transform);
        IsOpen = true;
        playerWasEnabled = player.enabled;
        previousCursorLock = Cursor.lockState;
        cursorWasVisible = Cursor.visible;
        GameFlowModalState.Acquire(this);
        player.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        view.Open(order, () => Close(true));
    }

    void Close(bool waitForInteractRelease)
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        if (view != null)
            view.Close();
        if (player != null)
            player.enabled = playerWasEnabled;
        Cursor.lockState = previousCursorLock;
        Cursor.visible = cursorWasVisible;
        GameFlowModalState.Release(this, waitForInteractRelease);
    }

    void OnDisable()
    {
        HidePrompt();
        Close(false);
    }

    static string CurrentPrompt()
    {
        WorkshopOrder order = WorkshopOrder.Current;
        return order != null && order.IsTeachingDay && order.CurrentLesson == WorkshopOrder.LessonBeat.Pattern
            ? "按 E 教艾琳扎花"
            : Prompt;
    }

    static void HidePrompt()
    {
        if (InteractionPromptUI.CurrentMessage == Prompt || InteractionPromptUI.CurrentMessage == "按 E 教艾琳扎花")
            InteractionPromptUI.Hide();
    }
}
