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

        if (order == null || !order.CanExplore || SceneTransitionState.IsLoading)
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
        deadline = Time.unscaledTime + QteSeconds;
        sneakHint.transform.parent.gameObject.SetActive(false);
        timingView.SetContent("竹枝响了", "");
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
            presses++;
        if (presses >= QtePresses)
            Resolve(order, caught: false);
        else
            DrawChopTime(remaining);
    }

    void DrawChopTime(float remaining) => timingView.SetBody(
        $"黄狗听见了响动！在 5 秒内连按空格，及时掩住响声。\n\n还剩 {remaining:0.0} 秒    空格 {presses}/{QtePresses}");

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
        timingView.SetContent("李婶的斑竹", caught
            ? $"黄狗叫了起来。李婶找舅舅告了状，你被扣了 {WorkshopOrder.SpottedBambooPenalty} 元。"
            : "你趁黄狗没发现，带走了 2 根极品斑竹。可以拿去集市卖，或在篾竹台按 V 放入斑竹。");
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
        CloseTimingView();
        HideEntrancePrompt();
    }

    static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
}
