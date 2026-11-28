using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>挂在 BambooAreaMat 上：区域内 E 进入、空格放料、鼠标加工、Esc 返回。</summary>
[DisallowMultipleComponent]
public class BambooWorkbench : MonoBehaviour
{
    [SerializeField] Transform workTable;
    [SerializeField] GameObject[] storedCanes = System.Array.Empty<GameObject>();
    PlayerController player;
    Camera cameraView;
    CameraFollow follow;
    BambooWorkbenchView view;
    BambooCraftProgress practiceProgress;
    bool playerEnabled, followEnabled, wasInArea, held;
    bool wasOrthographic;
    float oldFov, oldOrthographicSize;
    Vector3 cameraPosition, lastPoint;
    Quaternion cameraRotation;
    CursorLockMode oldCursorLock;
    bool oldCursorVisible;
    float lastSampleTime;
    enum LessonPhase { None, Demo, Pupil }
    LessonPhase lessonPhase;
    float pupilX;
    int pupilRow;
    bool pupilJabbed;
    static readonly float[] PupilRows = { .12f, .4f, .68f, .92f };
    public bool IsOpen { get; private set; }
    public bool IsLessonDemo => lessonPhase == LessonPhase.Demo;
    public bool IsLessonPupil => lessonPhase == LessonPhase.Pupil;
    public BambooCraftProgress Progress => practiceProgress ?? BambooCraftSession.Current;
    public BambooWorkbenchView View => view;

    public void Configure(Transform table, GameObject[] canes)
    {
        workTable = table; storedCanes = canes;
    }

    void OnEnable()
    {
        GreenBambooBag.Changed += UpdateStoredCanes;
        UpdateStoredCanes();
    }

    void OnDisable()
    {
        Close(false);
        GreenBambooBag.Changed -= UpdateStoredCanes;
        if (wasInArea) InteractionPromptUI.Hide();
        wasInArea = false;
    }

    void OnDestroy()
    {
        if (view != null) Destroy(view.gameObject);
    }

    void UpdateStoredCanes()
    {
        for (int i = 0; i < storedCanes.Length; i++)
            if (storedCanes[i] != null) storedCanes[i].SetActive(i < GreenBambooBag.Count);
    }

    public bool Contains(Vector3 worldPosition)
    {
        Vector3 p = transform.InverseTransformPoint(worldPosition);
        return Mathf.Abs(p.x) <= 0.5f && Mathf.Abs(p.z) <= 0.5f && Mathf.Abs(worldPosition.y - transform.position.y) < 3f;
    }

    void Update()
    {
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (SceneTransitionState.IsLoading)
        {
            Close(false);
            return;
        }
        if (Time.timeScale <= 0)
        {
            CancelPointer();
            return;
        }
        Keyboard keyboard = Keyboard.current;
        if (!IsOpen)
        {
            var order = WorkshopOrder.Current;
            bool inArea = player != null && Contains(player.transform.position) &&
                !GameFlowModalState.IsInteractionBlocked;
            if (order?.CurrentLesson == WorkshopOrder.LessonBeat.Scrape)
            {
                if (inArea)
                    InteractionPromptUI.Show("按 E 教艾琳刮青");
                else if (wasInArea)
                    InteractionPromptUI.Hide();
                wasInArea = inArea;
                if (inArea && SceneTransitionManager.TryConsumeInteractPress())
                    OpenLesson();
                return;
            }
            bool canWork = order == null || order.CanWorkBamboo;
            if (inArea) InteractionPromptUI.Show(!canWork
                ? order.DayEnded ? "今日已收工" : order.IntroductionHint ?? "先采集完整青竹或到集市购买斑竹"
                : order != null && order.SpottedBamboo > 0
                    ? "按 E 篾竹，进台后按 V 放置斑竹" : "按 E 进入篾竹工作台");
            else if (wasInArea) InteractionPromptUI.Hide();
            wasInArea = inArea;
            if (inArea && canWork && (order == null ? keyboard?.eKey.wasPressedThisFrame == true
                : SceneTransitionManager.TryConsumeInteractPress())) Open();
            return;
        }
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            Close();
            return;
        }
        if (lessonPhase == LessonPhase.Pupil)
        {
            DriveAilinScrape();
            return;
        }
        if (lessonPhase == LessonPhase.Demo)
        {
            StrokeFromPointer();
            if (Progress.Stage != BambooCraftProgress.Step.Scraping)
                BeginAilinScrape();
            return;
        }
        var activeOrder = WorkshopOrder.Current;
        if (activeOrder?.DayEnded == true)
        {
            Close();
            return;
        }
        if (keyboard?.vKey.wasPressedThisFrame == true && activeOrder != null)
        {
            if (Progress.PlaceSpotted(activeOrder.TryTakeSpottedBamboo)) held = false;
            activeOrder.ObserveBamboo(Progress);
            view.Refresh();
        }
        else if (keyboard?.spaceKey.wasPressedThisFrame == true)
        {
            bool changed = Progress.Advance();
            if (changed) held = false;
            view.Refresh();
        }
        if (Progress.IsFinishingStage)
        {
            held = false;
            float vertical = keyboard == null ? 0 : (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
            bool pulling = Mouse.current != null && Mouse.current.rightButton.isPressed;
            Progress.TickFinishing(Time.deltaTime, vertical, pulling);
            WorkshopOrder.Current?.ObserveBamboo(Progress);
            view.FinishingInput(pulling);
            return;
        }
        StrokeFromPointer();
        WorkshopOrder.Current?.ObserveBamboo(Progress);
    }

