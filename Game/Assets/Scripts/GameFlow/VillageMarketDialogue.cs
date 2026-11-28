using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class VillageMarketDialogue : MonoBehaviour, IDialogueEntryPoint, IDialogueAvailability
{
    public enum Role { Blacksmith, TeaOwner, LiAunt, GoodsMerchant, Collector, NightTrader, XiaoPiBucket }

    const string TeaTitle = "茶摊的大学问";
    static readonly string[] TeaWords =
    {
        "骗走旧物的人", "请乡亲们", "上好的", "留意", "今晚", "一只茶杯", "以鉴宝为由", "送来"
    };
    static readonly int[] CorrectTeaOrder = { 1, 3, 6, 0 };

    [SerializeField] Role role;
    [SerializeField] GameObject appearance;

    readonly List<int> chosenWords = new(4);
    VillageActivityView view;
    NpcCharacter character;
    Action closedCallback;
    CursorLockMode previousCursorLock;
    bool previousCursorVisible;
    string feedback;

    public Role MerchantRole => role;
    public bool IsDialogueAvailable => WorkshopOrder.Current is { } order && order.CanExplore && (role switch
    {
        Role.Collector => order.CollectorVisiting,
        Role.NightTrader => order.BlackMarketOpen,
        _ => true
    });

    public void Configure(Role value, GameObject model)
    {
        role = value;
        appearance = model;
    }

    void Awake()
    {
        character = GetComponent<NpcCharacter>();
        UpdateAppearance();
    }

    void Update()
    {
        UpdateAppearance();
        if (closedCallback != null && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            Close();
    }

    void UpdateAppearance()
    {
        if (role is not (Role.NightTrader or Role.Collector))
            return;
        bool visible = IsDialogueAvailable || closedCallback != null;
        if (appearance != null && appearance.activeSelf != visible)
            appearance.SetActive(visible);
        character?.SetNameplateVisible(visible);
    }

    public bool TryBeginDialogue(NpcDialogueInteraction source, Action onDialogueClosed)
    {
        if (!IsDialogueAvailable || closedCallback != null)
            return false;
        view ??= new VillageActivityView(transform, Close);
        feedback = null;
        chosenWords.Clear();
        closedCallback = onDialogueClosed;
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Refresh();
        view.Root.SetActive(true);
        return true;
    }

    void Close()
    {
        if (closedCallback == null)
            return;
        view.Root.SetActive(false);
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
        Action callback = closedCallback;
        closedCallback = null;
        callback();
    }

    void OnDisable() => Close();

    string Message(string description) => string.IsNullOrEmpty(feedback) ? description : description + "\n\n" + feedback;

    void Refresh()
    {
        var order = WorkshopOrder.Current;
        if (order == null)
            return;

        switch (role)
        {
            case Role.Blacksmith:
                view.SetContent("铁匠铺", Message($"铁匠：炉子还缺柴火。完整青竹一根给 {WorkshopOrder.GreenBambooSalePrice} 元。\n" +
                    $"你的青竹 {WorkshopInventory.GreenBamboo} 根    资金 ¥{order.Funds}"));
                view.SetOption(0, "卖 1 根青竹", () => Trade(order.TrySellGreenBamboo, $"卖出青竹，收入 {WorkshopOrder.GreenBambooSalePrice} 元。"),
                    order.CanExplore && WorkshopInventory.GreenBamboo > 0);
                break;

            case Role.TeaOwner:
                ShowTea(order);
                break;

            case Role.LiAunt:
                string grove = order.SpottedGroveHarvested ? "竹丛少了两根。谁动了我的斑竹？" :
                    "竹林朝村子这边有一丛紫斑竹，黄狗替我看着。集市有人卖斑竹，要用就去那儿找。";
                view.SetContent("李婶的斑竹", "李婶：" + grove);
                break;

            case Role.GoodsMerchant:
                view.SetContent("集市杂货摊", Message($"这里收购和出售斑竹。篾竹时按 V 放斑竹，整张帘用斑竹，舅舅按双倍价验收。\n" +
                    $"你的斑竹 {order.SpottedBamboo} 根    资金 ¥{order.Funds}"));
                view.SetOption(0, $"买 1 根斑竹  ¥{WorkshopOrder.SpottedBambooPurchasePrice}",
                    () => Trade(order.TryBuySpottedBamboo, "买入 1 根斑竹。"),
                    order.CanExplore && order.Funds >= WorkshopOrder.SpottedBambooPurchasePrice);
                view.SetOption(1, $"卖 1 根斑竹  +¥{WorkshopOrder.SpottedBambooSalePrice}",
                    () => Trade(order.TrySellSpottedBamboo, $"卖出斑竹，收入 {WorkshopOrder.SpottedBambooSalePrice} 元。"),
                    order.CanExplore && order.SpottedBamboo > 0);
                break;

            case Role.Collector:
                ShowCollector(order);
                break;

            case Role.NightTrader:
                view.SetContent("毁尸灭迹的残次品", Message("黑衣小贩：好竹丝和残次竹丝我都收。还有一瓶特效增白剂，行里可不准用。\n" +
                    $"合格竹丝 {WorkshopInventory.BambooThreads} 根    残次竹丝 {WorkshopInventory.DefectiveThreads} 根    资金 ¥{order.Funds}"));
                view.SetOption(0, $"卖 {WorkshopOrder.ScrapBundle} 根残次竹丝  +¥{WorkshopOrder.ScrapBundlePrice}",
                    () => Trade(() => order.TrySellThreads(true), $"卖出一把残次竹丝，收入 {WorkshopOrder.ScrapBundlePrice} 元。"),
                    order.BlackMarketOpen && WorkshopInventory.DefectiveThreads >= WorkshopOrder.ScrapBundle);
                view.SetOption(1, $"卖 {WorkshopOrder.ThreadBundle} 根竹丝  +¥{WorkshopOrder.ThreadBundlePrice}",
                    () => Trade(() => order.TrySellThreads(false), $"卖出一捆竹丝，收入 {WorkshopOrder.ThreadBundlePrice} 元。"),
                    order.BlackMarketOpen && WorkshopInventory.BambooThreads >= WorkshopOrder.ThreadBundle);
                view.SetOption(2, $"买特效增白剂  ¥{WorkshopOrder.BrightenerPrice}",
                    () => Trade(order.TryBuyBrightener, "增白剂已放入背包。小皮的水桶就在工坊编帘台旁。"),
                    order.BlackMarketOpen && order.Funds >= WorkshopOrder.BrightenerPrice);
                break;

            case Role.XiaoPiBucket:
                view.SetContent("小皮的编帘水桶", Message(order.XiaoPiSabotaged
                    ? "水里已经加过增白剂。等晚上舅舅验货才能知道结果。"
                    : $"小皮在这里给编帘用的线过水。你的背包有 {order.Brightener} 瓶特效增白剂。"));
                if (!order.XiaoPiSabotaged)
                    view.SetPrimary("偷偷倒入 1 瓶", () => Trade(order.TrySabotageXiaoPi, "用掉了 1 瓶增白剂。"),
                        order.CanExplore && order.Brightener > 0);
                break;
        }
    }

    void ShowTea(WorkshopOrder order)
    {
        if (order.TeaNoticeCompleted)
        {
            string tip = order.HasCollectorTip ? "我听说第二天集市东头有个富商，专收好手艺做的罗纹帘。" :
                "乡亲们看到告示，来喝茶时都说要多留个心眼。";
            view.SetContent(TeaTitle, Message("王老板：" + tip));
            return;
        }

        string sentence = chosenWords.Count == 0 ? "（还没选词）" :
            string.Join(" ", chosenWords.ConvertAll(index => TeaWords[index]));
        view.SetContent(TeaTitle, Message("王老板：最近来了个收古董的，借看宝贝把我的旧茶壶骗走了。我认不得几个字，你能替我写张告示吗？\n" +
            $"按顺序选 4 个词组，贴告示用时 {WorkshopOrder.TeaNoticeMinutes} 分钟。\n草稿  {sentence}"));
        for (int i = 0; i < TeaWords.Length; i++)
        {
            int index = i;
            view.SetOption(i, TeaWords[i], () =>
            {
                if (chosenWords.Count < CorrectTeaOrder.Length && !chosenWords.Contains(index))
                    chosenWords.Add(index);
                feedback = null;
                ShowTea(order);
            }, !chosenWords.Contains(i) && chosenWords.Count < CorrectTeaOrder.Length);
        }
        view.SetClear(() =>
        {
            chosenWords.Clear();
            feedback = null;
            ShowTea(order);
        }, chosenWords.Count > 0);
        view.SetPrimary("贴告示", () => PostTeaNotice(order), chosenWords.Count == CorrectTeaOrder.Length);
    }

    void PostTeaNotice(WorkshopOrder order)
    {
        if (chosenWords.Count != CorrectTeaOrder.Length)
            return;
        for (int i = 0; i < CorrectTeaOrder.Length; i++)
        {
            if (chosenWords[i] == CorrectTeaOrder[i])
                continue;
            feedback = "告示读不通。按清空重新排一次。";
            ShowTea(order);
            return;
        }
        string thanks = $"王老板给了你 {WorkshopOrder.TeaNoticeReward} 元茶水费。" +
            (order.Days == 1 ? "\n「明天集市东头有个富商收罗纹帘，你可以提前准备。」" : "");
        Trade(order.TryFinishTeaNotice, thanks, "今天剩余时间不足 30 分钟，明早再来吧。");
    }

    void ShowCollector(WorkshopOrder order)
    {
        WorkshopOrder.QualityGrade? grade = order.CollectorCurtainGrade;
        bool eligible = grade is WorkshopOrder.QualityGrade.S or WorkshopOrder.QualityGrade.A;
        view.SetContent("富商收购罗纹帘", Message(order.CollectorBoughtToday ? "富商：多谢，这一张罗纹就够了。" :
            $"富商：我收一张罗纹帘，综合品相要到 A 或 S 级。底价 {WorkshopOrder.CollectorBasePrice} 元，按品相加价。\n" +
            $"手上罗纹帘 {(grade?.ToString() ?? "无")}    货款计入今日收入。"));
        if (!order.CollectorBoughtToday)
            view.SetPrimary("出售罗纹帘", () => Trade(order.TrySellToCollector, "已售出罗纹帘。"), order.CollectorAvailable && eligible);
    }

    void Trade(Func<bool> transact, string success, string failure = "当前无法交易。")
    {
        feedback = transact() ? success : failure;
        Refresh();
    }
}
