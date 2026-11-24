using UnityEngine;
using UnityEngine.UI;

/// <summary>晾晒架交互与世界空间倒计时。阴干期间玩家可自由离开工坊。</summary>
[DisallowMultipleComponent]
public sealed class DryingStationTrigger : MonoBehaviour
{
    const float InteractionDistance = 3f;
    const string Prompt = "按 E 将漆帘放上晾晒架阴干";

    PlayerController player;
    Transform countdownPivot;
    Text countdownLabel;
    bool wasInRange;

    void Awake() => EnsureCountdownView();

    void Update()
    {
        WorkshopOrder order = WorkshopOrder.Current;
        if (order == null)
            return;

        UpdateCountdown(order);
        if (player == null)
            player = FindFirstObjectByType<PlayerController>();

        bool inRange = player != null && !GameFlowModalState.IsInteractionBlocked &&
            !SceneTransitionState.IsLoading && HorizontalDistanceSquared(player.transform.position, transform.position)
            <= InteractionDistance * InteractionDistance;
        bool canStart = inRange && order.CanStartDrying;
        if (canStart)
            InteractionPromptUI.Show(Prompt);
        else if (wasInRange && InteractionPromptUI.CurrentMessage == Prompt)
            InteractionPromptUI.Hide();
        wasInRange = canStart;

        if (canStart && SceneTransitionManager.TryConsumeInteractPress())
        {
            order.BeginDrying();
            InteractionPromptUI.HideImmediate();
            UpdateCountdown(order);
        }
    }

    void LateUpdate()
    {
        if (countdownPivot == null || !countdownPivot.gameObject.activeSelf || Camera.main == null)
            return;
        countdownPivot.rotation = Quaternion.LookRotation(
            countdownPivot.position - Camera.main.transform.position, Vector3.up);
    }

    void UpdateCountdown(WorkshopOrder order)
    {
        EnsureCountdownView();
        bool visible = order.IsDrying || order.GetStatus(WorkshopOrder.Stage.Drying) == WorkshopOrder.StageStatus.Complete;
        countdownPivot.gameObject.SetActive(visible);
        if (!visible)
            return;

        if (!order.IsDrying)
        {
            countdownLabel.text = $"第 {order.CurrentSheetNumber} 张阴干完成";
            countdownLabel.color = new Color32(174, 232, 154, 255);
            return;
        }

        int remaining = order.RemainingDryingMinutes;
        countdownLabel.text = $"第 {order.CurrentSheetNumber} 张阴干  {remaining / 60:00}:{remaining % 60:00}";
        countdownLabel.color = new Color32(255, 238, 190, 255);
    }

    void EnsureCountdownView()
    {
        if (countdownPivot != null)
            return;
        var root = new GameObject("DryingCountdown", typeof(RectTransform), typeof(Canvas));
        root.transform.SetParent(transform, false);
        root.transform.localPosition = new Vector3(0f, 2.6f, 0f);
        countdownPivot = root.transform;

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 40;
        RectTransform canvasRect = root.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(430f, 90f);
        canvasRect.localScale = Vector3.one * .012f;

        var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(root.transform, false);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = backgroundRect.offsetMax = Vector2.zero;
        background.GetComponent<Image>().color = new Color(.12f, .08f, .04f, .88f);

        var label = new GameObject("Time", typeof(RectTransform), typeof(Text));
        label.transform.SetParent(root.transform, false);
        countdownLabel = label.GetComponent<Text>();
        countdownLabel.rectTransform.anchorMin = Vector2.zero;
        countdownLabel.rectTransform.anchorMax = Vector2.one;
        countdownLabel.rectTransform.offsetMin = new Vector2(15f, 8f);
        countdownLabel.rectTransform.offsetMax = new Vector2(-15f, -8f);
        countdownLabel.alignment = TextAnchor.MiddleCenter;
        countdownLabel.fontSize = 36;
        countdownLabel.fontStyle = FontStyle.Bold;
        countdownLabel.raycastTarget = false;
        InteractionPromptUI.EnsureChineseFont(countdownLabel);
        root.SetActive(false);
    }

    static float HorizontalDistanceSquared(Vector3 a, Vector3 b)
    {
        Vector3 delta = a - b;
        delta.y = 0f;
        return delta.sqrMagnitude;
    }

    void OnDisable()
    {
        if (InteractionPromptUI.CurrentMessage == Prompt)
            InteractionPromptUI.Hide();
        wasInRange = false;
    }
}
