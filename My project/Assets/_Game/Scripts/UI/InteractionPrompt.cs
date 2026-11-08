using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class InteractionPrompt : MonoBehaviour
{
    [SerializeField] private FivePillarLevel level;
    [SerializeField] private Camera view;
    [SerializeField] private TextMeshProUGUI status;
    [SerializeField] private TextMeshProUGUI[] pullLabels = new TextMeshProUGUI[0];
    [SerializeField] private ClimbableRope[] ropes = new ClimbableRope[0];
    [SerializeField] private TextMeshProUGUI[] climbLabels = new TextMeshProUGUI[0];

    private RectTransform timingRoot;
    private RectTransform timingPointer;
    private TextMeshProUGUI timingTitle;
    private TextMeshProUGUI completionText;
    private RectTransform completionRoot;
    private const float TimingBarWidth = 560f;

    public void Configure(
        FivePillarLevel puzzle,
        Camera camera,
        TextMeshProUGUI help,
        TextMeshProUGUI[] pulls,
        ClimbableRope[] climbs,
        TextMeshProUGUI[] climbTexts)
    {
        level = puzzle;
        view = camera;
        status = help;
        pullLabels = pulls;
        ropes = climbs;
        climbLabels = climbTexts;
    }

    private void Awake()
    {
        BuildRuntimeHud();
    }

    private void LateUpdate()
    {
        if (level == null || view == null) return;

        UpdateWorldLabels();
        UpdateTimingBar();
        UpdateCompletion();
        UpdateStatus();
    }

    private void UpdateWorldLabels()
    {
        int pullCount = Mathf.Min(pullLabels.Length, level.Mechanisms.Length);
        for (int i = 0; i < pullCount; i++)
        {
            PullBlockMechanism mechanism = level.Mechanisms[i];
            TextMeshProUGUI label = pullLabels[i];
            if (mechanism == null || label == null) continue;

            label.transform.parent.rotation = view.transform.rotation;
            bool show = mechanism.State == PullBlockMechanism.PullState.Ready ||
                        mechanism.State == PullBlockMechanism.PullState.Charging ||
                        mechanism.State == PullBlockMechanism.PullState.Throwing ||
                        mechanism.State == PullBlockMechanism.PullState.Failing;
            label.transform.parent.gameObject.SetActive(show);
            switch (mechanism.State)
            {
                case PullBlockMechanism.PullState.Ready:
                    label.text = "THROW [E]";
                    break;
                case PullBlockMechanism.PullState.Charging:
                    label.text = "HOLDING PLANK - SPACE!";
                    break;
                case PullBlockMechanism.PullState.Throwing:
                    label.text = "THROWING...";
                    break;
                case PullBlockMechanism.PullState.Failing:
                    label.text = "ROPE SNAPPED";
                    break;
            }

            label.color = float.IsPositiveInfinity(mechanism.InteractionDistance(level.Player.Position))
                ? new Color(1f, 0.75f, 0.25f)
                : Color.white;
        }

        int climbCount = Mathf.Min(climbLabels.Length, ropes.Length);
        for (int i = 0; i < climbCount; i++)
        {
            if (climbLabels[i] == null || ropes[i] == null) continue;
            climbLabels[i].transform.parent.gameObject.SetActive(
                ropes[i].Available && ropes[i].gameObject.activeInHierarchy);
            climbLabels[i].transform.parent.rotation = view.transform.rotation;
            climbLabels[i].text = ropes[i].SwingAvailable ? "RIDE PLANK / GRAB ROPE [E]" : "CLIMB [E]";
            climbLabels[i].color = float.IsPositiveInfinity(ropes[i].InteractionDistance(level.Player.Position))
                ? new Color(0.2f, 1f, 1f)
                : Color.white;
        }
    }

    private void UpdateTimingBar()
    {
        if (timingRoot == null) BuildRuntimeHud();
        PullBlockMechanism timing = level.ActiveTimingMechanism;
        bool active = timing != null;
        if (completionRoot != null) completionRoot.gameObject.SetActive(!active);
        timingRoot.gameObject.SetActive(active);
        if (!active) return;

        timingPointer.anchoredPosition = new Vector2(timing.TimingPointer * TimingBarWidth, 0f);
        timingTitle.text = "FACING LEFT - THROW WITH SPACE IN THE GREEN ZONE";
    }

    private void UpdateCompletion()
    {
        if (completionText == null) BuildRuntimeHud();
        string result = level.ReachedFinish
            ? (level.PerfectComplete ? "PERFECT RUN" : "FINISHED WITH MISSES")
            : "MOVE EVERY RIGHT PLANK LEFT";
        completionText.text =
            $"COMPLETION  {level.CompletionPercent}%\n" +
            $"<size=50%>{result}</size>\n" +
            $"<size=50%>LANDED {level.TransferCount}/{level.Mechanisms.Length}   LOST {level.FailureCount}</size>";
    }

    private void UpdateStatus()
    {
        if (status == null) return;

        string instruction = level.Interaction != null ? level.Interaction.CurrentPrompt : string.Empty;
        PullBlockMechanism timing = level.ActiveTimingMechanism;

        if (timing != null)
            instruction = "Timing active: press SPACE while the pointer is green.";
        else if (level.Player.IsSwinging)
            instruction = "W / S pumps the suspended plank, rope and rider together. SPACE leaps at the current tangent.";
        else if (level.AtGoal)
            instruction = level.PerfectComplete
                ? "COMPLETE - every right plank landed on the left."
                : "FINISH REACHED - missed planks permanently reduced completion.";
        else if (level.Complete && string.IsNullOrEmpty(instruction))
            instruction = "All throws resolved. Continue forward to the finish.";
        else if (string.IsNullOrEmpty(instruction))
            instruction = "Orange rope rings block lanes. Find a gap and reach the next THROW marker.";

        status.text =
            "WASD / Arrows: move   SPACE: jump / release   E: interact\n" +
            $"Landed left: {level.TransferCount}/{level.Mechanisms.Length}   " +
            $"Lost: {level.FailureCount}   Completion: {level.CompletionPercent}%\n" + instruction;
    }

    private void BuildRuntimeHud()
    {
        if (timingRoot != null && completionText != null) return;

        TMP_FontAsset font = status != null ? status.font : null;

        RectTransform completionPanel = CreatePanel(
            "CompletionPanel",
            transform,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-20f, -20f),
            new Vector2(390f, 112f),
            new Vector2(1f, 1f),
            new Color(0.055f, 0.045f, 0.035f, 0.9f));
        completionRoot = completionPanel;
        completionText = CreateText("CompletionText", completionPanel, font, 27, TextAlignmentOptions.TopRight);
        completionText.margin = new Vector4(16f, 10f, 16f, 10f);
        completionText.color = new Color(1f, 0.89f, 0.58f);

        timingRoot = CreatePanel(
            "ThrowTimingPanel",
            transform,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -24f),
            new Vector2(650f, 120f),
            new Vector2(0.5f, 1f),
            new Color(0.045f, 0.035f, 0.028f, 0.94f));

        timingTitle = CreateText("TimingTitle", timingRoot, font, 24, TextAlignmentOptions.Top);
        timingTitle.rectTransform.offsetMin = new Vector2(15f, 65f);
        timingTitle.rectTransform.offsetMax = new Vector2(-15f, -8f);
        timingTitle.color = new Color(1f, 0.92f, 0.72f);

        RectTransform bar = CreatePanel(
            "TimingBar",
            timingRoot,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 20f),
            new Vector2(TimingBarWidth, 28f),
            new Vector2(0.5f, 0f),
            new Color(0.12f, 0.1f, 0.08f, 1f));

        CreateFill("OrangeLeft", bar, 0f, 0.42f, new Color(0.96f, 0.34f, 0.08f));
        CreateFill("Green", bar, 0.42f, 0.58f, new Color(0.24f, 0.92f, 0.34f));
        CreateFill("OrangeRight", bar, 0.58f, 1f, new Color(0.96f, 0.34f, 0.08f));

        var pointerObject = new GameObject("Pointer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        pointerObject.transform.SetParent(bar, false);
        timingPointer = pointerObject.GetComponent<RectTransform>();
        timingPointer.anchorMin = new Vector2(0f, 0.5f);
        timingPointer.anchorMax = new Vector2(0f, 0.5f);
        timingPointer.pivot = new Vector2(0.5f, 0.5f);
        timingPointer.sizeDelta = new Vector2(7f, 44f);
        pointerObject.GetComponent<Image>().color = Color.white;

        timingRoot.gameObject.SetActive(false);
    }

    private static RectTransform CreatePanel(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 size,
        Vector2 pivot,
        Color color)
    {
        var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        gameObject.transform.SetParent(parent, false);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        gameObject.GetComponent<Image>().color = color;
        return rect;
    }

    private static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        TMP_FontAsset font,
        float size,
        TextAlignmentOptions alignment)
    {
        var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        gameObject.transform.SetParent(parent, false);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static void CreateFill(string name, Transform parent, float start, float end, Color color)
    {
        var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        gameObject.transform.SetParent(parent, false);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(start, 0f);
        rect.anchorMax = new Vector2(end, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        gameObject.GetComponent<Image>().color = color;
    }
}
