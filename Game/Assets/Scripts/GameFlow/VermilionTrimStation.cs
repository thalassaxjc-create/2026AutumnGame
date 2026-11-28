using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SettlementExitController))]
public sealed class VermilionTrimStation : MonoBehaviour
{
    const string Prompt = "按 E 在上漆边台选样";
    [SerializeField, Min(.2f)] float interactionDistance = 1.45f;
    PlayerController player;
    SettlementExitController modal;
    VermilionTrimView view;

    public bool IsOpen => modal != null && modal.IsSettlementActive;

    void Awake() => modal = GetComponent<SettlementExitController>();

    void Start() => player = FindFirstObjectByType<PlayerController>();

    void Update()
    {
        WorkshopOrder order = WorkshopOrder.Current;
        bool available = WorkshopPracticeMode.Enabled || order?.CanUseTrimStation == true;
        if (IsOpen)
        {
            if (SceneTransitionState.IsLoading || !available)
                modal.CloseAndResume();
            return;
        }
        bool canOpen = available && player != null && player.isActiveAndEnabled &&
            !SceneTransitionState.IsLocked && !GameFlowModalState.IsInteractionBlocked && InRange();
        if (!canOpen)
        {
            HidePrompt();
            return;
        }
        InteractionPromptUI.Show(Prompt);
        if (SceneTransitionManager.TryConsumeInteractPress())
            Open(WorkshopPracticeMode.Enabled ? null : order);
    }

    public void OpenForPractice()
    {
        if (WorkshopPracticeMode.Enabled && !SceneTransitionState.IsLocked && !GameFlowModalState.IsInteractionBlocked)
            Open(null);
    }

    bool InRange()
    {
        Vector3 delta = player.transform.position - transform.position;
        delta.y = 0f;
        return delta.sqrMagnitude <= interactionDistance * interactionDistance;
    }

    void Open(WorkshopOrder order)
    {
        if (view == null)
        {
            var root = new GameObject("上漆边台界面", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            view = root.AddComponent<VermilionTrimView>();
            root.SetActive(false);
        }
        WorkshopStoryUI.EnsureEventSystem(transform);
        if (modal.BeginSettlement(view.gameObject))
            view.Open(order, modal.CloseAndResume);
    }

    void OnDisable()
    {
        HidePrompt();
        if (modal != null)
            modal.CloseAndResume();
    }

    static void HidePrompt()
    {
        if (InteractionPromptUI.CurrentMessage == Prompt)
            InteractionPromptUI.Hide();
    }
}
