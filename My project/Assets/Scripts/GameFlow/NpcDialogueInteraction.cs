using System;
using UnityEngine;

/// <summary>
/// Proximity entry point for an NPC conversation. Dialogue content and order creation are delegated
/// to the assigned IDialogueEntryPoint implementation.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed class NpcDialogueInteraction : MonoBehaviour
{
    [SerializeField] MonoBehaviour dialogueEntryPoint;
    [SerializeField] string promptText = "Press E to talk";

    PlayerController playerInRange;
    PlayerController lockedPlayer;
    bool lockedPlayerWasEnabled;
    bool dialogueOpen;

    public bool IsDialogueOpen => dialogueOpen;
    public bool PlayerIsInRange => playerInRange != null;
    public string PromptText => promptText;
    public MonoBehaviour DialogueEntryPointComponent => dialogueEntryPoint;

    void Awake()
    {
        BoxCollider trigger = GetComponent<BoxCollider>();
        trigger.isTrigger = true;

        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    void Update()
    {
        if (dialogueOpen || playerInRange == null || GameFlowModalState.IsInteractionBlocked)
            return;

        InteractionPromptUI.Show(promptText);
        if (SceneTransitionManager.TryConsumeInteractPress())
            TryBeginInteraction();
    }

    void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
            return;

        playerInRange = player;
        if (!dialogueOpen && !GameFlowModalState.IsInteractionBlocked)
            InteractionPromptUI.Show(promptText);
    }

    void OnTriggerExit(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || player != playerInRange)
            return;

        if (!dialogueOpen)
            playerInRange = null;
        InteractionPromptUI.Hide();
    }

    public bool TryBeginInteraction()
    {
        if (dialogueOpen || playerInRange == null || GameFlowModalState.IsInteractionBlocked)
            return false;

        IDialogueEntryPoint entryPoint = dialogueEntryPoint as IDialogueEntryPoint;
        if (entryPoint == null)
        {
            Debug.LogError("NPC dialogue entry point must implement IDialogueEntryPoint.", this);
            return false;
        }

        dialogueOpen = true;
        lockedPlayer = playerInRange;
        lockedPlayerWasEnabled = lockedPlayer.enabled;
        if (lockedPlayerWasEnabled)
            lockedPlayer.enabled = false;

        GameFlowModalState.Acquire(this);
        InteractionPromptUI.HideImmediate();

        bool accepted = false;
        try
        {
            accepted = entryPoint.TryBeginDialogue(this, NotifyDialogueClosed);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }

        if (!accepted && dialogueOpen)
            NotifyDialogueClosed();
        return accepted;
    }

    /// <summary>Dialogue implementations call this callback, directly or via the supplied Action.</summary>
    public void NotifyDialogueClosed()
    {
        if (!dialogueOpen)
            return;

        dialogueOpen = false;
        if (lockedPlayer != null && lockedPlayerWasEnabled)
            lockedPlayer.enabled = true;

        lockedPlayer = null;
        lockedPlayerWasEnabled = false;
        GameFlowModalState.Release(this);
    }

    public void Configure(MonoBehaviour entryPoint, string prompt)
    {
        dialogueEntryPoint = entryPoint;
        promptText = string.IsNullOrWhiteSpace(prompt) ? "Press E to talk" : prompt;
    }

    void OnDisable()
    {
        InteractionPromptUI.Hide();

        if (dialogueOpen)
        {
            dialogueOpen = false;
            if (lockedPlayer != null && lockedPlayerWasEnabled)
                lockedPlayer.enabled = true;
        }

        lockedPlayer = null;
        lockedPlayerWasEnabled = false;
        playerInRange = null;
        GameFlowModalState.Release(this, false);
    }
}
