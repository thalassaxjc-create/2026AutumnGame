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
    int displayedOrderRevision = -1;
    int displayedInventoryRevision = -1;

    public Role MerchantRole => role;
    public bool IsDialogueAvailable => WorkshopOrder.Current is { } order && (role switch
    {
        Role.TeaOwner => order.CanVisitTeaStall,
        Role.LiAunt => order.CanVisitSpottedGrove,
        Role.Collector => order.CollectorVisiting,
        Role.NightTrader => order.BlackMarketOpen,
        Role.XiaoPiBucket => order.CanDoSideQuests,
        _ => order.CanExplore
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
        if (closedCallback != null && role is Role.NightTrader or Role.XiaoPiBucket)
        {
            if (!IsDialogueAvailable)
            {
                Close();
                return;
            }
            if (displayedOrderRevision != WorkshopOrder.Current.Revision ||
                displayedInventoryRevision != WorkshopInventory.Revision)
                Refresh();
        }
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
        displayedOrderRevision = order.Revision;
        displayedInventoryRevision = WorkshopInventory.Revision;

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
                    "竹林靠我家那边有一片紫斑竹，是做高级花纹帘的好料，黄狗替我看着。要用就去集市买，100 元一根，可别打我家竹子的主意。";
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
                view.SetContent("毁尸灭迹的残次品", Message("黑衣小贩：每天 18:00 后在村口找我。废竹丝一把 25 根，只给 5 元。\n" +
                    "特效增白剂 20 元/瓶：会腐蚀宣纸，是行内禁品。小皮的编帘水桶在工坊编帘台旁。\n" +
                    $"残次竹丝 {WorkshopInventory.DefectiveThreads} 根    合格竹丝 {WorkshopInventory.BambooThreads} 根    增白剂 {order.Brightener} 瓶    资金 ¥{order.Funds}"));
                view.SetOption(0, $"卖 {WorkshopOrder.ScrapBundle} 根残次竹丝  +¥{WorkshopOrder.ScrapBundlePrice}",
                    () => Trade(() => order.TrySellThreads(true), $"卖出一把残次竹丝，收入 {WorkshopOrder.ScrapBundlePrice} 元。"),
                    order.BlackMarketOpen && WorkshopInventory.DefectiveThreads >= WorkshopOrder.ScrapBundle);
                view.SetOption(1, $"卖 {WorkshopOrder.ThreadBundle} 根竹丝  +¥{WorkshopOrder.ThreadBundlePrice}",
                    () => Trade(() => order.TrySellThreads(false), $"卖出一捆竹丝，收入 {WorkshopOrder.ThreadBundlePrice} 元。"),
                    order.BlackMarketOpen && WorkshopInventory.BambooThreads >= WorkshopOrder.ThreadBundle);
                view.SetOption(2, $"买特效增白剂  ¥{WorkshopOrder.BrightenerPrice}",
                    () => Trade(order.TryBuyBrightener, "花费 20 元，获得 1 瓶增白剂。靠近小皮水桶按 E，可选择偷偷倒入。"),
                    order.BlackMarketOpen && order.Funds >= WorkshopOrder.BrightenerPrice);
                break;

            case Role.XiaoPiBucket:
                view.SetContent("小皮的编帘水桶", Message(order.XiaoPiSabotaged
                    ? "水面泛起不自然的白色，旁边小皮今天的帘子也发白了。\n今晚舅舅验货会发现质量问题，从小皮今天的收入中扣除 100 元。\n今天已投放过，不能重复投放或叠加扣款。"
                    : "小皮在这里给编帘用的线过水。\n偷偷倒入 1 瓶特效增白剂，会让他今天做的帘子发白；晚上验货时，小皮当天收入扣除 100 元。\n" +
                        $"背包：增白剂 {order.Brightener} 瓶。" + (order.Brightener == 0 ? "\n18:00 后可到村口黑衣小贩处花 20 元购买。" : "\n可以选择离开，不消耗瓶子。")));
                view.SetExpandedBody();
                if (!order.XiaoPiSabotaged)
                    view.SetPrimary("偷偷倒入 1 瓶", () => Trade(order.TrySabotageXiaoPi, "用掉了 1 瓶增白剂。"),
                        order.CanSabotageXiaoPi);
                break;
        }
    }

    void ShowTea(WorkshopOrder order)
    {
        if (order.TeaNoticeCompleted)
        {
            string tip = !order.HasCollectorTip ? "乡亲们看到告示，都说要多留个心眼。" : order.Days == 1
                ? "明天集市东头会有个富商来收高档货，你可以提前准备。\n情报已解锁：第二天 · A / S 级龟纹小帘。"
                : order.Days == 2 ? "富商今天就在集市东头，收 A / S 级龟纹小帘。" : "那位富商第二天来过集市，告示还替大家提着醒呢。";
            view.SetContent(TeaTitle + " · 已完成", "王老板（感激地）：大学生就是有学问！告示写得明明白白，这下乡亲们不会再上当了。\n" +
                $"茶水费 {WorkshopOrder.TeaNoticeReward} 元已领取 · 用时 {WorkshopOrder.TeaNoticeMinutes} 分钟 · 不耗体力\n\n「{tip}」");
            view.SetTeaDraft("已张贴：请乡亲们留意以鉴宝为由骗走旧物的人。", null, false);
            return;
        }

        string sentence = string.Join("  ", chosenWords.ConvertAll(index => TeaWords[index]));
        for (int i = chosenWords.Count; i < CorrectTeaOrder.Length; i++) sentence += "  ____";
        view.SetContent(TeaTitle, Message("王老板（愁眉苦脸）：收古董的借鉴宝骗走了我的旧茶壶！我不识几个字，你是大学生，帮我写告示提醒乡亲们吧。\n" +
            $"按顺序选 4 个词组 · 完成用时 {WorkshopOrder.TeaNoticeMinutes} 分钟 · 不耗体力 · 茶水费 {WorkshopOrder.TeaNoticeReward} 元"));
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
        view.SetTeaDraft($"告示草稿（{chosenWords.Count}/4）：{sentence}", () =>
        {
            if (chosenWords.Count > 0) chosenWords.RemoveAt(chosenWords.Count - 1);
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
            feedback = "王老板：这是提醒谁、留意什么人呢？可以撤回或清空重排。选错不扣时间，也不耗体力。";
            ShowTea(order);
            return;
        }
        string thanks = $"王老板给了你 {WorkshopOrder.TeaNoticeReward} 元茶水费。" +
            (order.Days == 1 ? "\n「明天集市东头会有个富商来收高档货，你可以提前准备。」" : "");
        Trade(order.TryFinishTeaNotice, thanks, "今天剩余时间不足 30 分钟，明早再来吧。");
    }

    void ShowCollector(WorkshopOrder order)
    {
        bool eligible = order.PendingCurtainGrade is WorkshopOrder.QualityGrade.S or WorkshopOrder.QualityGrade.A;
        view.SetContent("富商的龟纹订单", Message(order.CollectorBoughtToday ? "富商：我已收好这张帘，今天的货齐了。" :
            $"富商：我只收一张 A 或 S 级的龟纹小帘，底价 {WorkshopOrder.CollectorBasePrice} 元，按品相加价。\n" +
            $"手上成品 {(order.PendingCurtainGrade?.ToString() ?? "无")}    卖给我也计入今天的交付张数。"));
        if (!order.CollectorBoughtToday)
            view.SetPrimary("卖给富商", () => Trade(order.TrySellToCollector, "已交付给富商。"), order.CollectorAvailable && eligible);
    }

    void Trade(Func<bool> transact, string success, string failure = "当前无法交易。")
    {
        feedback = transact() ? success : failure;
        Refresh();
    }
}
