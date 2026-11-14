using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 刷大漆小游戏：Cytus 风格上下扫描线、双色鼠标点按与方向拖拽漆条。
/// UI 和抽象帘面全部由 UGUI 在运行时生成，不依赖外部图片。
/// </summary>
[DisallowMultipleComponent]
public sealed class LacquerMinigameController : MonoBehaviour
{
    public enum LacquerGrade
    {
        S,
        A,
        B,
        C,
        D
    }

    [Serializable]
    public struct LacquerResult
    {
        public LacquerGrade grade;
        public float accuracy;
        public int score;
        public int maxCombo;
        public int perfectCount;
        public int greatCount;
        public int goodCount;
        public int missCount;
        public int gameMinutes;
    }

    enum Phase
    {
        Closed,
        Ready,
        Countdown,
        Playing,
        Result
    }

    enum MarkType
    {
        Tap,
        Stroke
    }

    enum PaintColor
    {
        Red,
        Green
    }

    enum Judgement
    {
        Perfect,
        Great,
        Good,
        Miss
    }

    sealed class MarkSpec
    {
        public MarkType type;
        public PaintColor color;
        public float targetTime;
        public int index;
        public Vector2 start;
        public Vector2 end;

        public Vector2 Center => (start + end) * 0.5f;
    }

    sealed class ActiveMark
    {
        public MarkSpec spec;
        public RectTransform rect;
        public LacquerNoteGraphic graphic;
        public CanvasGroup canvasGroup;
        public bool dragging;
        public float dragProgress;
        public Judgement startJudgement;
        public int dragSamples;
        public int outsideSamples;
    }

    [Header("场景引用")]
    [SerializeField] GameObject canvasRoot;

    [Header("订单")]
    [SerializeField, Min(8)] int totalMarks = 32;
    [SerializeField, Min(2)] int strokeEvery = 4;
    [SerializeField, Min(1)] int gameMinutesCost = 20;

    [Header("扫描与判定")]
    [SerializeField, Min(2f)] float scanPassDuration = 4.2f;
    [SerializeField, Min(0.3f)] float preShowTime = 1.25f;
    [SerializeField, Min(0.03f)] float perfectWindow = 0.10f;
    [SerializeField, Min(0.05f)] float greatWindow = 0.20f;
    [SerializeField, Min(0.08f)] float goodWindow = 0.32f;
    [SerializeField, Min(0.12f)] float missWindow = 0.48f;
    [SerializeField, Min(0.5f)] float dragDeadline = 1.7f;
    [SerializeField, Min(20f)] float dragCorridor = 66f;

    const int MarksPerPass = 8;
    const float LeftBound = -535f;
    const float RightBound = 535f;
    const float BottomBound = -255f;
    const float TopBound = 255f;

    static readonly Color RedLacquer = new Color32(184, 55, 43, 255);
    static readonly Color GreenLacquer = new Color32(50, 132, 86, 255);

    readonly List<MarkSpec> schedule = new List<MarkSpec>(48);
    readonly List<ActiveMark> activeMarks = new List<ActiveMark>(16);

    Phase phase = Phase.Closed;
    Action exitRequested;
    Font uiFont;
    AudioSource audioSource;
    AudioClip redClip;
    AudioClip greenClip;
    AudioClip strokeClip;
    AudioClip missClip;

    RectTransform playArea;
    RectTransform noteLayer;
    RectTransform scanGlow;
    RectTransform scanLine;
    RectTransform progressFill;
    Text progressText;
    Text scoreText;
    Text feedbackText;
    Text countdownText;
    Text resultText;
    Text scanDirectionText;
    GameObject introPanel;
    GameObject resultPanel;
    LacquerCurtainGraphic curtainGraphic;

    int nextSpawnIndex;
    int judgedCount;
    int score;
    int combo;
    int maxCombo;
    int perfectCount;
    int greatCount;
    int goodCount;
    int missCount;
    int roundNumber;
    float songTime;
    float countdownRemaining;
    float feedbackRemaining;
    bool initialized;

    ActiveMark activeDrag;
    CursorLockMode previousCursorLock;
    bool previousCursorVisible;
    bool cursorStateStored;

    public bool IsOpen => phase != Phase.Closed;
    public int TotalMarks => totalMarks;
    public event Action<LacquerResult> Completed;

    void Awake()
    {
        EnsureInitialized();
        if (canvasRoot != null)
            canvasRoot.SetActive(false);
    }

    void Update()
    {
        if (phase == Phase.Closed)
            return;

        UpdateFeedbackFade();
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            RequestExit();
            return;
        }

        switch (phase)
        {
            case Phase.Ready:
                if ((keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)) ||
                    (mouse != null && mouse.leftButton.wasPressedThisFrame))
                    BeginCountdown();
                break;

            case Phase.Countdown:
                UpdateCountdown();
                break;

            case Phase.Playing:
                UpdatePlaying(mouse);
                break;

