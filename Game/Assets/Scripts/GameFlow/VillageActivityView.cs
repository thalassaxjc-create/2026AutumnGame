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
    readonly Button primary;
    readonly Text primaryLabel;
    readonly Button secondary;
    readonly Button undo;
    readonly Text draft;

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
        var card = WorkshopStoryUI.Panel(dim.transform, "告示与交易", new Rect(.22f, .13f, .56f, .74f),
            WorkshopStoryUI.Paper).transform;
        WorkshopStoryUI.Panel(card, "木色横边", new Rect(0, .98f, 1, .02f), WorkshopStoryUI.Wood);
        title = WorkshopStoryUI.Label(card, "标题", "", new Rect(.06f, .84f, .88f, .11f),
            48, WorkshopStoryUI.Ink, TextAnchor.MiddleLeft, true);
        body = WorkshopStoryUI.Label(card, "说明", "", new Rect(.06f, .54f, .88f, .28f),
            31, WorkshopStoryUI.Ink, TextAnchor.UpperLeft);
        draft = WorkshopStoryUI.Label(card, "告示草稿", "", new Rect(.06f, .54f, .88f, .10f),
            29, WorkshopStoryUI.Green, TextAnchor.MiddleLeft, true);
        draft.gameObject.SetActive(false);

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
        undo = WorkshopStoryUI.Button(card, "撤回词组", "撤回", new Rect(.34f, .035f, .13f, .078f),
            WorkshopStoryUI.Wood, 26, NoAction);
        undo.gameObject.SetActive(false);
        primary = WorkshopStoryUI.Button(card, "确认动作", "", new Rect(.64f, .035f, .30f, .078f),
            WorkshopStoryUI.Green, 27, NoAction);
        primaryLabel = primary.GetComponentInChildren<Text>();
        Root.SetActive(false);
    }

    internal void SetContent(string heading, string description)
    {
        title.text = heading;
        SetBody(description);
        foreach (Button option in options)
            option.gameObject.SetActive(false);
        primary.gameObject.SetActive(false);
        secondary.gameObject.SetActive(false);
        undo.gameObject.SetActive(false);
        draft.gameObject.SetActive(false);
        Layout(body.rectTransform, new Rect(.06f, .54f, .88f, .28f));
        Layout((RectTransform)secondary.transform, new Rect(.37f, .035f, .22f, .078f));
    }

    internal void SetBody(string description) => body.text = description;

    internal void SetExpandedBody() => Layout(body.rectTransform, new Rect(.06f, .17f, .88f, .65f));

    internal void SetOption(int index, string caption, UnityAction action, bool enabled = true) =>
        Bind(options[index], optionLabels[index], caption, action, enabled);

    internal void SetPrimary(string caption, UnityAction action, bool enabled = true) =>
        Bind(primary, primaryLabel, caption, action, enabled);

    internal void SetClear(UnityAction action, bool enabled) => Bind(secondary, null, "清空", action, enabled);

    internal void SetTeaDraft(string sentence, UnityAction withdraw, bool canWithdraw)
    {
        Layout(body.rectTransform, new Rect(.06f, .66f, .88f, .17f));
        Layout((RectTransform)secondary.transform, new Rect(.49f, .035f, .13f, .078f));
        draft.text = sentence;
        draft.gameObject.SetActive(true);
        if (withdraw != null) Bind(undo, null, "撤回", withdraw, canWithdraw);
        else undo.gameObject.SetActive(false);
    }

    static void Layout(RectTransform rect, Rect anchors)
    {
        rect.anchorMin = anchors.min;
        rect.anchorMax = anchors.max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

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
