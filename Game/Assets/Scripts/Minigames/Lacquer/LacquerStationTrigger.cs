using UnityEngine;

/// <summary>
/// 刷大漆工作台的世界交互入口。负责玩家检测、交互提示与移动锁定；
/// 小游戏流程由 LacquerMinigameController 负责。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed class LacquerStationTrigger : MonoBehaviour
{
    [SerializeField] GameObject minigameCanvas;
    [SerializeField] LacquerMinigameController minigameController;
    [SerializeField] string promptText = "按 E 开始刷大漆";

    PlayerController playerInRange;
    CharacterController playerBody;
    BoxCollider stationArea;
    bool minigameOpen;
    bool playerWasEnabled;
    WorkshopOrder order;
    bool IsAvailable => WorkshopOrder.Current?.CanPlay(WorkshopOrder.Stage.Lacquer) ?? true;

    public bool IsMinigameOpen => minigameOpen;

    void Awake()
    {
        stationArea = GetComponent<BoxCollider>();
        ResolveController();
        if (minigameCanvas != null)
            minigameCanvas.SetActive(false);
    }

    void Update()
    {
        // Collider disable/teleport can bypass OnTriggerExit. Check the existing
        // trigger bounds before letting a stale registration consume Interact.
        if (playerInRange != null && !ContainsPlayer())
            ClearPlayer();
        if (minigameOpen || playerInRange == null || !IsAvailable || GameFlowModalState.IsInteractionBlocked)
            return;

        InteractionPromptUI.Show(promptText);
        if (SceneTransitionManager.TryConsumeInteractPress())
            OpenMinigame();
    }

    void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
            return;

        playerInRange = player;
        playerBody = player.GetComponent<CharacterController>();
        if (!minigameOpen && IsAvailable && !GameFlowModalState.IsInteractionBlocked)
            InteractionPromptUI.Show(promptText);
    }

    void OnTriggerExit(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || player != playerInRange)
            return;

        ClearPlayer();
    }

    bool ContainsPlayer() =>
        playerBody != null && stationArea != null && stationArea.bounds.Intersects(playerBody.bounds);

    void ClearPlayer()
    {
        CloseMinigame(false);
        playerInRange = null;
        playerBody = null;
        if (InteractionPromptUI.CurrentMessage == promptText)
            InteractionPromptUI.Hide();
    }

    public void OpenMinigame()
    {
        if (minigameOpen || playerInRange == null || !ContainsPlayer() || GameFlowModalState.IsInteractionBlocked)
            return;

        ResolveController();
        if (minigameController == null && minigameCanvas == null)
        {
            Debug.LogError("刷大漆工作台没有配置小游戏控制器或 Canvas。", this);
            return;
        }

        order = WorkshopOrder.Current;
        if (order != null)
        {
            if (minigameController == null || !order.Begin(WorkshopOrder.Stage.Lacquer))
            {
                order = null;
                return;
            }
            minigameController.ResultConfirmed += order.ConfirmLacquer;
            GameFlowModalState.Acquire(this);
        }

        playerWasEnabled = playerInRange.enabled;
        minigameOpen = true;
        InteractionPromptUI.Hide();
        playerInRange.enabled = false;

        if (minigameController != null)
            minigameController.Open(CloseMinigame);
        else
            minigameCanvas.SetActive(true);
    }

    public void CloseMinigame() => CloseMinigame(true);

    void CloseMinigame(bool waitForInteractRelease)
    {
        if (!minigameOpen)
            return;

        minigameOpen = false;
        if (order != null)
        {
            if (minigameController != null)
                minigameController.ResultConfirmed -= order.ConfirmLacquer;
            order.Leave(WorkshopOrder.Stage.Lacquer);
            order = null;
            GameFlowModalState.Release(this, waitForInteractRelease);
        }

        if (minigameController != null)
            minigameController.Close();
        else if (minigameCanvas != null)
            minigameCanvas.SetActive(false);

        if (playerInRange != null)
            playerInRange.enabled = playerWasEnabled;
    }

    public void Configure(GameObject canvasRoot, LacquerMinigameController controller)
    {
        minigameCanvas = canvasRoot;
        minigameController = controller;
    }

    void ResolveController()
    {
        if (minigameController == null)
            minigameController = GetComponent<LacquerMinigameController>();
    }

    void OnDisable() => ClearPlayer();
}
