using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>A marked grove with an active patrol and a real-time chop QTE.</summary>
[DisallowMultipleComponent]
public sealed class VillageSpottedGrove : MonoBehaviour
{
    public const float SightRange = 5.5f;
    public const float SightHalfAngle = 38f;
    public const float QteSeconds = 5f;
    public const int QtePresses = 8;

    [SerializeField] Transform entrance;
    [SerializeField] Transform bamboo;
    [SerializeField] Transform patrolDog;
    [SerializeField] Transform[] patrolRoute;
    [SerializeField] GameObject harvestableStalks;
    [SerializeField] GameObject sightMarker;

    static VillageSpottedGrove active;
    PlayerController player;
    SettlementExitController timingLock;
    VillageActivityView timingView;
    Text sneakHint;
    int waypoint;
    int presses;
    float deadline;
    bool timing;
    bool nearEntrance;
    string entrancePrompt;
    GameObject qteMeters;
    Image timeFill;
    Image pressFill;
    AudioSource sounds;
    AudioClip chopClip;
    AudioClip coverClip;
    AudioClip alarmClip;
    int displayedTenths = -1;
    int displayedPresses = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession() => active = null;

    public static bool IsSneaking => active != null;
    public bool IsTiming => timing;

    public void Configure(Transform start, Transform clump, Transform dog, Transform[] route, GameObject stalks, GameObject sight)
    {
        entrance = start;
        bamboo = clump;
        patrolDog = dog;
        patrolRoute = route;
        harvestableStalks = stalks;
        sightMarker = sight;
    }

    void Update()
    {
        var order = WorkshopOrder.Current;
        if (harvestableStalks != null && order != null && harvestableStalks.activeSelf == order.SpottedGroveHarvested)
            harvestableStalks.SetActive(!order.SpottedGroveHarvested);

        Keyboard keyboard = Keyboard.current;
        if (timingLock != null && timingLock.IsSettlementActive)
        {
            if (keyboard?.escapeKey.wasPressedThisFrame == true)
            {
                if (timing)
                    Resolve(order, caught: true);
                else
                    CloseTimingView();
            }
            else if (timing)
                TickChop(order, keyboard);
            return;
        }

        if (order == null || !order.CanVisitSpottedGrove || SceneTransitionState.IsLoading)
        {
            StopSneaking();
            HideEntrancePrompt();
            return;
        }
        if (GameFlowModalState.IsInteractionBlocked)
            return;

        WalkPatrol();
        if (player == null)
            player = FindFirstObjectByType<PlayerController>();
        if (player == null || entrance == null || bamboo == null)
            return;
        Vector3 position = player.transform.position;
        if (active != this)
        {
            CheckEntrance(order, position);
            return;
        }

        if (keyboard?.escapeKey.wasPressedThisFrame == true || FlatDistance(position, entrance.position) > 19f)
        {
            StopSneaking();
            return;
        }
        if (patrolDog != null && SeesPlayer(patrolDog.position, patrolDog.forward, position))
        {
            Resolve(order, caught: true);
            return;
        }
        bool nearBamboo = FlatDistance(position, bamboo.position) <= 2.15f;
        string message = nearBamboo ? "按空格砍斑竹   ·   Esc 放弃" :
            "绕开黄狗脚前的视线，靠近紫色斑竹   ·   Esc 放弃";
        if (sneakHint.text != message)
            sneakHint.text = message;
        if (nearBamboo && keyboard?.spaceKey.wasPressedThisFrame == true)
            BeginChop();
    }

    void CheckEntrance(WorkshopOrder order, Vector3 position)
    {
        bool inRange = FlatDistance(position, entrance.position) <= 2.35f;
        if (!inRange)
        {
            HideEntrancePrompt();
            return;
        }
        entrancePrompt = order.CanStealSpottedBamboo ? "按 E 潜入李婶的斑竹地" :
            order.SpottedGroveHarvested ? "这里的斑竹已经砍过了" : "黄狗还在警觉，明天再来";
        nearEntrance = true;
        InteractionPromptUI.Show(entrancePrompt);
        if (!order.CanStealSpottedBamboo || !SceneTransitionManager.TryConsumeInteractPress())
            return;
        ShowBriefing();
    }

    void ShowBriefing()
    {
        EnsureTimingView();
        timingView.SetContent("邻居李婶的斑竹",
            "李婶家的紫斑竹是做高级花纹宣纸帘的好料，正规集市售价 100 元/根。\n" +
            "避开黄狗脚前的黄色视线范围，靠近紫竹后按空格砍竹。遇到硬竹节发出响声时，在 5 秒内连按 8 次空格掩声。\n" +
            "成功获得 2 根极品斑竹：可加工成帘，让舅舅的订单底价翻倍；也可两根合计卖 120 元。被抓扣 100 元。潜行中 Esc 可放弃，掩声中退出算被抓。");
        timingView.SetExpandedBody();
        timingView.SetPrimary("开始潜入", BeginSneaking);
        HideEntrancePrompt();
        timingLock.BeginSettlement(timingView.Root);
    }