    void StrokeFromPointer()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) { CancelPointer(); return; }
        Vector2 screen = mouse.position.ReadValue();
        view.ScreenToSurface(screen, out Vector2 point);
        if (mouse.leftButton.isPressed && !held)
        {
            held = true;
            Progress.BeginStroke(point);
            lastPoint = point;
            lastSampleTime = Time.unscaledTime;
        }
        int removed = 0;
        float speed = 0;
        if (held && mouse.leftButton.isPressed)
        {
            Vector2 delta = point - (Vector2)lastPoint;
            float distance = new Vector2(delta.x, delta.y * 0.18f).magnitude;
            if (distance > 0.0001f)
            {
                float dt = Mathf.Max(0.001f, Mathf.Min(Time.unscaledDeltaTime, Time.unscaledTime - lastSampleTime));
                speed = distance / dt;
                removed = Progress.Drag(point, dt);
                lastSampleTime = Time.unscaledTime;
                lastPoint = point;
            }
        }
        if (mouse.leftButton.wasReleasedThisFrame)
        {
            if (held) Progress.Drag(point, Mathf.Max(0.001f, Time.unscaledDeltaTime));
            if (Progress.EndStroke()) view.PlaySplit();
            held = false;
        }
        view.Pointer(screen, held && mouse.leftButton.isPressed, speed, removed);
    }

    bool OpenLesson()
    {
        if (!Open(false, BambooCraftProgress.Step.Scraping, lesson: true))
            return false;
        Progress.BeginLessonScrape("先刮给艾琳看。按住左键，顺着竹青刮到净。");
        lessonPhase = LessonPhase.Demo;
        view.ShowKnife(false);
        view.Refresh();
        return true;
    }

    void BeginAilinScrape()
    {
        held = false;
        Progress.CancelStroke();
        Progress.BeginLessonScrape("换艾琳来刮。刀自己走。");
        lessonPhase = LessonPhase.Pupil;
        pupilX = 0f;
        pupilRow = 0;
        pupilJabbed = false;
        Progress.BeginStroke(new Vector2(0f, PupilRows[0]));
        view.Refresh();
        AilinDanmaku.Play(view.transform,
            "这要用多大力啊", "刀好沉", "这怎么这么难", "竹青好滑", "我怕刮破了");
    }

    void DriveAilinScrape()
    {
        if (Progress.Stage != BambooCraftProgress.Step.Scraping)
        {
            FinishAilinScrape();
            return;
        }
        float next = Mathf.Min(1f, pupilX + Mathf.Min(.02f, .5f * Time.deltaTime));
        float y = PupilRows[Mathf.Clamp(pupilRow, 0, PupilRows.Length - 1)];
        if (!pupilJabbed && pupilX < .51f && next >= .51f)
        {
            pupilJabbed = true;
            Progress.Drag(new Vector2(.56f, y), .008f);
            next = Mathf.Max(next, .56f);
        }
        int removed = Progress.Drag(new Vector2(next, y), Mathf.Max(.016f, Time.deltaTime));
        pupilX = next;
        view.Pointer(LessonScreen(next, y), true, .5f, removed);
        if (Progress.Stage != BambooCraftProgress.Step.Scraping)
        {
            FinishAilinScrape();
            return;
        }
        if (pupilX < .98f)
            return;
        pupilX = 0f;
        pupilRow++;
        if (pupilRow >= PupilRows.Length)
            pupilRow = 0;
        Progress.EndStroke();
        Progress.BeginStroke(new Vector2(0f, PupilRows[pupilRow]));
    }

    void FinishAilinScrape()
    {
        string line = Progress.Damage > 0
            ? "艾琳，竹节上太快了。经过竹节要把刀放慢，别留下深痕。"
            : "艾琳，刀要贴着竹青刮。这一遍刮净了，就这样。";
        Close();
        AilinLessonTalk.Show(line, () =>
            WorkshopOrder.Current?.CompleteLessonPass(WorkshopOrder.LessonBeat.Scrape));
    }

    Vector2 LessonScreen(float x, float y)
    {
        RectTransform rect = view.SurfaceRect;
        Rect area = rect.rect;
        Vector3 world = rect.TransformPoint(new Vector3(
            Mathf.Lerp(area.xMin, area.xMax, x), Mathf.Lerp(area.yMin, area.yMax, y), 0f));
        return RectTransformUtility.WorldToScreenPoint(null, world);
    }

    public bool Open() => Open(false, BambooCraftProgress.Step.PlaceBamboo, false);

    public void OpenForPractice(BambooCraftProgress.Step step)
    {
        if (WorkshopPracticeMode.Enabled)
            Open(true, step, false);
    }

    bool Open(bool directPractice, BambooCraftProgress.Step step, bool lesson)
    {
        if (player == null)
            player = FindFirstObjectByType<PlayerController>();
        if (IsOpen || player == null || (!directPractice && !Contains(player.transform.position)) ||
            SceneTransitionState.IsLoading || GameFlowModalState.IsInteractionBlocked)
            return false;
        cameraView = player.GameplayCamera != null ? player.GameplayCamera : Camera.main;
        if (cameraView == null) return false;
        var order = WorkshopOrder.Current;
        if (!lesson && order != null && !order.CanWorkBamboo)
            return false;
#if UNITY_EDITOR
        if (WorkshopPracticeMode.Enabled && !lesson)
            practiceProgress = BambooCraftProgress.CreatePractice(step, order?.HasUpgradedKnife == true);
#endif
        if (lesson)
            practiceProgress = new BambooCraftProgress(() => 0f, () => false);
        Progress.UpgradedKnife = !lesson && order?.HasUpgradedKnife == true;
        GameFlowModalState.Acquire(this);
        if (view == null) view = BambooWorkbenchView.Create(Progress);
        else
        {
            view.Bind(Progress);
            view.gameObject.SetActive(true);
        }
        view.ShowKnife(Progress.UpgradedKnife);
        playerEnabled = player.enabled;
        player.enabled = false;
        follow = cameraView.GetComponent<CameraFollow>();
        followEnabled = follow != null && follow.enabled;
        if (follow != null) follow.enabled = false;
        cameraPosition = cameraView.transform.position;
        cameraRotation = cameraView.transform.rotation;
        oldFov = cameraView.fieldOfView;
        wasOrthographic = cameraView.orthographic;
        oldOrthographicSize = cameraView.orthographicSize;
        Vector3 focus = workTable != null ? workTable.position + Vector3.up * 0.9f : transform.position + Vector3.up;
        cameraView.transform.position = focus + new Vector3(0, 3.1f, -1.3f);
        cameraView.transform.LookAt(focus);
        cameraView.orthographic = false;
        cameraView.fieldOfView = 48;
        oldCursorLock = Cursor.lockState; oldCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        IsOpen = true;
        InteractionPromptUI.Hide();
        return true;
    }

    public void Close() => Close(true);

    void Close(bool waitForInteractRelease)
    {
        if (!IsOpen) return;
        CancelPointer();
        if (view != null) view.gameObject.SetActive(false);
        if (player != null) player.enabled = playerEnabled;
        if (cameraView != null)
        {
            cameraView.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            cameraView.fieldOfView = oldFov;
            cameraView.orthographic = wasOrthographic;
            cameraView.orthographicSize = oldOrthographicSize;
        }
        if (follow != null) follow.enabled = followEnabled;
        Cursor.lockState = oldCursorLock; Cursor.visible = oldCursorVisible;
        IsOpen = false;
        lessonPhase = LessonPhase.None;
        practiceProgress = null;
        AilinDanmaku.Stop();
        GameFlowModalState.Release(this, waitForInteractRelease);
    }

    void CancelPointer()
    {
        held = false;
        Progress.CancelStroke();
        if (view != null && view.gameObject.activeSelf) view.Pointer(Vector2.zero, false, 0, 0);
    }

    void OnApplicationFocus(bool focused) { if (!focused) CancelPointer(); }
}
