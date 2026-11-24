using UnityEngine;

[DisallowMultipleComponent]
public sealed class PatternStationTrigger : MonoBehaviour
{
    const string Prompt = "按 E 使用纹样台";
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
        if (IsOpen)
        {
            if (SceneTransitionState.IsLoading || order?.CanUsePatternStation != true)
                Close(false);
            return;
        }
        if (player == null)
            player = FindFirstObjectByType<PlayerController>();
        bool inRange = order?.CanUsePatternStation == true && player != null && player.isActiveAndEnabled &&
            !SceneTransitionState.IsLocked && !GameFlowModalState.IsInteractionBlocked && InRange();
        if (!inRange)
        {
            HidePrompt();
            return;
        }
        InteractionPromptUI.Show(Prompt);
        if (SceneTransitionManager.TryConsumeInteractPress())
            Open(order);
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

    static void HidePrompt()
    {
        if (InteractionPromptUI.CurrentMessage == Prompt)
            InteractionPromptUI.Hide();
    }
}
