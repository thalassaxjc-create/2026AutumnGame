using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(InteractionPoint))]
public sealed class WorkshopComputer : MonoBehaviour
{
    [SerializeField] ComputerUI interfacePrefab;
    [SerializeField] PlayerController player;
    [SerializeField] CameraFollow cameraFollow;
    [SerializeField, Min(0.1f)] float interactionDistance = 2f;

    ComputerUI screen;
    InputAction interactAction;
    GameObject localEventSystem;
    bool isOpen;
    bool showingPrompt;
    bool movementWasEnabled;
    bool followWasEnabled;
    bool cursorWasVisible;
    CursorLockMode previousCursorLock;

    public ComputerUI Screen => screen;
    public event Action<ComputerUI> Opened;

    void OnEnable()
    {
        interactAction = InputSystem.actions?.FindAction("Player/Interact", false);
        interactAction?.Enable();
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (isOpen)
        {
            if (SceneTransitionState.IsLoading || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame))
                Close();
            return;
        }

        bool canOpen = interfacePrefab != null && player != null && player.isActiveAndEnabled
            && !SceneTransitionState.IsLoading
            && (player.transform.position - transform.position).sqrMagnitude
                <= interactionDistance * interactionDistance;
        SetPrompt(canOpen);
        if (canOpen && (interactAction?.WasPressedThisFrame() ?? (keyboard?.eKey.wasPressedThisFrame == true)))
            Open();
    }

    void Open()
    {
        SetPrompt(false);
        if (EventSystem.current == null)
        {
            localEventSystem = new GameObject("Computer UI Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
            localEventSystem.transform.SetParent(transform, false);
        }

        if (screen == null)
        {
            screen = Instantiate(interfacePrefab);
            SceneManager.MoveGameObjectToScene(screen.gameObject, gameObject.scene);
            screen.CloseRequested += Close;
        }

        movementWasEnabled = player.enabled;
        followWasEnabled = cameraFollow != null && cameraFollow.enabled;
        cursorWasVisible = Cursor.visible;
        previousCursorLock = Cursor.lockState;

        isOpen = true;
        player.enabled = false;
        if (cameraFollow != null)
            cameraFollow.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        screen.gameObject.SetActive(true);
        Opened?.Invoke(screen);
    }

    void Close()
    {
        if (!isOpen)
            return;

        isOpen = false;
        if (screen != null)
            screen.gameObject.SetActive(false);
        if (localEventSystem != null)
        {
            localEventSystem.SetActive(false);
            Destroy(localEventSystem);
            localEventSystem = null;
        }

        if (player != null)
            player.enabled = movementWasEnabled;
        if (cameraFollow != null)
            cameraFollow.enabled = followWasEnabled;
        Cursor.lockState = previousCursorLock;
        Cursor.visible = cursorWasVisible;
    }

    void OnDisable()
    {
        SetPrompt(false);
        Close();
    }

    void OnDestroy()
    {
        if (screen != null)
            Destroy(screen.gameObject);
    }

    void SetPrompt(bool visible)
    {
        if (showingPrompt == visible)
            return;

        showingPrompt = visible;
        if (visible)
            InteractionPromptUI.Show("按 E 使用电脑");
        else
            InteractionPromptUI.Hide();
    }
}