            case Phase.Result:
                if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
                    BeginCountdown();
                else if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
                    RequestExit();
                break;
        }
    }

    public void Configure(GameObject root)
    {
        canvasRoot = root;
    }

    public void Open(Action onExitRequested)
    {
        EnsureInitialized();
        if (!initialized || canvasRoot == null)
            return;

        exitRequested = onExitRequested;
        StoreAndReleaseCursor();
        canvasRoot.SetActive(true);
        ResetToReady();
    }

    /// <summary>供编辑器运行时预览菜单使用，不要求玩家先进入触发器。</summary>
    public void OpenPreview()
    {
        Open(Close);
    }

    public void Close()
    {
        if (phase == Phase.Closed && (canvasRoot == null || !canvasRoot.activeSelf))
            return;

        phase = Phase.Closed;
        exitRequested = null;
        ClearActiveMarks();
        RestoreCursor();

        if (canvasRoot != null)
            canvasRoot.SetActive(false);
    }

    void RequestExit()
    {
        Action callback = exitRequested;
        if (callback != null)
            callback.Invoke();
        else
            Close();
    }

    void EnsureInitialized()
    {
        if (initialized)
            return;

        ResolveCanvas();
        if (canvasRoot == null)
        {
            Debug.LogError("刷大漆小游戏找不到 LacquerMinigameCanvas。", this);
            return;
        }

        BuildUi();
        PrepareAudio();
        initialized = true;
    }

    void ResolveCanvas()
    {
        if (canvasRoot != null)
            return;

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas != null && canvas.name == "LacquerMinigameCanvas")
            {
                canvasRoot = canvas.gameObject;
                return;
            }
        }
    }

    void ResetToReady()
    {
        ClearActiveMarks();
        ResetScore();
        phase = Phase.Ready;
        introPanel.SetActive(true);
        resultPanel.SetActive(false);
        countdownText.gameObject.SetActive(false);
        feedbackText.text = "";
        feedbackRemaining = 0f;
        curtainGraphic.ResetVisual();
        UpdateScanLine(0f);
        UpdateHud();
    }

    void BeginCountdown()
    {
        ClearActiveMarks();
        ResetScore();
        introPanel.SetActive(false);
        resultPanel.SetActive(false);
        countdownText.gameObject.SetActive(true);
        countdownRemaining = 3.15f;
        countdownText.text = "3";
        phase = Phase.Countdown;
        curtainGraphic.ResetVisual();
        UpdateScanLine(0f);
        UpdateHud();
    }

    void UpdateCountdown()
    {
        countdownRemaining -= Time.unscaledDeltaTime;
        if (countdownRemaining <= 0f)
        {
            StartRound();
            return;
        }

        countdownText.text = countdownRemaining <= 0.45f
            ? "下刷"
            : Mathf.CeilToInt(countdownRemaining).ToString();
    }

    void StartRound()
    {
        countdownText.gameObject.SetActive(false);
        GenerateSchedule();
        nextSpawnIndex = 0;
        songTime = 0f;
        phase = Phase.Playing;
        UpdateScanLine(songTime);
        ShowFeedback("跟住游标，认准漆色", new Color32(255, 228, 180, 255));
    }

    void GenerateSchedule()
    {
        schedule.Clear();
        roundNumber++;
        var random = new System.Random(20261113 + roundNumber * 47);
        int passCount = Mathf.CeilToInt(totalMarks / (float)MarksPerPass);
        int globalIndex = 0;

        for (int pass = 0; pass < passCount; pass++)
        {
            int count = Mathf.Min(MarksPerPass, totalMarks - globalIndex);
            bool movingUp = pass % 2 == 0;

            for (int slot = 0; slot < count; slot++, globalIndex++)
            {
                float pathProgress = (slot + 1f) / (count + 1f);
                pathProgress += Mathf.Lerp(-0.027f, 0.027f, (float)random.NextDouble());
                pathProgress = Mathf.Clamp(pathProgress, 0.075f, 0.925f);
                float y = movingUp
                    ? Mathf.Lerp(BottomBound, TopBound, pathProgress)
                    : Mathf.Lerp(TopBound, BottomBound, pathProgress);

                MarkType type = (globalIndex + 1) % Mathf.Max(2, strokeEvery) == 0
                    ? MarkType.Stroke
                    : MarkType.Tap;
                PaintColor paint = random.NextDouble() < 0.5 ? PaintColor.Red : PaintColor.Green;
                Vector2 start;
                Vector2 end;

                if (type == MarkType.Tap)
                {
                    float x = Mathf.Lerp(LeftBound + 70f, RightBound - 70f, (float)random.NextDouble());
                    start = end = new Vector2(x, y);
                }
                else
                {
                    int directionIndex = random.Next(0, 4);
                    Vector2 direction = directionIndex == 0
                        ? Vector2.right
                        : directionIndex == 1
                            ? Vector2.left
                            : directionIndex == 2
                                ? Vector2.up
                                : Vector2.down;
                    float length = Mathf.Lerp(175f, 245f, (float)random.NextDouble());

                    // 扫描线在 targetTime 必须正好经过拖拽起点，而不是漆条中心。
                    // 若随机到的竖直方向空间不足，就反向延伸。
                    if (direction == Vector2.up && y + length > TopBound - 25f)
                        direction = Vector2.down;
                    else if (direction == Vector2.down && y - length < BottomBound + 25f)
                        direction = Vector2.up;

                    float xMin = LeftBound + 75f;
                    float xMax = RightBound - 75f;
                    if (direction == Vector2.right)
                        xMax -= length;
                    else if (direction == Vector2.left)
                        xMin += length;

                    float x = Mathf.Lerp(xMin, xMax, (float)random.NextDouble());
                    start = new Vector2(x, y);
                    end = start + direction * length;
                }

                schedule.Add(new MarkSpec
                {
                    type = type,
                    color = paint,
                    index = globalIndex,
                    targetTime = pass * scanPassDuration + pathProgress * scanPassDuration,
                    start = start,
                    end = end
                });
            }
        }
    }

    void UpdatePlaying(Mouse mouse)
    {
        songTime += Time.unscaledDeltaTime;
        UpdateScanLine(songTime);
        SpawnDueMarks();
        UpdateMarkVisuals();

        if (mouse != null && TryGetPointer(mouse, out Vector2 pointer))
        {
            bool leftPressed = mouse.leftButton.wasPressedThisFrame;
            bool rightPressed = mouse.rightButton.wasPressedThisFrame;
            if (activeDrag == null && (leftPressed || rightPressed))
                HandlePointerPress(pointer, leftPressed, rightPressed);

            if (activeDrag != null)
                UpdateActiveDrag(mouse, pointer);
        }

        ResolveMissedMarks();

        if (judgedCount >= totalMarks && activeMarks.Count == 0)
            FinishRound();
    }

    void SpawnDueMarks()
    {
        while (nextSpawnIndex < schedule.Count &&
               songTime >= schedule[nextSpawnIndex].targetTime - preShowTime)
        {
            SpawnMark(schedule[nextSpawnIndex]);
            nextSpawnIndex++;
        }
    }

    void SpawnMark(MarkSpec spec)
    {
        RectTransform rect;
        if (spec.type == MarkType.Tap)
        {
            rect = CreateRect(noteLayer, "LacquerDot_" + (spec.index + 1), new Vector2(0.5f, 0.5f),
                spec.Center, new Vector2(150f, 150f));
        }
        else
        {
            Vector2 segment = spec.end - spec.start;
            rect = CreateRect(noteLayer, "LacquerStroke_" + (spec.index + 1), new Vector2(0.5f, 0.5f),
                spec.Center, new Vector2(segment.magnitude + 105f, 125f));
            rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(segment.y, segment.x) * Mathf.Rad2Deg);
        }

        LacquerNoteGraphic graphic = rect.gameObject.AddComponent<LacquerNoteGraphic>();
        graphic.Configure(spec.type == MarkType.Tap
            ? LacquerNoteGraphic.NoteShape.Tap
            : LacquerNoteGraphic.NoteShape.Stroke, GetPaintColor(spec.color));
        graphic.color = Color.white;

        CanvasGroup group = rect.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0.12f;

        activeMarks.Add(new ActiveMark
        {
            spec = spec,
            rect = rect,
            graphic = graphic,
            canvasGroup = group
        });
    }

    void UpdateMarkVisuals()
    {
        for (int i = 0; i < activeMarks.Count; i++)
        {
            ActiveMark mark = activeMarks[i];
            float beforeTarget = mark.spec.targetTime - songTime;
            float approach = 1f - Mathf.Clamp01(beforeTarget / Mathf.Max(0.01f, preShowTime));
            mark.graphic.SetApproach(approach);
            mark.canvasGroup.alpha = Mathf.Lerp(0.18f, 1f, Mathf.Clamp01(approach * 1.7f));
        }
    }

    void HandlePointerPress(Vector2 pointer, bool leftPressed, bool rightPressed)
    {
        ActiveMark candidate = FindPointerCandidate(pointer);
        if (candidate == null)
        {
            combo = 0;
            ShowFeedback("空刷", new Color32(190, 167, 143, 255));
            UpdateHud();
            return;
        }

        bool correctColor = candidate.spec.color == PaintColor.Red ? leftPressed : rightPressed;
        if (!correctColor)
        {
            Judge(candidate, Judgement.Miss, "漆色错了");
            return;
        }

        float timingOffset = Mathf.Abs(songTime - candidate.spec.targetTime);
        Judgement timing = GetJudgement(timingOffset);
        if (candidate.spec.type == MarkType.Tap)
        {
            Judge(candidate, timing);
            return;
        }

        if (timing == Judgement.Miss)
        {
            Judge(candidate, Judgement.Miss, "起刷太早或太晚");
            return;
        }

        candidate.dragging = true;
        candidate.startJudgement = timing;
        candidate.dragProgress = 0f;
        activeDrag = candidate;
        candidate.graphic.SetDragProgress(0f, true);
        ShowFeedback("按住，沿箭头拖到底", new Color32(255, 232, 180, 255));
    }

    ActiveMark FindPointerCandidate(Vector2 pointer)
    {
        ActiveMark best = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < activeMarks.Count; i++)
        {
            ActiveMark mark = activeMarks[i];
            if (mark.dragging)
                continue;

            float timeDistance = Mathf.Abs(songTime - mark.spec.targetTime);
            if (timeDistance > missWindow)
                continue;

            float pointerDistance = Vector2.Distance(pointer,
                mark.spec.type == MarkType.Tap ? mark.spec.Center : mark.spec.start);
            float captureRadius = mark.spec.type == MarkType.Tap ? 76f : 82f;
            if (pointerDistance > captureRadius)
                continue;

            float candidateScore = pointerDistance + timeDistance * 95f;
            if (candidateScore < bestScore)
            {
                best = mark;
                bestScore = candidateScore;
            }
        }

        return best;
    }

    void UpdateActiveDrag(Mouse mouse, Vector2 pointer)
    {
        ActiveMark mark = activeDrag;
        if (mark == null)
            return;

        bool held = mark.spec.color == PaintColor.Red
            ? mouse.leftButton.isPressed
            : mouse.rightButton.isPressed;
        bool released = mark.spec.color == PaintColor.Red
            ? mouse.leftButton.wasReleasedThisFrame
            : mouse.rightButton.wasReleasedThisFrame;

        if (!held)
        {
            if (released || songTime > mark.spec.targetTime + 0.05f)
                Judge(mark, Judgement.Miss, "漆条中途断了");
            return;
        }

        Vector2 segment = mark.spec.end - mark.spec.start;
        float lengthSquared = Mathf.Max(1f, segment.sqrMagnitude);
        float rawProgress = Vector2.Dot(pointer - mark.spec.start, segment) / lengthSquared;
        float clampedProgress = Mathf.Clamp01(rawProgress);
        Vector2 closest = mark.spec.start + segment * clampedProgress;
        float corridorDistance = Vector2.Distance(pointer, closest);
        bool inCorridor = corridorDistance <= dragCorridor && rawProgress >= -0.18f && rawProgress <= 1.18f;
        bool movingForward = rawProgress + 0.075f >= mark.dragProgress;

        mark.dragSamples++;
        if (!inCorridor || !movingForward)
            mark.outsideSamples++;
        else
            mark.dragProgress = Mathf.Max(mark.dragProgress, clampedProgress);

        mark.graphic.SetDragProgress(mark.dragProgress, inCorridor && movingForward);

        if (mark.dragProgress >= 0.94f && Vector2.Distance(pointer, mark.spec.end) <= 88f)
        {
            float outsideRatio = mark.outsideSamples / Mathf.Max(1f, mark.dragSamples);
            Judgement finalJudgement = mark.startJudgement;
            if (outsideRatio > 0.30f)
                finalJudgement = Degrade(finalJudgement, 2);
            else if (outsideRatio > 0.12f)
                finalJudgement = Degrade(finalJudgement, 1);

            Judge(mark, finalJudgement);
            return;
        }

        if (songTime > mark.spec.targetTime + dragDeadline)
            Judge(mark, Judgement.Miss, "漆条超时");
    }

    void ResolveMissedMarks()
    {
        for (int i = activeMarks.Count - 1; i >= 0; i--)
        {
            ActiveMark mark = activeMarks[i];
            if (mark == activeDrag)
                continue;

            if (songTime - mark.spec.targetTime > missWindow)
                Judge(mark, Judgement.Miss);
        }
    }

    Judgement GetJudgement(float distance)
    {
        if (distance <= perfectWindow)
            return Judgement.Perfect;
        if (distance <= greatWindow)
            return Judgement.Great;
        if (distance <= goodWindow)
            return Judgement.Good;
        return Judgement.Miss;
    }

    static Judgement Degrade(Judgement judgement, int steps)
    {
        return (Judgement)Mathf.Clamp((int)judgement + steps, (int)Judgement.Perfect, (int)Judgement.Miss);
    }

    void Judge(ActiveMark mark, Judgement judgement, string specialMessage = null)
    {
        if (mark == null || !activeMarks.Remove(mark))
            return;

        if (activeDrag == mark)
            activeDrag = null;

        judgedCount++;
        int points;
        switch (judgement)
        {
            case Judgement.Perfect:
                points = 100;
                perfectCount++;
                combo++;
                ShowFeedback(specialMessage ?? "细润", new Color32(255, 239, 190, 255));
                break;
            case Judgement.Great:
                points = 85;
                greatCount++;
                combo++;
                ShowFeedback(specialMessage ?? "匀顺", new Color32(187, 225, 188, 255));
                break;
            case Judgement.Good:
                points = 65;
                goodCount++;
                combo++;
                ShowFeedback(specialMessage ?? "擦边", new Color32(223, 188, 127, 255));
                break;
            default:
                points = 0;
                missCount++;
                combo = 0;
                ShowFeedback(specialMessage ?? "漏漆", new Color32(225, 107, 84, 255));
                break;
        }

        score += points;
        maxCombo = Mathf.Max(maxCombo, combo);
        curtainGraphic.SetProgress(judgedCount / (float)totalMarks);

        if (judgement != Judgement.Miss)
        {
            Color paint = GetPaintColor(mark.spec.color);
            curtainGraphic.PlayBrush(paint);
            if (mark.spec.type == MarkType.Stroke)
                PlayClip(strokeClip, 0.62f);
            else
                PlayClip(mark.spec.color == PaintColor.Red ? redClip : greenClip, 0.54f);
        }
        else
        {
            PlayClip(missClip, 0.38f);
        }

        Destroy(mark.rect.gameObject);
        UpdateHud();
    }

    void FinishRound()
    {
        phase = Phase.Result;
        resultPanel.SetActive(true);
        float accuracy = score / Mathf.Max(1f, totalMarks * 100f);
        LacquerGrade grade = CalculateGrade(accuracy, missCount);

        resultText.text =
            "完成一张宣纸竹帘刷漆\n\n" +
            "刷漆评级  " + grade + "\n" +
            "漆面准确率  " + (accuracy * 100f).ToString("0.0") + "%\n" +
            "最高连击  " + maxCombo + "\n\n" +
            "细润 " + perfectCount + "   匀顺 " + greatCount + "   擦边 " + goodCount + "   漏漆 " + missCount + "\n" +
            "游戏时间 +" + gameMinutesCost + " 分钟\n\n" +
            "Enter 返回工作台     R 再刷一次";

        var result = new LacquerResult
        {
            grade = grade,
            accuracy = accuracy,
            score = score,
            maxCombo = maxCombo,
            perfectCount = perfectCount,
            greatCount = greatCount,
            goodCount = goodCount,
            missCount = missCount,
            gameMinutes = gameMinutesCost
        };

        Completed?.Invoke(result);
        Debug.Log("刷大漆完成：评级 " + grade + "，准确率 " + (accuracy * 100f).ToString("0.0") +
                  "%；游戏内用时 " + gameMinutesCost + " 分钟。", this);
    }

    static LacquerGrade CalculateGrade(float accuracy, int misses)
    {
        if (accuracy >= 0.90f && misses <= 2)
            return LacquerGrade.S;
        if (accuracy >= 0.80f)
            return LacquerGrade.A;
        if (accuracy >= 0.68f)
            return LacquerGrade.B;
        if (accuracy >= 0.52f)
            return LacquerGrade.C;
        return LacquerGrade.D;
    }

    void ResetScore()
    {
        schedule.Clear();
        nextSpawnIndex = 0;
        judgedCount = 0;
        score = 0;
        combo = 0;
        maxCombo = 0;
        perfectCount = 0;
        greatCount = 0;
        goodCount = 0;
        missCount = 0;
        songTime = 0f;
        activeDrag = null;
    }

    void ClearActiveMarks()
    {
        for (int i = 0; i < activeMarks.Count; i++)
        {
            if (activeMarks[i]?.rect != null)
                Destroy(activeMarks[i].rect.gameObject);
        }

        activeMarks.Clear();
        activeDrag = null;
    }

    void UpdateScanLine(float time)
    {
        if (scanLine == null || scanGlow == null)
            return;

        int pass = Mathf.FloorToInt(time / Mathf.Max(0.01f, scanPassDuration));
        float passProgress = Mathf.Repeat(time, Mathf.Max(0.01f, scanPassDuration)) /
                             Mathf.Max(0.01f, scanPassDuration);
        bool movingUp = pass % 2 == 0;
        float y = movingUp
            ? Mathf.Lerp(BottomBound, TopBound, passProgress)
            : Mathf.Lerp(TopBound, BottomBound, passProgress);
        scanLine.anchoredPosition = new Vector2(0f, y);
        scanGlow.anchoredPosition = new Vector2(0f, y);
        if (scanDirectionText != null)
            scanDirectionText.text = movingUp ? "游标方向  ↑" : "游标方向  ↓";
    }

    void UpdateHud()
    {
        if (progressText == null)
            return;

        float accuracy = score / Mathf.Max(1f, judgedCount * 100f);
        progressText.text = "漆面  " + judgedCount + " / " + totalMarks;
        scoreText.text = "分数 " + score.ToString("D6") + "    连击 " + combo +
                         "    准确率 " + Mathf.RoundToInt(accuracy * 100f) + "%";

        float normalized = judgedCount / (float)Mathf.Max(1, totalMarks);
        progressFill.anchorMax = new Vector2(normalized, 1f);
        progressFill.offsetMin = Vector2.zero;
        progressFill.offsetMax = Vector2.zero;
    }

    void ShowFeedback(string message, Color color)
    {
        feedbackText.text = message;
        feedbackText.color = color;
        feedbackRemaining = 0.62f;
    }

    void UpdateFeedbackFade()
    {
        if (feedbackRemaining <= 0f || feedbackText == null)
            return;

        feedbackRemaining = Mathf.Max(0f, feedbackRemaining - Time.unscaledDeltaTime);
        Color color = feedbackText.color;
        color.a = Mathf.Clamp01(feedbackRemaining / 0.24f);
        feedbackText.color = color;
        if (feedbackRemaining <= 0f)
            feedbackText.text = "";
    }

    bool TryGetPointer(Mouse mouse, out Vector2 localPoint)
    {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            playArea, mouse.position.ReadValue(), null, out localPoint);
    }

    static Color GetPaintColor(PaintColor paint)
    {
        return paint == PaintColor.Red ? RedLacquer : GreenLacquer;
    }

    void StoreAndReleaseCursor()
    {
        if (!cursorStateStored)
        {
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            cursorStateStored = true;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void RestoreCursor()
    {
        if (!cursorStateStored)
            return;

        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
        cursorStateStored = false;
    }

    void BuildUi()
    {
        Canvas canvas = canvasRoot.GetComponent<Canvas>();
        if (canvas == null)
            canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 210;

        CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (canvasRoot.GetComponent<GraphicRaycaster>() == null)
            canvasRoot.AddComponent<GraphicRaycaster>();

        uiFont = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei", "微软雅黑", "SimHei", "黑体", "Arial Unicode MS", "Arial" }, 32);
        if (uiFont == null)
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        RectTransform canvasRect = canvasRoot.GetComponent<RectTransform>();
        RectTransform backdrop = canvasRoot.transform.Find("LacquerBackdrop") as RectTransform;
        if (backdrop == null)
        {
            backdrop = CreateRect(canvasRect, "LacquerBackdrop", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(backdrop);
        }

        Image oldBackdrop = backdrop.GetComponent<Image>();
        if (oldBackdrop != null)
            oldBackdrop.enabled = false;

        RectTransform backdropArt = backdrop.Find("ProceduralLacquerTable") as RectTransform;
        if (backdropArt == null)
        {
            backdropArt = CreateRect(backdrop, "ProceduralLacquerTable", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(backdropArt);
        }

        curtainGraphic = backdropArt.GetComponent<LacquerCurtainGraphic>();
        if (curtainGraphic == null)
            curtainGraphic = backdropArt.gameObject.AddComponent<LacquerCurtainGraphic>();
        curtainGraphic.color = Color.white;
        curtainGraphic.raycastTarget = false;
        backdrop.SetAsFirstSibling();

        Transform oldRoot = canvasRoot.transform.Find("LacquerRuntimeUI");
        if (oldRoot != null)
            Destroy(oldRoot.gameObject);

        RectTransform root = CreateRect(canvasRect, "LacquerRuntimeUI", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Stretch(root);

        Image fieldImage = CreateImage(root, "LacquerPlayArea", new Vector2(0.5f, 0.5f), new Vector2(-145f, -10f),
            new Vector2(1240f, 650f), new Color(0.045f, 0.025f, 0.020f, 0.58f));
        AddOutline(fieldImage.gameObject, new Color(0.83f, 0.58f, 0.34f, 0.66f), new Vector2(2f, -2f));
        playArea = fieldImage.rectTransform;

        noteLayer = CreateRect(playArea, "LacquerMarks", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Stretch(noteLayer);

        Image glowImage = CreateImage(playArea, "ScanGlow", new Vector2(0.5f, 0.5f), new Vector2(0f, BottomBound),
            new Vector2(1160f, 38f), new Color(1f, 0.78f, 0.52f, 0.10f));
        scanGlow = glowImage.rectTransform;
        Image lineImage = CreateImage(playArea, "ScanLine", new Vector2(0.5f, 0.5f), new Vector2(0f, BottomBound),
            new Vector2(1160f, 6f), new Color32(255, 234, 198, 255));
        scanLine = lineImage.rectTransform;
        AddOutline(lineImage.gameObject, new Color(0.52f, 0.15f, 0.10f, 0.85f), new Vector2(0f, -2f));

        feedbackText = CreateText(root, "Feedback", "", 45, TextAnchor.MiddleCenter,
            new Vector2(-145f, -215f), new Vector2(720f, 92f), Color.white);
        AddOutline(feedbackText.gameObject, new Color(0f, 0f, 0f, 0.94f), new Vector2(2f, -2f));

        Image guide = CreateImage(root, "GuidePanel", new Vector2(0.5f, 0.5f), new Vector2(690f, -10f),
            new Vector2(330f, 650f), new Color(0.055f, 0.034f, 0.027f, 0.93f));
        AddOutline(guide.gameObject, new Color(0.73f, 0.48f, 0.28f, 0.68f), new Vector2(2f, -2f));
        RectTransform guideRect = guide.rectTransform;
        CreateText(guideRect, "GuideTitle", "漆色与手法", 29, TextAnchor.MiddleCenter,
            new Vector2(0f, 270f), new Vector2(285f, 55f), new Color32(244, 220, 176, 255));
        CreateLegendSwatch(guideRect, "Red", new Vector2(0f, 188f), RedLacquer, "朱漆  ·  鼠标左键");
        CreateLegendSwatch(guideRect, "Green", new Vector2(0f, 104f), GreenLacquer, "青漆  ·  鼠标右键");
        CreateText(guideRect, "TapGuide", "●  漆点\n游标经过时点按", 23, TextAnchor.MiddleCenter,
            new Vector2(0f, -15f), new Vector2(285f, 90f), new Color32(226, 212, 188, 255));
        CreateText(guideRect, "StrokeGuide", "━━▷  漆条\n从亮圈按住，沿箭头拖到底", 22, TextAnchor.MiddleCenter,
            new Vector2(0f, -130f), new Vector2(290f, 110f), new Color32(226, 212, 188, 255));
        scanDirectionText = CreateText(guideRect, "Direction", "游标方向  ↑", 27, TextAnchor.MiddleCenter,
            new Vector2(0f, -255f), new Vector2(285f, 60f), new Color32(255, 225, 167, 255));

        Image header = CreateImage(root, "Header", new Vector2(0.5f, 1f), new Vector2(0f, -58f),
            new Vector2(1760f, 88f), new Color(0.045f, 0.026f, 0.021f, 0.94f));
        AddOutline(header.gameObject, new Color(0.72f, 0.42f, 0.26f, 0.55f), new Vector2(0f, -2f));
        CreateText(header.rectTransform, "Title", "抄纸竹帘 · 刷大漆", 35, TextAnchor.MiddleLeft,
            new Vector2(-560f, 0f), new Vector2(520f, 68f), new Color32(246, 219, 171, 255));
        progressText = CreateText(header.rectTransform, "Progress", "漆面  0 / 32", 29, TextAnchor.MiddleCenter,
            new Vector2(0f, 0f), new Vector2(420f, 68f), Color.white);
        scoreText = CreateText(header.rectTransform, "Score", "分数 000000    连击 0    准确率 100%", 24,
            TextAnchor.MiddleRight, new Vector2(520f, 0f), new Vector2(650f, 68f), new Color32(222, 212, 194, 255));

        Image progressBack = CreateImage(root, "ProgressBar", new Vector2(0.5f, 1f), new Vector2(0f, -118f),
            new Vector2(1220f, 18f), new Color(0.075f, 0.040f, 0.030f, 0.96f));
        progressFill = CreateRect(progressBack.rectTransform, "Fill", new Vector2(0f, 0f), Vector2.zero, Vector2.zero);
        progressFill.anchorMin = Vector2.zero;
        progressFill.anchorMax = new Vector2(0f, 1f);
        progressFill.pivot = new Vector2(0f, 0.5f);
        progressFill.offsetMin = Vector2.zero;
        progressFill.offsetMax = Vector2.zero;
        Image progressImage = progressFill.gameObject.AddComponent<Image>();
        progressImage.color = new Color32(165, 53, 40, 255);
        progressImage.raycastTarget = false;

        CreateText(root, "Footer", "朱漆左键 · 青漆右键     漆点点按     漆条按住顺箭头拖动     Esc 退出", 24,
            TextAnchor.MiddleCenter, new Vector2(0f, -507f), new Vector2(1580f, 45f),
            new Color32(216, 200, 176, 238));

        countdownText = CreateText(root, "Countdown", "3", 112, TextAnchor.MiddleCenter,
            new Vector2(-145f, 25f), new Vector2(600f, 230f), new Color32(255, 232, 181, 255));
        AddOutline(countdownText.gameObject, new Color(0f, 0f, 0f, 0.94f), new Vector2(4f, -4f));
        countdownText.gameObject.SetActive(false);

        introPanel = CreateOverlayPanel(root, "IntroPanel", new Vector2(0f, -5f), new Vector2(820f, 620f));
        RectTransform introRect = introPanel.GetComponent<RectTransform>();
        Text introTitle = CreateText(introRect, "Title", "给宣纸竹帘刷大漆", 44, TextAnchor.MiddleCenter,
            new Vector2(0f, 220f), new Vector2(720f, 75f), new Color32(248, 219, 169, 255));
        AddOutline(introTitle.gameObject, new Color(0f, 0f, 0f, 0.9f), new Vector2(2f, -2f));
        CreateText(introRect, "Body",
            "水平游标会像 Cytus 一样上下往返\n游标经过漆点或漆条起点时操作\n\n朱红：鼠标左键    青绿：鼠标右键\n圆漆点：对准点按\n长漆条：从亮圈按住，沿箭头方向拖到末端\n\n偏离漆条或反向拖动会降低漆面评级",
            26, TextAnchor.MiddleCenter, new Vector2(0f, 15f), new Vector2(720f, 355f),
            new Color32(230, 218, 195, 255));
        Text startText = CreateText(introRect, "Start", "按 SPACE 或单击开始", 32, TextAnchor.MiddleCenter,
            new Vector2(0f, -245f), new Vector2(590f, 65f), new Color32(255, 232, 176, 255));
        AddOutline(startText.gameObject, new Color(0f, 0f, 0f, 0.88f), new Vector2(2f, -2f));

        resultPanel = CreateOverlayPanel(root, "ResultPanel", new Vector2(0f, -5f), new Vector2(800f, 650f));
        resultText = CreateText(resultPanel.GetComponent<RectTransform>(), "ResultText", "", 29,
            TextAnchor.MiddleCenter, Vector2.zero, new Vector2(720f, 580f), new Color32(239, 222, 192, 255));
        resultPanel.SetActive(false);
    }

    void CreateLegendSwatch(RectTransform parent, string name, Vector2 position, Color color, string label)
    {
        Image swatch = CreateImage(parent, name, new Vector2(0.5f, 0.5f), position + new Vector2(-105f, 0f),
            new Vector2(44f, 44f), color);
        AddOutline(swatch.gameObject, new Color(0.06f, 0.03f, 0.02f, 0.95f), new Vector2(2f, -2f));
        CreateText(parent, name + "Label", label, 22, TextAnchor.MiddleLeft,
            position + new Vector2(35f, 0f), new Vector2(225f, 50f), new Color32(232, 219, 197, 255));
    }

    GameObject CreateOverlayPanel(RectTransform parent, string name, Vector2 position, Vector2 size)
    {
        Image image = CreateImage(parent, name, new Vector2(0.5f, 0.5f), position, size,
            new Color(0.065f, 0.037f, 0.028f, 0.975f));
        AddOutline(image.gameObject, new Color(0.80f, 0.52f, 0.30f, 0.84f), new Vector2(3f, -3f));
        return image.gameObject;
    }

    RectTransform CreateRect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    Image CreateImage(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
    {
        RectTransform rect = CreateRect(parent, name, anchor, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    Text CreateText(Transform parent, string name, string value, int fontSize, TextAnchor alignment,
        Vector2 position, Vector2 size, Color color)
    {
        RectTransform rect = CreateRect(parent, name, new Vector2(0.5f, 0.5f), position, size);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = uiFont;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Normal;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = color;
        text.text = value;
        text.raycastTarget = false;
        return text;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    static void AddOutline(GameObject target, Color color, Vector2 distance)
    {
        Outline outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
    }

    void PrepareAudio()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 0.65f;

        redClip = CreateTone("Lacquer_Red", 430f, 0.085f, 0.04f);
        greenClip = CreateTone("Lacquer_Green", 585f, 0.085f, 0.04f);
        strokeClip = CreateTone("Lacquer_Stroke", 720f, 0.15f, 0.08f);
        missClip = CreateTone("Lacquer_Miss", 115f, 0.12f, 0.14f);
    }

    static AudioClip CreateTone(string name, float frequency, float duration, float noiseAmount)
    {
        const int sampleRate = 22050;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        var random = new System.Random(name.GetHashCode());

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;
            float envelope = 1f - i / (float)sampleCount;
            float tone = Mathf.Sin(Mathf.PI * 2f * frequency * time) * 0.62f;
            float noise = ((float)random.NextDouble() * 2f - 1f) * noiseAmount;
            samples[i] = (tone + noise) * envelope;
        }

        AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    void PlayClip(AudioClip clip, float volume)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip, volume);
    }

    void OnValidate()
    {
        totalMarks = Mathf.Max(8, totalMarks);
        strokeEvery = Mathf.Max(2, strokeEvery);
        scanPassDuration = Mathf.Max(2f, scanPassDuration);
        preShowTime = Mathf.Max(0.3f, preShowTime);
        perfectWindow = Mathf.Max(0.03f, perfectWindow);
        greatWindow = Mathf.Max(perfectWindow, greatWindow);
        goodWindow = Mathf.Max(greatWindow, goodWindow);
        missWindow = Mathf.Max(goodWindow, missWindow);
        dragDeadline = Mathf.Max(0.5f, dragDeadline);
        dragCorridor = Mathf.Max(20f, dragCorridor);
    }
}
