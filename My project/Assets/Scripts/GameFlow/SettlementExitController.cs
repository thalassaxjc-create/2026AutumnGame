using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public enum SettlementReturnMode
{
    CloseAndResume,
    LoadConfiguredScene
}

/// <summary>
/// Owns only the exit boundary of settlement: modal input, view closure, optional scene return,
/// and restoration of player/cursor state. Settlement calculations and content belong elsewhere.
/// Keep this controller on an always-active object rather than the settlement panel itself.
/// </summary>
[DisallowMultipleComponent]
public sealed class SettlementExitController : MonoBehaviour
{
    [Header("Settlement View")]
    [SerializeField] GameObject settlementRoot;
    [SerializeField] PlayerController controlledPlayer;

    [Header("Return Destination")]
    [SerializeField] SettlementReturnMode returnMode = SettlementReturnMode.CloseAndResume;
    [SerializeField] string returnSceneName = SceneTransitionState.WorldSceneName;
    [SerializeField] string returnSpawnId = SceneTransitionState.SpawnWorkshopExterior;
    [SerializeField] bool avoidReloadingCurrentScene = true;

    [Header("Integration Events")]
    [SerializeField] UnityEvent settlementOpened = new UnityEvent();
    [SerializeField] UnityEvent settlementClosed = new UnityEvent();

    GameObject activeSettlementRoot;
    bool settlementActive;
    bool resolvingExit;
    bool playerStateStored;
    bool playerWasEnabled;
    bool cursorStateStored;
    CursorLockMode previousCursorLock;
    bool previousCursorVisible;

    public bool IsSettlementActive => settlementActive;
    public bool IsResolvingExit => resolvingExit;
    public SettlementReturnMode ReturnMode => returnMode;
    public string ReturnSceneName => returnSceneName;
    public string ReturnSpawnId => returnSpawnId;

    /// <summary>Entry point for a settlement panel assigned in the Inspector.</summary>
    public void OpenSettlement()
    {
        BeginSettlement(settlementRoot);
    }

    /// <summary>
    /// Entry point for a teammate-owned settlement view. The view is activated here and closed on exit.
    /// Returns false if a settlement is already active or no view was supplied.
    /// </summary>
    public bool BeginSettlement(GameObject viewRoot)
    {
        if (settlementActive || resolvingExit)
            return false;
        if (viewRoot == null)
        {
            Debug.LogError("SettlementExitController requires a settlement view root.", this);
            return false;
        }

        activeSettlementRoot = viewRoot;
        ResolvePlayer();
        StoreAndReleaseCursor();
        StoreAndDisablePlayer();
        GameFlowModalState.Acquire(this);
        InteractionPromptUI.HideImmediate();

        settlementActive = true;
        activeSettlementRoot.SetActive(true);
        settlementOpened.Invoke();
        return true;
    }

    /// <summary>Idempotent UI confirmation/close callback.</summary>
    public void ConfirmAndExit()
    {
        if (!settlementActive || resolvingExit)
            return;

        resolvingExit = true;
        bool shouldLoadScene = ShouldLoadConfiguredScene();
        if (shouldLoadScene && (string.IsNullOrWhiteSpace(returnSceneName) || string.IsNullOrWhiteSpace(returnSpawnId)))
        {
            Debug.LogError("Settlement return requires both a scene name and spawn ID. Resuming current scene instead.", this);
            shouldLoadScene = false;
        }

        CloseViewAndRestoreGameplay(true, true);

        if (!shouldLoadScene)
        {
            resolvingExit = false;
            return;
        }

        if (!SceneTransitionManager.TryRequest(returnSceneName, returnSpawnId))
            resolvingExit = false;
    }

    /// <summary>Explicit close-only path for cancel buttons or settlement views that never change scenes.</summary>
    public void CloseAndResume()
    {
        if (!settlementActive || resolvingExit)
            return;

        resolvingExit = true;
        CloseViewAndRestoreGameplay(true, true);
        resolvingExit = false;
    }

    public void ConfigureReturn(SettlementReturnMode mode, string sceneName, string spawnId)
    {
        returnMode = mode;
        returnSceneName = sceneName;
        returnSpawnId = spawnId;
    }

    public void SetSettlementRoot(GameObject viewRoot)
    {
        settlementRoot = viewRoot;
    }

    bool ShouldLoadConfiguredScene()
    {
        if (returnMode != SettlementReturnMode.LoadConfiguredScene)
            return false;

        Scene activeScene = SceneManager.GetActiveScene();
        return !avoidReloadingCurrentScene || activeScene.name != returnSceneName;
    }

    void ResolvePlayer()
    {
        if (controlledPlayer == null)
            controlledPlayer = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
    }

    void StoreAndDisablePlayer()
    {
        playerStateStored = controlledPlayer != null;
        playerWasEnabled = playerStateStored && controlledPlayer.enabled;
        if (playerWasEnabled)
            controlledPlayer.enabled = false;
    }

    void RestorePlayer()
    {
        if (playerStateStored && controlledPlayer != null)
            controlledPlayer.enabled = playerWasEnabled;
        playerStateStored = false;
        playerWasEnabled = false;
    }

    void StoreAndReleaseCursor()
    {
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        cursorStateStored = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void RestoreCursor()
    {
        if (!cursorStateStored)
            return;

        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
        cursorStateStored = false;
    }

    void CloseViewAndRestoreGameplay(bool waitForInteractRelease, bool invokeClosedEvent)
    {
        settlementActive = false;
        GameFlowModalState.Release(this, waitForInteractRelease);
        RestorePlayer();
        RestoreCursor();
        InteractionPromptUI.HideImmediate();

        GameObject view = activeSettlementRoot;
        activeSettlementRoot = null;
        if (view != null)
            view.SetActive(false);

        if (invokeClosedEvent)
            settlementClosed.Invoke();
    }

    void OnDisable()
    {
        if (settlementActive)
            CloseViewAndRestoreGameplay(false, false);
        else
            GameFlowModalState.Release(this, false);
        resolvingExit = false;
    }
}