    void BeginSneaking()
    {
        if (WorkshopOrder.Current?.CanStealSpottedBamboo != true)
        {
            CloseTimingView();
            return;
        }
        timingLock.CloseAndResume();
        active = this;
        HideEntrancePrompt();
        if (sneakHint == null)
            BuildSneakHint();
        sneakHint.transform.parent.gameObject.SetActive(true);
        if (sightMarker != null)
            sightMarker.SetActive(true);
    }

    void WalkPatrol()
    {
        if (patrolDog == null || patrolRoute == null || patrolRoute.Length == 0)
            return;
        Vector3 target = patrolRoute[waypoint].position;
        Vector3 delta = target - patrolDog.position;
        delta.y = 0f;
        if (delta.sqrMagnitude <= .02f)
        {
            waypoint = (waypoint + 1) % patrolRoute.Length;
            return;
        }
        patrolDog.rotation = Quaternion.RotateTowards(patrolDog.rotation,
            Quaternion.LookRotation(delta), Time.deltaTime * 315f);
        patrolDog.position = Vector3.MoveTowards(patrolDog.position,
            new Vector3(target.x, patrolDog.position.y, target.z), Time.deltaTime * 1.9f);
    }

    public static bool SeesPlayer(Vector3 dogPosition, Vector3 facing, Vector3 playerPosition)
    {
        Vector3 offset = playerPosition - dogPosition;
        offset.y = 0f;
        if (offset.sqrMagnitude > SightRange * SightRange)
            return false;
        return offset.sqrMagnitude < .25f || Vector3.Angle(facing, offset) <= SightHalfAngle;
    }

