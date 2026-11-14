using System;
using UnityEngine;

/// <summary>
/// Distance-based entry point for an existing NPC. Dialogue content and order creation are delegated
/// to the assigned IDialogueEntryPoint implementation. No collider, Rigidbody, or NPC placement is changed.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NpcCharacter))]
public sealed class NpcDialogueInteraction : MonoBehaviour
{
    [SerializeField] MonoBehaviour dialogueEntryPoint;
    [SerializeField] string promptText = "Press E to talk";
    [SerializeField, Min(0.25f)] float interactionRadius = 3f;

    PlayerController nearbyPlayer;
    PlayerController lockedPlayer;
    bool lockedPlayerWasEnabled;
    bool dialogueOpen;

    public bool IsDialogueOpen => dialogueOpen;
    public bool PlayerIsInRange => nearbyPlayer != null;
    public string PromptText => promptText;
    public float InteractionRadius => interactionRadius;
    public MonoBehaviour DialogueEntryPointComponent => dialogueEntryPoint;

    void Update()
    {
        RefreshNearbyPlayer();

        if (dialogueOpen || nearbyPlayer == null || GameFlowModalState.IsInteractionBlocked)
            return;

        InteractionPromptUI.Show(promptText);
        if (SceneTransitionManager.TryConsumeInteractPress())
            TryBeginInteraction();
    }

    void RefreshNearbyPlayer()
    {
        PlayerController player = nearbyPlayer;
        if (player == null)
            player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();

        bool wasInRange = nearbyPlayer != null;
        bool isInRange = player != null && IsWithinInteractionRadius(player.transform.position);
        nearbyPlayer = isInRange ? player : null;

        if (wasInRange && !isInRange && !dialogueOpen)
            InteractionPromptUI.Hide();
    }

    bool IsWithinInteractionRadius(Vector3 playerPosition)
    {
        Vector3 delta = playerPosition - transform.position;
        delta.y = 0f;
        return delta.sqrMagnitude <= interactionRadius * interactionRadius;
    }

    public bool TryBeginInteraction()
    {
        RefreshNearbyPlayer();
        if (dialogueOpen || nearbyPlayer == null || GameFlowModalState.IsInteractionBlocked)
            return false;

        IDialogueEntryPoint entryPoint = dialogueEntryPoint as IDialogueEntryPoint;
        if (entryPoint == null)
        {
            Debug.LogError("NPC dialogue entry point must implement IDialogueEntryPoint.", this);
            return false;
        }

        dialogueOpen = true;
        lockedPlayer = nearbyPlayer;
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

    public void Configure(MonoBehaviour entryPoint, string prompt, float radius = 3f)
    {
        dialogueEntryPoint = entryPoint;
        promptText = string.IsNullOrWhiteSpace(prompt) ? "Press E to talk" : prompt;
        interactionRadius = Mathf.Max(0.25f, radius);
    }

    void OnValidate()
    {
        interactionRadius = Mathf.Max(0.25f, interactionRadius);
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
        nearbyPlayer = null;
        GameFlowModalState.Release(this, false);
    }

    void OnDrawGizmosSelected()
    {
        Color previous = Gizmos.color;
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.75f);
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
        Gizmos.color = previous;
    }
}
