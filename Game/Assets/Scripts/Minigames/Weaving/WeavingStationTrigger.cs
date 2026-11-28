using UnityEngine;

/// <summary>
/// 编帘工作台的世界交互入口。负责玩家检测、交互提示与移动锁定；
/// 具体的音游流程交给 WeavingMinigameController。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed class WeavingStationTrigger : MonoBehaviour
{
    [SerializeField] GameObject minigameCanvas;
    [SerializeField] WeavingMinigameController minigameController;
    [SerializeField] string promptText = "按 E 开始编帘";

    PlayerController playerInRange;
    PlayerController activePlayer;
    CharacterController playerBody;
    BoxCollider stationArea;
    NpcDialogueInteraction xiaoPiBucket;
    bool minigameOpen;
    bool playerWasEnabled;
    bool directPractice;
    WorkshopOrder order;
    bool IsAvailable => WorkshopOrder.Current?.CanPlay(WorkshopOrder.Stage.Weaving) ?? true;
    bool BucketHasPriority => xiaoPiBucket != null && xiaoPiBucket.CanInteractFrom(playerInRange);

    public bool IsMinigameOpen => minigameOpen;

    void Awake()
    {
        stationArea = GetComponent<BoxCollider>();
        if (transform.parent != null)
            foreach (var dialogue in transform.parent.GetComponentsInChildren<VillageMarketDialogue>(true))
                if (dialogue.MerchantRole == VillageMarketDialogue.Role.XiaoPiBucket)
                {
                    xiaoPiBucket = dialogue.GetComponent<NpcDialogueInteraction>();
                    break;
                }
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
        if (minigameOpen || playerInRange == null || !IsAvailable || GameFlowModalState.IsInteractionBlocked || BucketHasPriority)
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
        if (!minigameOpen && IsAvailable && !GameFlowModalState.IsInteractionBlocked && !BucketHasPriority)
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
        if (!directPractice)
            CloseMinigame(false);
        playerInRange = null;
        playerBody = null;
        if (InteractionPromptUI.CurrentMessage == promptText)
            InteractionPromptUI.Hide();
    }

    public void OpenMinigame()
    {
        if (playerInRange != null && ContainsPlayer() && !BucketHasPriority)
            OpenFor(playerInRange, false);
    }

    public void OpenForPractice(int day = 1)
    {
        if (WorkshopPracticeMode.Enabled)
            OpenFor(FindFirstObjectByType<PlayerController>(), true, day);
    }

    void OpenFor(PlayerController player, bool practiceFromMenu, int practiceDay = 1)
    {
        if (minigameOpen || player == null || SceneTransitionState.IsLoading || GameFlowModalState.IsInteractionBlocked)
            return;

        ResolveController();
        if (minigameController == null && minigameCanvas == null)
        {
            Debug.LogError("编帘工作台没有配置小游戏控制器或 Canvas。", this);
            return;
        }

        order = WorkshopPracticeMode.Enabled ? null : WorkshopOrder.Current;
        if (order != null)
        {
            if (minigameController == null || !order.Begin(WorkshopOrder.Stage.Weaving))
            {
                order = null;
                return;
            }
            minigameController.ResultConfirmed += order.ConfirmWeaving;
        }

        directPractice = practiceFromMenu;
        activePlayer = player;
        playerWasEnabled = player.enabled;
        minigameOpen = true;
        GameFlowModalState.Acquire(this);
        player.enabled = false;

        if (minigameController != null)
            minigameController.Open(CloseMinigame, order?.Days ?? practiceDay, order?.HasAutomaticShuttle == true);
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
                minigameController.ResultConfirmed -= order.ConfirmWeaving;
            order.Leave(WorkshopOrder.Stage.Weaving);
            order = null;
        }

        if (minigameController != null)
            minigameController.Close();
        else if (minigameCanvas != null)
            minigameCanvas.SetActive(false);

        if (activePlayer != null)
            activePlayer.enabled = playerWasEnabled;
        activePlayer = null;
        directPractice = false;
        GameFlowModalState.Release(this, waitForInteractRelease);
    }

    public void Configure(GameObject canvasRoot, WeavingMinigameController controller)
    {
        minigameCanvas = canvasRoot;
        minigameController = controller;
    }

    void ResolveController()
    {
        if (minigameController == null)
            minigameController = GetComponent<WeavingMinigameController>();
    }

    void OnDisable()
    {
        CloseMinigame(false);
        ClearPlayer();
    }
}
