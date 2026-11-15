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
    bool minigameOpen;

    public bool IsMinigameOpen => minigameOpen;

    void Awake()
    {
        ResolveController();
        if (minigameCanvas != null)
            minigameCanvas.SetActive(false);
    }

    void Update()
    {
        if (minigameOpen || playerInRange == null)
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
        if (!minigameOpen)
            InteractionPromptUI.Show(promptText);
    }

    void OnTriggerExit(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || player != playerInRange)
            return;

        if (minigameOpen)
            CloseMinigame();

        playerInRange = null;
        InteractionPromptUI.Hide();
    }

    public void OpenMinigame()
    {
        if (playerInRange == null)
            return;

        ResolveController();
        if (minigameController == null && minigameCanvas == null)
        {
            Debug.LogError("编帘工作台没有配置小游戏控制器或 Canvas。", this);
            return;
        }

        minigameOpen = true;
        InteractionPromptUI.Hide();
        playerInRange.enabled = false;

        if (minigameController != null)
            minigameController.Open(CloseMinigame);
        else
            minigameCanvas.SetActive(true);
    }

    public void CloseMinigame()
    {
        if (!minigameOpen)
            return;

        minigameOpen = false;

        if (minigameController != null)
            minigameController.Close();
        else if (minigameCanvas != null)
            minigameCanvas.SetActive(false);

        if (playerInRange != null)
            playerInRange.enabled = true;
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
        InteractionPromptUI.Hide();

        if (minigameController != null)
            minigameController.Close();
        else if (minigameCanvas != null)
            minigameCanvas.SetActive(false);

        if (playerInRange != null)
            playerInRange.enabled = true;

        minigameOpen = false;
    }
}
