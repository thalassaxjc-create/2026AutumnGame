using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 抄纸竹帘编帘音游：100 根竹丝、F/J 双轨、定期双键打结、结算评级。
/// UI 与抽象工作台画面全部在运行时用 UGUI 生成，不依赖外部图片。
/// </summary>
[DisallowMultipleComponent]
public sealed class WeavingMinigameController : MonoBehaviour
{
    public enum WeavingGrade
    {
        S,
        A,
        B,
        C,
        D
    }

    [Serializable]
    public struct WeavingResult
    {
        public WeavingGrade grade;
        public float accuracy;
        public int score;
        public int maxCombo;
        public int perfectCount;
        public int greatCount;
        public int goodCount;
        public int missCount;
        public int consumedThreads;
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

    enum NoteKind
    {
        Left,
        Right,
        Knot
    }

    enum Judgement
    {
        Perfect,
        Great,
        Good,
        Miss
    }

    sealed class NoteSpec
    {
        public NoteKind kind;
        public float targetTime;
        public int index;
    }

    sealed class ActiveNote
    {
        public NoteSpec spec;
        public RectTransform rect;
        public Image image;
        public Text label;
        public CanvasGroup canvasGroup;
    }

    [Header("场景引用")]
    [SerializeField] GameObject canvasRoot;

    [Header("订单")]
    [SerializeField, Min(1)] int requiredThreads = 100;
    [SerializeField, Min(2)] int knotEvery = 5;
    [SerializeField, Min(1)] int gameMinutesCost = 60;

    [Header("节奏")]
    [SerializeField, Min(0.2f)] float spawnInterval = 0.46f;
    [SerializeField, Min(0.5f)] float noteTravelTime = 2.2f;
    [SerializeField, Min(0.01f)] float perfectWindow = 0.075f;
    [SerializeField, Min(0.02f)] float greatWindow = 0.145f;
    [SerializeField, Min(0.03f)] float goodWindow = 0.24f;
    [SerializeField, Min(0.04f)] float missWindow = 0.31f;
    [SerializeField, Min(0.03f)] float chordGrace = 0.14f;

    const float SpawnY = 305f;
    const float HitY = -250f;
    const float LeftLaneX = -118f;
    const float RightLaneX = 118f;

    readonly List<NoteSpec> schedule = new List<NoteSpec>(128);
    readonly List<ActiveNote> activeNotes = new List<ActiveNote>(12);

    Phase phase = Phase.Closed;
    Action exitRequested;
    Font uiFont;
    AudioSource audioSource;
    AudioClip leftClip;
    AudioClip rightClip;
    AudioClip knotClip;
    AudioClip missClip;

    RectTransform noteLayer;
    RectTransform progressFill;
    Text progressText;
    Text scoreText;
    Text feedbackText;
    Text countdownText;
    Text resultText;
    GameObject introPanel;
    GameObject resultPanel;
    WeavingLoomGraphic loomGraphic;
    WeavingArcGraphic arcGraphic;

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

    ActiveNote pendingChord;
    bool pendingChordFirstWasLeft;
    float pendingChordStartedAt;

    public bool IsOpen => phase != Phase.Closed;
    public int RequiredThreads => requiredThreads;
    public event Action<WeavingResult> Completed;

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
        if (keyboard == null)
            return;

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            RequestExit();
            return;
        }

        switch (phase)
        {
            case Phase.Ready:
                if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)
                    BeginCountdown();
                break;

            case Phase.Countdown:
                UpdateCountdown();
                break;

            case Phase.Playing:
                UpdatePlaying(keyboard);
                break;

