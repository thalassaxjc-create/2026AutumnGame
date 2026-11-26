using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Shared screen for market conversations and short village activities.</summary>
internal sealed class VillageActivityView
{
    const int OptionCount = 8;
    readonly Text title;
    readonly Text body;
    readonly Button[] options = new Button[OptionCount];
    readonly Text[] optionLabels = new Text[OptionCount];
    readonly Transform card;
    GameObject proof;
    RectTransform pullGauges;
    RectTransform balanceTrack;
    RectTransform balanceMarker;
    RectTransform coverageFill;
    Text pullCue;
    RectTransform secondProof;
    Text[] firstCorners;
    Text[] secondCorners;
    Text firstProofCaption;
    readonly Button primary;
    readonly Text primaryLabel;
    readonly Button secondary;
    readonly Text secondaryLabel;

    internal GameObject Root { get; }

    internal VillageActivityView(Transform parent, UnityAction close)
    {
        Root = new GameObject("村里活动", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        Root.transform.SetParent(parent, false);
        var canvas = Root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;
        var scaler = Root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        WorkshopStoryUI.EnsureEventSystem(Root.transform);
        var dim = WorkshopStoryUI.Panel(Root.transform, "遮罩", new Rect(0, 0, 1, 1),
            new Color32(26, 19, 13, 166), true);
        card = WorkshopStoryUI.Panel(dim.transform, "告示与交易", new Rect(.22f, .13f, .56f, .74f),
            WorkshopStoryUI.Paper).transform;
        WorkshopStoryUI.Panel(card, "木色横边", new Rect(0, .98f, 1, .02f), WorkshopStoryUI.Wood);
        title = WorkshopStoryUI.Label(card, "标题", "", new Rect(.06f, .84f, .88f, .11f),
            48, WorkshopStoryUI.Ink, TextAnchor.MiddleLeft, true);
        body = WorkshopStoryUI.Label(card, "说明", "", new Rect(.06f, .54f, .88f, .28f),
            31, WorkshopStoryUI.Ink, TextAnchor.UpperLeft);

        for (int i = 0; i < options.Length; i++)
        {
            options[i] = WorkshopStoryUI.Button(card, "选项", "",
                new Rect(.06f + (i % 2) * .45f, .435f - (i / 2) * .098f, .43f, .081f),
                WorkshopStoryUI.Wood, 29, NoAction);
            optionLabels[i] = options[i].GetComponentInChildren<Text>();
        }
        WorkshopStoryUI.Button(card, "离开", "离开  Esc", new Rect(.06f, .035f, .27f, .078f),
            WorkshopStoryUI.Wood, 26, close);
        secondary = WorkshopStoryUI.Button(card, "清空词组", "清空", new Rect(.37f, .035f, .22f, .078f),
            WorkshopStoryUI.Wood, 26, NoAction);
        secondaryLabel = secondary.GetComponentInChildren<Text>();
        primary = WorkshopStoryUI.Button(card, "确认动作", "", new Rect(.64f, .035f, .30f, .078f),
            WorkshopStoryUI.Green, 27, NoAction);
        primaryLabel = primary.GetComponentInChildren<Text>();
        Root.SetActive(false);
    }

    internal void SetContent(string heading, string description)
    {
        title.text = heading;
        if (proof != null)
            proof.SetActive(false);
        if (pullGauges != null)
            pullGauges.gameObject.SetActive(false);
        SetBody(description);
        foreach (Button option in options)
            option.gameObject.SetActive(false);
        primary.gameObject.SetActive(false);
        secondary.gameObject.SetActive(false);
    }

    internal void ShowTrialProof(bool turned, bool followsCurtain)
    {
        EnsureProof();
        firstProofCaption.text = "转帘前";
        secondProof.gameObject.SetActive(turned);
        PaintProof(firstCorners, 0);
        if (turned)
            PaintProof(secondCorners, followsCurtain ? 3 : 0);
    }

    internal void ShowLessonProof(bool successful, string caption = "艾琳的试纸")
    {
        EnsureProof();
        firstProofCaption.text = caption;
        secondProof.gameObject.SetActive(false);
        PaintProof(firstCorners, successful ? -1 : 0);
    }

    internal void ShowPull(float balance, float coverage, string cue)
    {
        if (pullGauges == null)
        {
            pullGauges = WorkshopStoryUI.Region(card, "试捞操作", new Rect(.10f, .17f, .80f, .34f));
            var wood = WorkshopStoryUI.Wood;
            var ink = WorkshopStoryUI.Ink;
            WorkshopStoryUI.Label(pullGauges, "平衡", "帘面平衡", new Rect(0f, .71f, .25f, .20f), 24, ink);
            balanceTrack = WorkshopStoryUI.Panel(pullGauges, "平衡槽", new Rect(.28f, .73f, .70f, .16f), wood).rectTransform;
            WorkshopStoryUI.Panel(balanceTrack, "平稳区", new Rect(.39f, 0f, .22f, 1f), WorkshopStoryUI.Green);
            balanceMarker = WorkshopStoryUI.Panel(balanceTrack, "帘面指针", new Rect(0f, .10f, .035f, .80f),
                WorkshopStoryUI.Paper).rectTransform;
            WorkshopStoryUI.Label(pullGauges, "纸浆", "纸浆覆盖", new Rect(0f, .44f, .25f, .20f), 24, ink);
            var coverageTrack = WorkshopStoryUI.Panel(pullGauges, "纸浆槽", new Rect(.28f, .46f, .70f, .16f), wood).transform;
            coverageFill = WorkshopStoryUI.Panel(coverageTrack, "进度", new Rect(0f, 0f, 0f, 1f),
                WorkshopStoryUI.Green).rectTransform;
            pullCue = WorkshopStoryUI.Label(pullGauges, "试捞状态", "", new Rect(0f, .01f, 1f, .31f),
                28, ink, bold: true);
        }
        pullGauges.gameObject.SetActive(true);
        float marker = (balance + 1f) * .4825f;
        balanceMarker.anchorMin = new Vector2(marker, .10f);
        balanceMarker.anchorMax = new Vector2(marker + .035f, .90f);
        coverageFill.anchorMax = new Vector2(coverage, 1f);
        pullCue.text = cue;
    }

    internal float? ReadPullPointer(Vector2 position)
    {
        if (balanceTrack == null || !RectTransformUtility.RectangleContainsScreenPoint(balanceTrack, position) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(balanceTrack, position, null, out var local))
            return null;
        Rect rect = balanceTrack.rect;
        return Mathf.Lerp(-1f, 1f, Mathf.InverseLerp(rect.xMin, rect.xMax, local.x));
    }

    void EnsureProof()
    {
        if (proof == null)
        {
            proof = WorkshopStoryUI.Region(card, "试纸对照", new Rect(.12f, .16f, .76f, .23f)).gameObject;
            BuildProofSheet(0f, "转帘前", out firstCorners, out firstProofCaption);
            secondProof = BuildProofSheet(.53f, "转帘后", out secondCorners, out _);
        }
        proof.SetActive(true);
    }

    RectTransform BuildProofSheet(float x, string caption, out Text[] corners, out Text header)
    {
        var panel = WorkshopStoryUI.Panel(proof.transform, caption,
            new Rect(x, 0f, .47f, 1f), WorkshopStoryUI.Wood).rectTransform;
        header = WorkshopStoryUI.Label(panel, "方向", caption, new Rect(.04f, .70f, .92f, .26f),
            23, WorkshopStoryUI.Paper);
        var paper = WorkshopStoryUI.Region(panel, "纸面", new Rect(.12f, .08f, .76f, .60f));
        corners = new Text[4];
        for (int i = 0; i < corners.Length; i++)
        {
            var cell = WorkshopStoryUI.Panel(paper, "纸角",
                new Rect((i % 2) * .51f, i < 2 ? .51f : 0f, .49f, .49f), WorkshopStoryUI.Paper);
            corners[i] = WorkshopStoryUI.Label(cell.transform, "厚薄", "", new Rect(0f, 0f, 1f, 1f),
                20, WorkshopStoryUI.Ink);
        }
        return panel;
    }

    static void PaintProof(Text[] corners, int thinCorner)
    {
        for (int i = 0; i < corners.Length; i++)
        {
            bool thin = i == thinCorner;
            corners[i].text = thin ? "偏薄" : "";
            corners[i].transform.parent.GetComponent<Image>().color =
                thin ? new Color32(255, 253, 236, 255) : WorkshopStoryUI.Paper;
        }
    }

    internal void SetBody(string description) => body.text = description;

    internal void SetOption(int index, string caption, UnityAction action, bool enabled = true) =>
        Bind(options[index], optionLabels[index], caption, action, enabled);

    internal void SetPrimary(string caption, UnityAction action, bool enabled = true) =>
        Bind(primary, primaryLabel, caption, action, enabled);

    internal void SetClear(UnityAction action, bool enabled, string caption = "清空") =>
        Bind(secondary, secondaryLabel, caption, action, enabled);

    static void Bind(Button button, Text label, string caption, UnityAction action, bool enabled)
    {
        if (label != null)
            label.text = caption;
        button.gameObject.name = caption;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
        button.interactable = enabled;
        button.gameObject.SetActive(true);
    }

    static void NoAction() { }
}