    void BuildSneakHint()
    {
        var root = new GameObject("潜行提示层", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        var bg = WorkshopStoryUI.Panel(root.transform, "潜行", new Rect(.29f, .065f, .42f, .061f),
            new Color32(48, 31, 29, 234));
        sneakHint = WorkshopStoryUI.Label(bg.transform, "潜行操作", "", new Rect(.03f, .08f, .94f, .84f),
            27, WorkshopStoryUI.Paper, bold: true);
    }

    void BeginChop()
    {
        EnsureTimingView();
        if (!timingLock.BeginSettlement(timingView.Root))
            return;
        timing = true;
        presses = 0;
        displayedTenths = displayedPresses = -1;
        deadline = Time.unscaledTime + QteSeconds;
        sneakHint.transform.parent.gameObject.SetActive(false);
        timingView.SetContent("硬竹节发出了响声！", "");
        qteMeters.SetActive(true);
        sounds.PlayOneShot(chopClip);
        DrawChopTime(QteSeconds);
    }

    void TickChop(WorkshopOrder order, Keyboard keyboard)
    {
        float remaining = deadline - Time.unscaledTime;
        if (remaining <= 0f)
        {
            Resolve(order, caught: true);
            return;
        }
        if (keyboard?.spaceKey.wasPressedThisFrame == true)
        {
            presses++;
            sounds.PlayOneShot(coverClip, .65f);
        }
        if (presses >= QtePresses)
            Resolve(order, caught: false);
        else
            DrawChopTime(remaining);
    }

    void DrawChopTime(float remaining)
    {
        int tenths = Mathf.CeilToInt(Mathf.Max(0, remaining) * 10);
        if (tenths == displayedTenths && presses == displayedPresses) return;
        displayedTenths = tenths;
        displayedPresses = presses;
        timingView.SetBody($"黄狗听见了响动！连按空格，用竹叶沙沙声掩住砍竹声。\n\n还剩 {tenths / 10f:0.0} 秒    空格 {presses}/{QtePresses}");
        timeFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(remaining / QteSeconds), 1);
        timeFill.color = remaining < 1.5f ? new Color32(179, 62, 47, 255) : WorkshopStoryUI.Green;
        pressFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)presses / QtePresses), 1);
    }

    void Resolve(WorkshopOrder order, bool caught)
    {
        bool resolved = order != null && order.TryResolveSpottedBambooTheft(caught);
        StopSneaking();
        if (!resolved)
        {
            CloseTimingView();
            return;
        }
        EnsureTimingView();
        qteMeters.SetActive(false);
        if (caught) sounds.PlayOneShot(alarmClip);
        string resultMessage = caught
            ? $"黄狗汪汪叫起来！李婶揪住你的耳朵，把你带到舅舅面前告状。\n李婶：好好的大学生，怎么惦记我家的竹子！\n舅舅：给李婶赔礼，这 {WorkshopOrder.SpottedBambooPenalty} 元从你的钱里扣。\n\n资金 -100 元。没有获得斑竹，黄狗今天会提高警惕，明天才能再试。"
            : "你趁黄狗没发现，带走了 2 根极品斑竹！\n\n倒卖：集市杂货商每根收 60 元，两根共 120 元。\n加工：篾竹台按 V 放入斑竹，整张帘用斑竹做成后，舅舅的订单底价翻倍。\n\n这片竹丛已经采过，本局不会再次发放奖励。";
        if (order.IntroductionHint != null)
            resultMessage += "\n\n接回主线：" + order.IntroductionHint + "。";
        timingView.SetContent(caught ? "被李婶逮住了" : "邻居李婶的斑竹 · 得手", resultMessage);
        timingView.SetExpandedBody();
        timingView.SetPrimary("回到村里", CloseTimingView);
        if (!timingLock.IsSettlementActive)
            timingLock.BeginSettlement(timingView.Root);
    }

    void EnsureTimingView()
    {
        if (timingView != null)
            return;
        timingLock = gameObject.AddComponent<SettlementExitController>();
        timingView = new VillageActivityView(transform, LeaveTimingView);
        qteMeters = WorkshopStoryUI.Panel(timingView.Root.transform, "掩声进度层", new Rect(.28f, .31f, .44f, .18f), Color.clear).gameObject;
        WorkshopStoryUI.Label(qteMeters.transform, "剩余时间标签", "剩余时间", new Rect(0, .76f, 1, .24f), 24, WorkshopStoryUI.Ink);
        var timeTrack = WorkshopStoryUI.Panel(qteMeters.transform, "剩余时间条", new Rect(0, .55f, 1, .15f), WorkshopStoryUI.Wood);
        timeFill = WorkshopStoryUI.Panel(timeTrack.transform, "时间填充", new Rect(0, 0, 1, 1), WorkshopStoryUI.Green);
        WorkshopStoryUI.Label(qteMeters.transform, "掩声进度标签", "掩声进度", new Rect(0, .24f, 1, .24f), 24, WorkshopStoryUI.Ink);
        var pressTrack = WorkshopStoryUI.Panel(qteMeters.transform, "掩声进度条", new Rect(0, 0, 1, .15f), WorkshopStoryUI.Wood);
        pressFill = WorkshopStoryUI.Panel(pressTrack.transform, "掩声填充", new Rect(0, 0, 0, 1), WorkshopStoryUI.Green);
        qteMeters.SetActive(false);
        sounds = gameObject.AddComponent<AudioSource>();
        sounds.playOnAwake = false;
        sounds.volume = .35f;
        chopClip = CreateSound("斑竹硬节咔声", .18f, 180, .75f);
        coverClip = CreateSound("竹叶掩声", .12f, 950, .95f);
        alarmClip = CreateSound("黄狗警报", .45f, 260, .35f);
    }

    void LeaveTimingView()
    {
        if (timing)
            Resolve(WorkshopOrder.Current, caught: true);
        else
            CloseTimingView();
    }

    void CloseTimingView()
    {
        StopSneaking();
        if (qteMeters != null) qteMeters.SetActive(false);
        if (timingLock != null)
            timingLock.CloseAndResume();
    }

    void StopSneaking()
    {
        if (active != this)
            return;
        active = null;
        timing = false;
        if (sneakHint != null)
            sneakHint.transform.parent.gameObject.SetActive(false);
        if (sightMarker != null)
            sightMarker.SetActive(false);
    }

    void HideEntrancePrompt()
    {
        if (nearEntrance && InteractionPromptUI.CurrentMessage == entrancePrompt)
            InteractionPromptUI.Hide();
        nearEntrance = false;
    }

    void OnDisable()
    {
        // Leaving a scene during the live QTE must not erase the risk or award materials.
        if (timing && active == this && Application.isPlaying)
            WorkshopOrder.Current?.TryResolveSpottedBambooTheft(caught: true);
        CloseTimingView();
        HideEntrancePrompt();
    }

    void OnDestroy()
    {
        if (chopClip != null) Destroy(chopClip);
        if (coverClip != null) Destroy(coverClip);
        if (alarmClip != null) Destroy(alarmClip);
    }

    static AudioClip CreateSound(string name, float seconds, float frequency, float noiseMix)
    {
        const int rate = 22050;
        var samples = new float[Mathf.CeilToInt(rate * seconds)];
        uint noise = 173u;
        for (int i = 0; i < samples.Length; i++)
        {
            noise = noise * 1664525u + 1013904223u;
            float hiss = ((noise >> 8) / 16777215f) * 2f - 1f;
            float t = (float)i / rate;
            float envelope = Mathf.Sin(Mathf.PI * i / samples.Length) * Mathf.Exp(-t * 13);
            samples[i] = envelope * Mathf.Lerp(Mathf.Sin(2 * Mathf.PI * frequency * t), hiss, noiseMix);
        }
        AudioClip clip = AudioClip.Create(name, samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
}
