using System;
using UnityEngine;
using UnityEngine.UI;

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

    PlayerController scenePlayer;
    PlayerController nearbyPlayer;
    PlayerController lockedPlayer;
    bool lockedPlayerWasEnabled;
    bool dialogueOpen;
    WorkshopOrder.Phase? storyPhase;
    bool allowDuringProduction;
    Transform markerPivot;
    Text markerLabel;
    float markerHeight = 2.15f;
    bool StoryAvailable
    {
        get
        {
            if (dialogueEntryPoint is IDialogueAvailability availability && !availability.IsDialogueAvailable)
                return false;
            if (!storyPhase.HasValue)
                return true;
            WorkshopOrder order = WorkshopOrder.Current;
            return order != null && (order.CurrentPhase == storyPhase ||
                (dialogueEntryPoint is TemporaryDialogueEntryPoint dialogue && dialogue.HasReminderFor(order.CurrentPhase)) ||
                (allowDuringProduction && order.CurrentPhase is WorkshopOrder.Phase.Production
                    or WorkshopOrder.Phase.ReadyToSubmit or WorkshopOrder.Phase.OrderComplete));
        }
    }

    public bool IsDialogueOpen => dialogueOpen;
    public bool PlayerIsInRange => nearbyPlayer != null;
    public string PromptText => promptText;
    public float InteractionRadius => interactionRadius;
    public MonoBehaviour DialogueEntryPointComponent => dialogueEntryPoint;

    void Awake() => EnsureMarker();

    void Update()
    {
        RefreshNearbyPlayer();
        UpdateMarker();

        if (dialogueOpen || nearbyPlayer == null || !StoryAvailable || GameFlowModalState.IsInteractionBlocked)
            return;

        InteractionPromptUI.Show(promptText);
        if (SceneTransitionManager.TryConsumeInteractPress())
            TryBeginInteraction();
    }

    void RefreshNearbyPlayer()
    {
        if (scenePlayer == null)
            scenePlayer = UnityEngine.Object.FindFirstObjectByType<PlayerController>();

        bool wasInRange = nearbyPlayer != null;
        bool isInRange = scenePlayer != null && IsWithinInteractionRadius(scenePlayer.transform.position);
        nearbyPlayer = isInRange ? scenePlayer : null;

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
        if (dialogueOpen || nearbyPlayer == null || !StoryAvailable || GameFlowModalState.IsInteractionBlocked)
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
        if (Application.isPlaying)
            EnsureMarker();
    }

    public void ConfigureStoryPhase(WorkshopOrder.Phase phase, bool availableDuringProduction = false)
    {
        storyPhase = phase;
        allowDuringProduction = availableDuringProduction;
    }

    public void ConfigureMarkerHeight(float height)
    {
        markerHeight = Mathf.Max(.5f, height);
        if (markerPivot != null)
            markerPivot.localPosition = Vector3.up * markerHeight;
    }

    void UpdateMarker()
    {
        EnsureMarker();
        bool visible = StoryAvailable && !dialogueOpen && !GameFlowModalState.IsModalOpen &&
            !SceneTransitionState.IsLoading &&
            !(WorkshopOrder.Current != null && dialogueEntryPoint is TemporaryDialogueEntryPoint dialogue &&
                dialogue.HasReminderFor(WorkshopOrder.Current.CurrentPhase));
        if (markerPivot.gameObject.activeSelf != visible)
            markerPivot.gameObject.SetActive(visible);
        if (!visible)
            return;

        markerPivot.localPosition = Vector3.up *
            (markerHeight + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4.5f)) * .12f);
        Camera camera = Camera.main;
        if (camera != null)
            markerPivot.rotation = Quaternion.LookRotation(markerPivot.position - camera.transform.position, Vector3.up);
    }

    void EnsureMarker()
    {
        if (markerPivot != null)
            return;
        var root = new GameObject("DialogueMarker", typeof(RectTransform), typeof(Canvas));
        root.transform.SetParent(transform, false);
        markerPivot = root.transform;
        markerPivot.localPosition = Vector3.up * markerHeight;
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 25;
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(100f, 100f);
        rootRect.localScale = Vector3.one * .012f;

        var labelObject = new GameObject("Marker", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(root.transform, false);
        markerLabel = labelObject.GetComponent<Text>();
        markerLabel.rectTransform.anchorMin = Vector2.zero;
        markerLabel.rectTransform.anchorMax = Vector2.one;
        markerLabel.rectTransform.offsetMin = markerLabel.rectTransform.offsetMax = Vector2.zero;
        markerLabel.text = "!";
        markerLabel.alignment = TextAnchor.MiddleCenter;
        markerLabel.fontSize = 72;
        markerLabel.fontStyle = FontStyle.Bold;
        markerLabel.color = new Color32(255, 220, 79, 255);
        markerLabel.raycastTarget = false;
        InteractionPromptUI.EnsureChineseFont(markerLabel);
        var outline = labelObject.AddComponent<Outline>();
        outline.effectColor = new Color(.12f, .07f, .02f, .96f);
        outline.effectDistance = new Vector2(2f, -2f);
        root.SetActive(false);
    }

    void OnValidate()
    {
        interactionRadius = Mathf.Max(0.25f, interactionRadius);
    }

    void OnDisable()
    {
        InteractionPromptUI.Hide();
        if (markerPivot != null)
            markerPivot.gameObject.SetActive(false);

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