            case Phase.Result:
                if (keyboard.rKey.wasPressedThisFrame)
                    BeginCountdown();
                else if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
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
        canvasRoot.SetActive(true);
        ResetToReady();
    }

    /// <summary>供编辑器运行时预览菜单使用，不需要玩家进入触发器。</summary>
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
        ClearActiveNotes();
        pendingChord = null;

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
            Debug.LogError("编帘小游戏找不到 WeavingMinigameCanvas。", this);
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
            if (canvas != null && canvas.name == "WeavingMinigameCanvas")
            {
                canvasRoot = canvas.gameObject;
                return;
            }
        }
    }

    void ResetToReady()
    {
        ClearActiveNotes();
        ResetScore();
        phase = Phase.Ready;
        introPanel.SetActive(true);
        resultPanel.SetActive(false);
        countdownText.gameObject.SetActive(false);
        feedbackText.text = "";
        feedbackRemaining = 0f;
        loomGraphic.ResetVisual();
        UpdateHud();
    }

    void BeginCountdown()
    {
        ClearActiveNotes();
        ResetScore();
        introPanel.SetActive(false);
        resultPanel.SetActive(false);
        countdownText.gameObject.SetActive(true);
        countdownRemaining = 3.15f;
        countdownText.text = "3";
        phase = Phase.Countdown;
        loomGraphic.ResetVisual();
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

        if (countdownRemaining <= 0.45f)
            countdownText.text = "开始";
        else
            countdownText.text = Mathf.CeilToInt(countdownRemaining).ToString();
    }

    void StartRound()
    {
        countdownText.gameObject.SetActive(false);
        GenerateSchedule();
        nextSpawnIndex = 0;
        songTime = 0f;
        phase = Phase.Playing;
        ShowFeedback("听梭落线", new Color32(255, 237, 190, 255));
    }

    void GenerateSchedule()
    {
        schedule.Clear();
        roundNumber++;
        var random = new System.Random(20261113 + roundNumber * 31);
        NoteKind previous = NoteKind.Left;
        int repeated = 0;

        for (int i = 0; i < requiredThreads; i++)
        {
            NoteKind kind;
            if ((i + 1) % Mathf.Max(2, knotEvery) == 0)
            {
                kind = NoteKind.Knot;
                repeated = 0;
            }
            else
            {
                kind = random.NextDouble() < 0.5 ? NoteKind.Left : NoteKind.Right;
                if (kind == previous)
                    repeated++;
                else
                    repeated = 0;

                if (repeated >= 2)
                {
                    kind = previous == NoteKind.Left ? NoteKind.Right : NoteKind.Left;
                    repeated = 0;
                }

                previous = kind;
            }

            schedule.Add(new NoteSpec
            {
                kind = kind,
                index = i,
                targetTime = noteTravelTime + i * spawnInterval
            });
        }
    }

    void UpdatePlaying(Keyboard keyboard)
    {
        songTime += Time.unscaledDeltaTime;
        SpawnDueNotes();
        UpdateNotePositions();

        bool leftDown = keyboard.fKey.wasPressedThisFrame;
        bool rightDown = keyboard.jKey.wasPressedThisFrame;
        if (leftDown || rightDown)
            HandlePress(leftDown, rightDown);

        ResolvePendingChordTimeout();
        ResolveMissedNotes();

        if (judgedCount >= requiredThreads && activeNotes.Count == 0)
            FinishRound();
    }

    void SpawnDueNotes()
    {
        while (nextSpawnIndex < schedule.Count &&
               songTime >= schedule[nextSpawnIndex].targetTime - noteTravelTime)
        {
            SpawnNote(schedule[nextSpawnIndex]);
            nextSpawnIndex++;
        }
    }

    void SpawnNote(NoteSpec spec)
    {
        RectTransform rect = CreateRect(noteLayer, "Thread_" + (spec.index + 1),
            new Vector2(0.5f, 0.5f), Vector2.zero,
            spec.kind == NoteKind.Knot ? new Vector2(420f, 56f) : new Vector2(176f, 48f));

        float x = spec.kind == NoteKind.Left ? LeftLaneX : spec.kind == NoteKind.Right ? RightLaneX : 0f;
        rect.anchoredPosition = new Vector2(x, SpawnY);

        Image image = rect.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        image.color = spec.kind == NoteKind.Knot
            ? new Color32(242, 230, 198, 245)
            : spec.kind == NoteKind.Left
                ? new Color32(222, 180, 105, 242)
                : new Color32(177, 193, 142, 242);

        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.12f, 0.07f, 0.035f, 0.90f);
        outline.effectDistance = new Vector2(2f, -2f);

        RectTransform fiber = CreateRect(rect, "BambooFiber", new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(spec.kind == NoteKind.Knot ? 380f : 146f, 5f));
        Image fiberImage = fiber.gameObject.AddComponent<Image>();
        fiberImage.color = new Color32(255, 242, 194, 235);
        fiberImage.raycastTarget = false;

        Text label = CreateText(rect, "Key", spec.kind == NoteKind.Left ? "F" : spec.kind == NoteKind.Right ? "J" : "F  +  J    结",
            spec.kind == NoteKind.Knot ? 25 : 28, TextAnchor.MiddleCenter, Vector2.zero,
            rect.sizeDelta, new Color32(48, 33, 20, 255));

        CanvasGroup group = rect.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0.55f;

        activeNotes.Add(new ActiveNote
        {
            spec = spec,
            rect = rect,
            image = image,
            label = label,
            canvasGroup = group
        });
    }

    void UpdateNotePositions()
    {
        for (int i = 0; i < activeNotes.Count; i++)
        {
            ActiveNote note = activeNotes[i];
            float spawnTime = note.spec.targetTime - noteTravelTime;
            float travel = Mathf.InverseLerp(spawnTime, note.spec.targetTime, songTime);
            float y = Mathf.Lerp(SpawnY, HitY, travel);
            if (songTime > note.spec.targetTime)
                y -= (songTime - note.spec.targetTime) * 190f;

            note.rect.anchoredPosition = new Vector2(
                note.spec.kind == NoteKind.Left ? LeftLaneX : note.spec.kind == NoteKind.Right ? RightLaneX : 0f,
                y);
            note.canvasGroup.alpha = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(travel * 2.2f));
        }
    }

    void HandlePress(bool leftDown, bool rightDown)
    {
        if (pendingChord != null)
        {
            bool secondSide = pendingChordFirstWasLeft ? rightDown : leftDown;
            if (secondSide)
            {
                ActiveNote chord = pendingChord;
                pendingChord = null;
                JudgeByTiming(chord);
            }
            else
            {
                ShowFeedback("另一边！", new Color32(255, 222, 150, 255));
            }

            return;
        }

        ActiveNote candidate = FindClosestCandidate();
        if (candidate == null)
        {
            combo = 0;
            ShowFeedback("空拍", new Color32(198, 173, 148, 255));
            UpdateHud();
            return;
        }

        if (candidate.spec.kind == NoteKind.Knot)
        {
            bool bothPressed = (leftDown && rightDown) ||
                               (leftDown && Keyboard.current.jKey.isPressed) ||
                               (rightDown && Keyboard.current.fKey.isPressed);
            if (bothPressed)
            {
                JudgeByTiming(candidate);
            }
            else
            {
                pendingChord = candidate;
                pendingChordFirstWasLeft = leftDown;
                pendingChordStartedAt = songTime;
                candidate.image.color = new Color32(255, 247, 218, 255);
                candidate.rect.localScale = Vector3.one * 1.05f;
                ShowFeedback("再按另一边打结", new Color32(255, 239, 185, 255));
            }

            return;
        }

        bool correct = candidate.spec.kind == NoteKind.Left ? leftDown : rightDown;
        if (correct)
            JudgeByTiming(candidate);
        else
            Judge(candidate, Judgement.Miss);
    }

    ActiveNote FindClosestCandidate()
    {
        ActiveNote best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < activeNotes.Count; i++)
        {
            ActiveNote note = activeNotes[i];
            float distance = Mathf.Abs(songTime - note.spec.targetTime);
            if (distance <= missWindow && distance < bestDistance)
            {
                best = note;
                bestDistance = distance;
            }
        }

        return best;
    }

    void ResolvePendingChordTimeout()
    {
        if (pendingChord == null || songTime - pendingChordStartedAt <= chordGrace)
            return;

        ActiveNote failed = pendingChord;
        pendingChord = null;
        Judge(failed, Judgement.Miss);
    }

    void ResolveMissedNotes()
    {
        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            ActiveNote note = activeNotes[i];
            if (songTime - note.spec.targetTime <= missWindow)
                continue;

            if (pendingChord == note)
                pendingChord = null;
            Judge(note, Judgement.Miss);
        }
    }

    void JudgeByTiming(ActiveNote note)
    {
        float distance = Mathf.Abs(songTime - note.spec.targetTime);
        Judgement judgement = distance <= perfectWindow
            ? Judgement.Perfect
            : distance <= greatWindow
                ? Judgement.Great
                : distance <= goodWindow
                    ? Judgement.Good
                    : Judgement.Miss;
        Judge(note, judgement);
    }

    void Judge(ActiveNote note, Judgement judgement)
    {
        if (note == null || !activeNotes.Remove(note))
            return;

        if (pendingChord == note)
            pendingChord = null;

        judgedCount++;
        int points;
        switch (judgement)
        {
            case Judgement.Perfect:
                points = 100;
                perfectCount++;
                combo++;
                ShowFeedback("精准", new Color32(255, 244, 194, 255));
                break;
            case Judgement.Great:
                points = 85;
                greatCount++;
                combo++;
                ShowFeedback("稳", new Color32(196, 224, 174, 255));
                break;
            case Judgement.Good:
                points = 65;
                goodCount++;
                combo++;
                ShowFeedback("擦边", new Color32(219, 189, 134, 255));
                break;
            default:
                points = 0;
                missCount++;
                combo = 0;
                ShowFeedback("漏织", new Color32(222, 116, 92, 255));
                break;
        }

        score += points;
        maxCombo = Mathf.Max(maxCombo, combo);
        float progress = judgedCount / (float)requiredThreads;
        loomGraphic.SetProgress(progress);

        if (judgement != Judgement.Miss)
        {
            if (note.spec.kind == NoteKind.Knot)
            {
                arcGraphic.PlayKnot();
                loomGraphic.PlayKnot();
                PlayClip(knotClip, 0.72f);
            }
            else if (note.spec.kind == NoteKind.Left)
            {
                arcGraphic.PlaySweep(1);
                loomGraphic.PlayShuttle(1);
                PlayClip(leftClip, 0.52f);
            }
            else
            {
                arcGraphic.PlaySweep(-1);
                loomGraphic.PlayShuttle(-1);
                PlayClip(rightClip, 0.52f);
            }
        }
        else
        {
            PlayClip(missClip, 0.38f);
        }

        Destroy(note.rect.gameObject);
        UpdateHud();
    }

    void FinishRound()
    {
        phase = Phase.Result;
        resultPanel.SetActive(true);
        float accuracy = score / Mathf.Max(1f, requiredThreads * 100f);
        WeavingGrade grade = CalculateGrade(accuracy, missCount);

        resultText.text =
            "完成一张抄纸竹帘\n\n" +
            "评级  " + grade + "\n" +
            "准确率  " + (accuracy * 100f).ToString("0.0") + "%\n" +
            "最高连击  " + maxCombo + "\n\n" +
            "精准 " + perfectCount + "   稳 " + greatCount + "   擦边 " + goodCount + "   漏织 " + missCount + "\n" +
            "消耗竹丝 " + requiredThreads + " 根   游戏时间 +" + gameMinutesCost + " 分钟\n\n" +
            "Enter 返回工作台     R 再织一次";

        var result = new WeavingResult
        {
            grade = grade,
            accuracy = accuracy,
            score = score,
            maxCombo = maxCombo,
            perfectCount = perfectCount,
            greatCount = greatCount,
            goodCount = goodCount,
            missCount = missCount,
            consumedThreads = requiredThreads,
            gameMinutes = gameMinutesCost
        };

        Completed?.Invoke(result);
        Debug.Log("编帘完成：评级 " + grade + "，准确率 " + (accuracy * 100f).ToString("0.0") +
                  "%；消耗 " + requiredThreads + " 根竹丝；游戏内用时 " + gameMinutesCost + " 分钟。", this);
    }

    static WeavingGrade CalculateGrade(float accuracy, int misses)
    {
        if (accuracy >= 0.90f && misses <= 5)
            return WeavingGrade.S;
        if (accuracy >= 0.80f)
            return WeavingGrade.A;
        if (accuracy >= 0.68f)
            return WeavingGrade.B;
        if (accuracy >= 0.52f)
            return WeavingGrade.C;
        return WeavingGrade.D;
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
        pendingChord = null;
    }

    void ClearActiveNotes()
    {
        for (int i = 0; i < activeNotes.Count; i++)
        {
            if (activeNotes[i]?.rect != null)
                Destroy(activeNotes[i].rect.gameObject);
        }

        activeNotes.Clear();
        pendingChord = null;
    }

    void UpdateHud()
    {
        if (progressText == null)
            return;

        progressText.text = "竹丝  " + judgedCount + " / " + requiredThreads;
        float accuracy = judgedCount > 0 ? score / (judgedCount * 100f) : 1f;
        scoreText.text = "分数 " + score.ToString("000000") + "    连击 " + combo +
                         "    准确率 " + (accuracy * 100f).ToString("0") + "%";

        float progress = judgedCount / (float)Mathf.Max(1, requiredThreads);
        progressFill.anchorMax = new Vector2(progress, 1f);
        progressFill.offsetMin = Vector2.zero;
        progressFill.offsetMax = Vector2.zero;
    }

    void ShowFeedback(string message, Color color)
    {
        feedbackText.text = message;
        feedbackText.color = color;
        feedbackRemaining = 0.55f;
    }

    void UpdateFeedbackFade()
    {
        if (feedbackRemaining <= 0f || feedbackText == null)
            return;

        feedbackRemaining = Mathf.Max(0f, feedbackRemaining - Time.unscaledDeltaTime);
        Color color = feedbackText.color;
        color.a = Mathf.Clamp01(feedbackRemaining / 0.22f);
        feedbackText.color = color;
        if (feedbackRemaining <= 0f)
            feedbackText.text = "";
    }

    void BuildUi()
    {
        Canvas canvas = canvasRoot.GetComponent<Canvas>();
        if (canvas == null)
            canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

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
        RectTransform backdrop = canvasRoot.transform.Find("LoomBackdrop") as RectTransform;
        if (backdrop == null)
        {
            backdrop = CreateRect(canvasRect, "LoomBackdrop", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(backdrop);
        }

        Image oldBackdrop = backdrop.GetComponent<Image>();
        if (oldBackdrop != null)
            oldBackdrop.enabled = false;

        RectTransform loomLayer = backdrop.Find("ProceduralLoom") as RectTransform;
        if (loomLayer == null)
        {
            loomLayer = CreateRect(backdrop, "ProceduralLoom", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(loomLayer);
        }

        loomGraphic = loomLayer.GetComponent<WeavingLoomGraphic>();
        if (loomGraphic == null)
            loomGraphic = loomLayer.gameObject.AddComponent<WeavingLoomGraphic>();
        loomGraphic.color = Color.white;
        loomGraphic.raycastTarget = false;
        backdrop.SetAsFirstSibling();

        Transform oldRoot = canvasRoot.transform.Find("WeavingRuntimeUI");
        if (oldRoot != null)
            Destroy(oldRoot.gameObject);

        RectTransform root = CreateRect(canvasRect, "WeavingRuntimeUI", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Stretch(root);

        RectTransform arcRect = CreateRect(root, "ThreadArc", new Vector2(0.5f, 0.5f), new Vector2(-350f, -50f), new Vector2(900f, 300f));
        arcGraphic = arcRect.gameObject.AddComponent<WeavingArcGraphic>();
        arcGraphic.color = Color.white;

        Image playfield = CreateImage(root, "RhythmArea", new Vector2(0.5f, 0.5f), new Vector2(410f, -15f),
            new Vector2(500f, 790f), new Color(0.075f, 0.052f, 0.038f, 0.91f));
        AddOutline(playfield.gameObject, new Color(0.72f, 0.52f, 0.28f, 0.66f), new Vector2(2f, -2f));

        RectTransform playfieldRect = playfield.rectTransform;
        CreateImage(playfieldRect, "LeftLane", new Vector2(0.5f, 0.5f), new Vector2(LeftLaneX, 18f),
            new Vector2(205f, 660f), new Color(0.87f, 0.69f, 0.39f, 0.09f));
        CreateImage(playfieldRect, "RightLane", new Vector2(0.5f, 0.5f), new Vector2(RightLaneX, 18f),
            new Vector2(205f, 660f), new Color(0.57f, 0.68f, 0.43f, 0.09f));
        CreateImage(playfieldRect, "Divider", new Vector2(0.5f, 0.5f), new Vector2(0f, 18f),
            new Vector2(2f, 660f), new Color(1f, 0.93f, 0.78f, 0.17f));

        noteLayer = CreateRect(playfieldRect, "NoteContainer", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Stretch(noteLayer);

        CreateImage(playfieldRect, "TargetGlow", new Vector2(0.5f, 0.5f), new Vector2(0f, HitY),
            new Vector2(440f, 62f), new Color(1f, 0.90f, 0.64f, 0.075f));
        CreateImage(playfieldRect, "HitLine", new Vector2(0.5f, 0.5f), new Vector2(0f, HitY),
            new Vector2(440f, 7f), new Color32(255, 241, 216, 255));

        CreateKeyMarker(playfieldRect, "FMarker", "F", new Vector2(LeftLaneX, -326f), new Color32(203, 151, 76, 255));
        CreateKeyMarker(playfieldRect, "JMarker", "J", new Vector2(RightLaneX, -326f), new Color32(113, 144, 88, 255));
        CreateText(playfieldRect, "KnotHint", "每 5 根：F + J 同时打结", 20, TextAnchor.MiddleCenter,
            new Vector2(0f, 347f), new Vector2(440f, 38f), new Color32(218, 199, 166, 230));

        feedbackText = CreateText(root, "Feedback", "", 43, TextAnchor.MiddleCenter,
            new Vector2(410f, -195f), new Vector2(500f, 88f), Color.white);
        AddOutline(feedbackText.gameObject, new Color(0f, 0f, 0f, 0.92f), new Vector2(2f, -2f));

        Image header = CreateImage(root, "Header", new Vector2(0.5f, 1f), new Vector2(0f, -58f),
            new Vector2(1760f, 88f), new Color(0.045f, 0.030f, 0.022f, 0.91f));
        AddOutline(header.gameObject, new Color(0.74f, 0.53f, 0.29f, 0.45f), new Vector2(0f, -2f));
        CreateText(header.rectTransform, "Title", "抄纸竹帘 · 编帘", 35, TextAnchor.MiddleLeft,
            new Vector2(-560f, 0f), new Vector2(520f, 68f), new Color32(246, 224, 174, 255));
        progressText = CreateText(header.rectTransform, "Progress", "竹丝  0 / 100", 29, TextAnchor.MiddleCenter,
            new Vector2(0f, 0f), new Vector2(420f, 68f), Color.white);
        scoreText = CreateText(header.rectTransform, "Score", "分数 000000    连击 0    准确率 100%", 24, TextAnchor.MiddleRight,
            new Vector2(520f, 0f), new Vector2(650f, 68f), new Color32(221, 215, 198, 255));

        Image progressBack = CreateImage(root, "ProgressBar", new Vector2(0.5f, 1f), new Vector2(0f, -118f),
            new Vector2(1220f, 18f), new Color(0.08f, 0.055f, 0.038f, 0.95f));
        progressFill = CreateRect(progressBack.rectTransform, "Fill", new Vector2(0f, 0f), Vector2.zero, Vector2.zero);
        progressFill.anchorMin = Vector2.zero;
        progressFill.anchorMax = new Vector2(0f, 1f);
        progressFill.pivot = new Vector2(0f, 0.5f);
        progressFill.offsetMin = Vector2.zero;
        progressFill.offsetMax = Vector2.zero;
        Image progressImage = progressFill.gameObject.AddComponent<Image>();
        progressImage.color = new Color32(216, 171, 89, 255);
        progressImage.raycastTarget = false;

        CreateText(root, "Footer", "F 左梭摆线     J 右梭摆线     F + J 打结     Esc 退出", 24, TextAnchor.MiddleCenter,
            new Vector2(0f, -507f), new Vector2(1500f, 45f), new Color32(211, 198, 174, 238));
        CreateText(root, "CraftLabel", "一丝一线，帘面随进度逐根生长", 22, TextAnchor.MiddleLeft,
            new Vector2(-660f, -413f), new Vector2(520f, 50f), new Color32(197, 163, 104, 220));

        countdownText = CreateText(root, "Countdown", "3", 112, TextAnchor.MiddleCenter,
            new Vector2(410f, 30f), new Vector2(500f, 230f), new Color32(255, 239, 192, 255));
        AddOutline(countdownText.gameObject, new Color(0f, 0f, 0f, 0.92f), new Vector2(4f, -4f));
        countdownText.gameObject.SetActive(false);

        introPanel = CreateOverlayPanel(root, "IntroPanel", new Vector2(0f, -5f), new Vector2(750f, 590f));
        RectTransform introRect = introPanel.GetComponent<RectTransform>();
        Text introTitle = CreateText(introRect, "Title", "编一张抄纸竹帘", 46, TextAnchor.MiddleCenter,
            new Vector2(0f, 205f), new Vector2(650f, 80f), new Color32(248, 224, 170, 255));
        AddOutline(introTitle.gameObject, new Color(0f, 0f, 0f, 0.9f), new Vector2(2f, -2f));
        CreateText(introRect, "Body",
            "材料检查：竹丝 100 / 100\n\n竹丝落到横线时按 F 或 J\n白色弧线代表木梭带线摆过帘面\n每第 5 根会出现“结”，请同时按 F + J\n\n失误不会卡住进度，但会影响最终评级",
            27, TextAnchor.MiddleCenter, new Vector2(0f, 25f), new Vector2(650f, 310f),
            new Color32(229, 219, 197, 255));
        Text startText = CreateText(introRect, "Start", "按 SPACE 开始", 33, TextAnchor.MiddleCenter,
            new Vector2(0f, -225f), new Vector2(500f, 70f), new Color32(255, 239, 177, 255));
        AddOutline(startText.gameObject, new Color(0f, 0f, 0f, 0.88f), new Vector2(2f, -2f));

        resultPanel = CreateOverlayPanel(root, "ResultPanel", new Vector2(0f, -5f), new Vector2(780f, 650f));
        resultText = CreateText(resultPanel.GetComponent<RectTransform>(), "ResultText", "", 29, TextAnchor.MiddleCenter,
            Vector2.zero, new Vector2(700f, 570f), new Color32(238, 224, 194, 255));
        resultPanel.SetActive(false);
    }

    GameObject CreateOverlayPanel(RectTransform parent, string name, Vector2 position, Vector2 size)
    {
        Image image = CreateImage(parent, name, new Vector2(0.5f, 0.5f), position, size,
            new Color(0.075f, 0.048f, 0.032f, 0.965f));
        AddOutline(image.gameObject, new Color(0.83f, 0.64f, 0.34f, 0.82f), new Vector2(3f, -3f));
        return image.gameObject;
    }

    void CreateKeyMarker(RectTransform parent, string name, string key, Vector2 position, Color color)
    {
        Image image = CreateImage(parent, name, new Vector2(0.5f, 0.5f), position, new Vector2(116f, 64f), color);
        AddOutline(image.gameObject, new Color(0.08f, 0.045f, 0.025f, 0.95f), new Vector2(2f, -2f));
        Text text = CreateText(image.rectTransform, "Label", key, 40, TextAnchor.MiddleCenter,
            Vector2.zero, new Vector2(110f, 60f), Color.white);
        AddOutline(text.gameObject, new Color(0f, 0f, 0f, 0.8f), new Vector2(1.5f, -1.5f));
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

        leftClip = CreateTone("Weaving_Left", 510f, 0.075f, 0.04f);
        rightClip = CreateTone("Weaving_Right", 640f, 0.075f, 0.04f);
        knotClip = CreateTone("Weaving_Knot", 790f, 0.13f, 0.02f);
        missClip = CreateTone("Weaving_Miss", 125f, 0.11f, 0.12f);
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
            float tone = Mathf.Sin(Mathf.PI * 2f * frequency * time) * 0.65f;
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
        requiredThreads = Mathf.Max(1, requiredThreads);
        knotEvery = Mathf.Max(2, knotEvery);
        spawnInterval = Mathf.Max(0.2f, spawnInterval);
        noteTravelTime = Mathf.Max(0.5f, noteTravelTime);
        perfectWindow = Mathf.Max(0.01f, perfectWindow);
        greatWindow = Mathf.Max(perfectWindow, greatWindow);
        goodWindow = Mathf.Max(greatWindow, goodWindow);
        missWindow = Mathf.Max(goodWindow, missWindow);
        chordGrace = Mathf.Max(0.03f, chordGrace);
    }
}
