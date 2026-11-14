using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 每个场景各自一份。负责启用 Interact、消费按键、加载场景并应用出生点。
/// </summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public class SceneTransitionManager : MonoBehaviour
{
    InputAction interactAction;
    bool subscribedToAction;

    void Awake()
    {
        CacheInteractAction();
        ApplyPendingSpawn();
    }

    void OnEnable()
    {
        CacheInteractAction();
        BindInteract(true);
    }

    void OnDisable()
    {
        BindInteract(false);
        InteractionPromptUI.HideImmediate();
    }

    void Update()
    {
        if (SceneTransitionState.WaitForInteractRelease && !IsInteractHeld())
            SceneTransitionState.WaitForInteractRelease = false;

        if (!SceneTransitionState.MustLeaveTrigger)
            return;

        PlayerController player = FindUniquePlayer();
        if (player != null && !IsInsideAnyDoor(player.transform))
            SceneTransitionState.MustLeaveTrigger = false;
    }

    public static bool TryConsumeInteractPress()
    {
        if (SceneTransitionState.IsLocked)
            return false;

        SceneTransitionManager manager = FindInstance();
        if (manager == null)
            return false;
        return manager.WasInteractPressedThisFrame();
    }

    public static void Request(string sceneName, string spawnId)
    {
        if (SceneTransitionState.IsLoading)
            return;

        if (string.IsNullOrEmpty(sceneName) || string.IsNullOrEmpty(spawnId))
        {
            Debug.LogError("场景切换缺少目标场景或出生点。");
            return;
        }

        if (!TryGetBuildIndex(sceneName, out int buildIndex, out string error))
        {
            Debug.LogError(error);
            SceneTransitionState.FailLoad();
            return;
        }

        InteractionPromptUI.HideImmediate();
        SceneTransitionState.BeginLoad(spawnId);
        SceneTransitionFader.Go(buildIndex);
    }

    void ApplyPendingSpawn()
    {
        string spawnId = SceneTransitionState.ConsumePendingSpawnId();
        if (string.IsNullOrEmpty(spawnId))
            return;

        PlayerController player = FindUniquePlayer();
        if (player == null)
            return;

        SceneSpawnPoint spawn = FindSpawn(spawnId);
        if (spawn == null)
        {
            Debug.LogError("找不到出生点: " + spawnId);
            return;
        }

        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = false;

        player.transform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);
        Physics.SyncTransforms();

        if (controller != null)
        {
            controller.enabled = true;
            controller.Move(Vector3.zero);
        }

        CameraFollow follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (follow != null)
            follow.SnapToTarget();
        else
            Debug.LogWarning("新场景没有可用的 CameraFollow。");

        Physics.SyncTransforms();
        SceneTransitionState.MustLeaveTrigger = true;
        SceneTransitionState.WaitForInteractRelease = false;
    }

    void CacheInteractAction()
    {
        if (interactAction != null || InputSystem.actions == null)
            return;

        interactAction = InputSystem.actions.FindAction("Player/Interact", false);
        if (interactAction == null)
        {
            Debug.LogError("InputSystem_Actions 中找不到 Player/Interact。");
            return;
        }

        if (!interactAction.enabled)
            interactAction.Enable();
    }

    void BindInteract(bool enable)
    {
        CacheInteractAction();
        if (interactAction == null)
            return;

        if (enable)
        {
            if (!interactAction.enabled)
                interactAction.Enable();
            subscribedToAction = true;
        }
        else
        {
            subscribedToAction = false;
        }
    }

    bool WasInteractPressedThisFrame()
    {
        CacheInteractAction();
        return interactAction != null && interactAction.WasPressedThisFrame();
    }

    bool IsInteractHeld()
    {
        CacheInteractAction();
        return interactAction != null && interactAction.IsPressed();
    }

    static bool TryGetBuildIndex(string sceneName, out int buildIndex, out string error)
    {
        buildIndex = -1;
        error = null;
        string path = sceneName == SceneTransitionState.WorkshopSceneName
            ? SceneTransitionState.WorkshopScenePath
            : sceneName == SceneTransitionState.HomeSceneName
                ? SceneTransitionState.HomeScenePath
                : sceneName == SceneTransitionState.WorldSceneName
                    ? SceneTransitionState.WorldScenePath
                    : null;

        if (path != null)
            buildIndex = SceneUtility.GetBuildIndexByScenePath(path);

        if (buildIndex < 0)
            buildIndex = SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/" + sceneName + ".unity");

        if (buildIndex < 0)
        {
            error = "场景未加入 Build Settings 或未启用: " + sceneName;
            return false;
        }

        return true;
    }

    static PlayerController FindUniquePlayer()
    {
        PlayerController[] players = Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (players.Length == 0)
        {
            Debug.LogError("当前场景找不到 PlayerController。");
            return null;
        }

        if (players.Length > 1)
            Debug.LogError("当前场景存在多个 Player: " + players.Length);

        return players[0];
    }

    static SceneSpawnPoint FindSpawn(string spawnId)
    {
        SceneSpawnPoint[] points = Object.FindObjectsByType<SceneSpawnPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        SceneSpawnPoint match = null;
        int count = 0;
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] == null || points[i].SpawnId != spawnId)
                continue;
            match = points[i];
            count++;
        }

        if (count > 1)
            Debug.LogError("出生点 ID 重复: " + spawnId);
        return match;
    }

    static bool IsInsideAnyDoor(Transform player)
    {
        DoorTransition[] doors = Object.FindObjectsByType<DoorTransition>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] != null && doors[i].OverlapsPlayer(player))
                return true;
        }

        return false;
    }

    static SceneTransitionManager FindInstance()
    {
        return Object.FindFirstObjectByType<SceneTransitionManager>();
    }

    public bool InteractEnabled => interactAction != null && interactAction.enabled;
    public bool HasInteractBinding => interactAction != null;
    public bool Subscribed => subscribedToAction;
}
