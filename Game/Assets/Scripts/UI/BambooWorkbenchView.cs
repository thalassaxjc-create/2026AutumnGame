using UnityEngine;
using UnityEngine.UI;

/// <summary>缓存的工作台界面和音效。小片竹青使用固定池，关闭/卸载时释放原生音频。</summary>
public class BambooWorkbenchView : MonoBehaviour
{
    static readonly Color Ink = new Color(0.14f, 0.23f, 0.18f);
    static readonly Color Cream = new Color(0.94f, 0.9f, 0.76f);
    BambooCraftProgress progress;
    BambooSurfaceGraphic surface;
    BambooFinishingGraphic finishingSurface;
    RectTransform root, blade, coverageFill, basketRoot;
    Text workHint, finishingNote;
    Text instruction, stage, stats, quality, detail, prompt, action, speedLabel, inventory;
    GameObject empty;
    AudioSource scrapeSource, cutSource;
    AudioClip scrapeClip, cutClip, metalClip;
    readonly RectTransform[] peel = new RectTransform[16];
    readonly Vector2[] peelStart = new Vector2[16];
    readonly float[] peelAge = new float[16];
    int nextPeel, lastRevision = -1, lastRaw = -1;
    float lastPeelTime;
    int lastFeedbackSerial;
    float nextFinishingTextTime;
    BambooCraftProgress.Step lastFinishingStep;
    public RectTransform SurfaceRect => surface.rectTransform;
    public RectTransform KnifeRect => blade;

    public static BambooWorkbenchView Create(BambooCraftProgress model)
    {
        var go = new GameObject("BambooWorkbenchUI", typeof(RectTransform));
        var view = go.AddComponent<BambooWorkbenchView>();
        view.Build(model);
        return view;
    }

    void Build(BambooCraftProgress model)
    {
        progress = model;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        Image backdrop = Panel(transform, "Backdrop", Vector2.zero, Vector2.zero, new Color(0.06f, 0.1f, 0.08f, 0.92f));
        backdrop.rectTransform.anchorMin = Vector2.zero; backdrop.rectTransform.anchorMax = Vector2.one;
        backdrop.rectTransform.offsetMin = backdrop.rectTransform.offsetMax = Vector2.zero;
        root = Panel(transform, "WorkbenchFrame", Vector2.zero, new Vector2(1460, 790), Cream).rectTransform;
        Panel(root, "Header", new Vector2(0, 310), new Vector2(1460, 170), Ink);
        Label(root, "篾竹 / 竹材初制", new Vector2(-665, 340), new Vector2(500, 58), 36, Cream);
        Label(root, "01 劈竹  /  02 剐青  /  03 剑门  /  04 抽丝", new Vector2(-665, 280), new Vector2(860, 42), 22, new Color(0.69f, 0.77f, 0.57f));
        Label(root, "ESC 返回工坊 · 进度保留", new Vector2(370, 341), new Vector2(330, 40), 22, Cream);
        inventory = Label(root, "", new Vector2(240, 281), new Vector2(450, 42), 18, Cream);
        Panel(root, "TableEdge", new Vector2(-145, -20), new Vector2(1090, 444), new Color(0.29f, 0.19f, 0.12f));
        var table = Panel(root, "WoodTable", new Vector2(-145, -14), new Vector2(1074, 428), new Color(0.57f, 0.39f, 0.24f));
        for (int i = 0; i < 18; i++)
            Panel(table.transform, "WoodGrain", new Vector2(0, -197 + i * 23), new Vector2(1054, i % 3 == 0 ? 3 : 1), new Color(0.27f, 0.16f, 0.07f, 0.25f));
        stage = Label(root, "", new Vector2(-650, 162), new Vector2(950, 40), 24, Cream);
        var surfaceGo = new GameObject("BambooSurface", typeof(RectTransform));
        surfaceGo.transform.SetParent(root, false);
        surface = surfaceGo.AddComponent<BambooSurfaceGraphic>();
        surface.raycastTarget = false;
        surface.Progress = model;
        surface.rectTransform.anchoredPosition = new Vector2(-145, -18);
        surface.rectTransform.sizeDelta = new Vector2(924, 168);
        empty = new GameObject("EmptyWorkbench", typeof(RectTransform));
        empty.transform.SetParent(root, false);
        Label(empty.transform, "把竹子放到这里", new Vector2(-440, -5), new Vector2(700, 50), 34, Cream);
        workHint = Label(root, "顺着竹纹行刀", new Vector2(-650, -185), new Vector2(980, 35), 19, new Color(0.89f, 0.74f, 0.49f));
        var finishingGo = new GameObject("BambooFinishingSurface", typeof(RectTransform));
        finishingGo.transform.SetParent(root, false);
        finishingSurface = finishingGo.AddComponent<BambooFinishingGraphic>();
        finishingSurface.Progress = model;
        finishingSurface.raycastTarget = false;
        finishingSurface.rectTransform.anchoredPosition = new Vector2(-145, -18);
        finishingSurface.rectTransform.sizeDelta = new Vector2(924, 260);

        quality = Label(root, "", new Vector2(435, 165), new Vector2(260, 50), 28, Ink);
        stats = Label(root, "", new Vector2(435, 110), new Vector2(260, 38), 21, Ink);
        Panel(root, "ProgressTrack", new Vector2(557, 65), new Vector2(246, 9), new Color(0.75f, 0.76f, 0.64f));
        coverageFill = Panel(root, "ProgressFill", new Vector2(434, 65), new Vector2(246, 9), new Color(0.28f, 0.45f, 0.25f)).rectTransform;
        coverageFill.pivot = new Vector2(0, 0.5f);
        detail = Label(root, "", new Vector2(435, 16), new Vector2(260, 76), 18, Ink);
        speedLabel = Label(root, "", new Vector2(435, -45), new Vector2(270, 38), 18, Ink);
        // A scraper rests next to the strip; during a drag its blade follows the cursor.
        blade = Panel(root, "Scraper", new Vector2(320, -151), new Vector2(58, 16), new Color(0.74f, 0.78f, 0.74f)).rectTransform;
        Panel(blade, "Handle", new Vector2(0, -23), new Vector2(14, 40), new Color(0.24f, 0.15f, 0.08f));
        blade.localRotation = Quaternion.Euler(0, 0, -20);
        basketRoot = new GameObject("BasketGroup", typeof(RectTransform)).GetComponent<RectTransform>();
        basketRoot.SetParent(root, false);
        Panel(basketRoot, "Basket", new Vector2(555, -167), new Vector2(210, 132), new Color(0.47f, 0.34f, 0.19f));
        for (int i = 0; i < 10; i++)
            Panel(basketRoot, "BasketWeave", new Vector2(461 + i * 21, -167), new Vector2(5, 132), new Color(0.69f, 0.52f, 0.3f));
        for (int i = 0; i < 6; i++)
            Panel(basketRoot, "BasketWeave", new Vector2(555, -225 + i * 23), new Vector2(210, 4), new Color(0.29f, 0.23f, 0.13f));
        Label(basketRoot, "竹青篓", new Vector2(510, -254), new Vector2(130, 36), 21, Ink);
        finishingNote = Label(root, "", new Vector2(435, -165), new Vector2(260, 190), 21, Ink);
        instruction = Label(root, "", new Vector2(-650, -281), new Vector2(1300, 64), 24, Ink);
        prompt = Label(root, "", new Vector2(-650, -341), new Vector2(920, 42), 19, Ink);
        action = Label(root, "", new Vector2(350, -341), new Vector2(350, 42), 23, Ink);

        for (int i = 0; i < peel.Length; i++)
        {
            peel[i] = Panel(root, "BambooPeeling", Vector2.zero, new Vector2(14, 4), new Color(0.3f, 0.47f, 0.18f)).rectTransform;
            peel[i].gameObject.SetActive(false);
            peelAge[i] = 1;
        }
        scrapeSource = gameObject.AddComponent<AudioSource>();
        cutSource = gameObject.AddComponent<AudioSource>();
        scrapeSource.playOnAwake = cutSource.playOnAwake = false;
        scrapeSource.loop = true;
        scrapeClip = MakeSound("BambooScrape", 0.4f, false);
        cutClip = MakeSound("BambooSplit", 0.11f, true);
        metalClip = MakeMetalSound();
        scrapeSource.clip = scrapeClip;
        lastFeedbackSerial = progress.FinishingFeedbackSerial;
        Refresh();
    }

    public bool ScreenToSurface(Vector2 screen, out Vector2 point)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(surface.rectTransform, screen, null, out Vector2 local);
        Rect rect = surface.rectTransform.rect;
        point = new Vector2((local.x - rect.x) / rect.width, (local.y - rect.y) / rect.height);
        // Screen/canvas conversion can place an edge pixel a fraction outside the rectangle.
        if (point.x >= -0.002f && point.x <= 1.002f && point.y >= -0.006f && point.y <= 1.006f)
            point = new Vector2(Mathf.Clamp01(point.x), Mathf.Clamp01(point.y));
        return progress.Stage == BambooCraftProgress.Step.Splitting
            ? BambooCraftProgress.InsideSplitArea(point) : BambooCraftProgress.Inside(point);
    }

    public void Pointer(Vector2 screen, bool held, float speed, int removedCells)
    {
        if (progress.IsFinishingStage) { scrapeSource.Stop(); return; }
        scrapeSource.pitch = 1;
        bool scraping = held && progress.Stage == BambooCraftProgress.Step.Scraping
            && ScreenToSurface(screen, out _) && speed > 0.002f;
        if (scraping)
        {
            scrapeSource.volume = Mathf.Clamp(speed * 0.35f, 0.07f, 0.34f);
            if (!scrapeSource.isPlaying) scrapeSource.Play();
        }
        else scrapeSource.Stop();
        if (held && RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 local))
            blade.anchoredPosition = local;
        else blade.anchoredPosition = new Vector2(320, -151);
        string speedText = progress.Stage == BambooCraftProgress.Step.Splitting ? "横向划一次即可"
            : progress.Stage == BambooCraftProgress.Step.RoughReady ? "已获得 4 块粗竹片"
            : !scraping ? "竹节处请慢行" : speed <= BambooCraftProgress.SafeNodeSpeed ? "当前刀速：平稳" : "当前刀速：快 · 留意竹节";
        if (speedLabel.text != speedText) speedLabel.text = speedText;
        if (removedCells > 0 && Time.unscaledTime - lastPeelTime > 0.035f)
        {
            int i = nextPeel++ % peel.Length;
            peelAge[i] = 0;
            peelStart[i] = blade.anchoredPosition;
            peel[i].anchoredPosition = peelStart[i];
            peel[i].gameObject.SetActive(true);
            lastPeelTime = Time.unscaledTime;
        }
    }

    public void PlaySplit() => cutSource.PlayOneShot(cutClip, 0.6f);

    public void FinishingInput(bool pulling)
    {
        bool active = pulling && progress.Stage == BambooCraftProgress.Step.Drawing && !progress.DrawNeedsRelease;
        if (active)
        {
            scrapeSource.volume = 0.12f;
            scrapeSource.pitch = 0.75f + progress.Tension * 0.35f;
            if (!scrapeSource.isPlaying) scrapeSource.Play();
        }
        else scrapeSource.Stop();
    }

    public void Refresh()
    {
        if (lastRevision == progress.Revision && lastRaw == GreenBambooBag.Count) return;
        lastRevision = progress.Revision; lastRaw = GreenBambooBag.Count;
        var step = progress.Stage;
        bool splitting = step == BambooCraftProgress.Step.Splitting || step == BambooCraftProgress.Step.RoughReady;
        bool waiting = step == BambooCraftProgress.Step.PlaceBamboo;
        bool finishing = progress.IsFinishingStage;
        surface.gameObject.SetActive(!waiting && !finishing);
        empty.SetActive(waiting);
        finishingSurface.gameObject.SetActive(finishing);
        finishingNote.gameObject.SetActive(finishing);
        basketRoot.gameObject.SetActive(!finishing);
        blade.gameObject.SetActive(!finishing);
        if (finishing)
        {
            finishingSurface.SetVerticesDirty();
            if (lastFinishingStep != step || Time.unscaledTime >= nextFinishingTextTime)
            {
                lastFinishingStep = step;
                nextFinishingTextTime = Time.unscaledTime + 0.08f;
                RefreshFinishing();
            }
            return;
        }
        workHint.text = "顺着竹纹行刀";
        surface.SetVerticesDirty();
        int rounds = BambooCraftProgress.ScrapingRounds;
        int round = Mathf.Min(progress.ScrapingRoundsCompleted + (step == BambooCraftProgress.Step.StripFinished ? 0 : 1), rounds);
        stage.text = waiting ? "准备 / 放置完整青竹" : splitting ? "01 / 劈竹     " + progress.Splits + " / 1 刀"
            : $"02 / 剐竹青     第 {round} / {rounds} 轮 · 每轮两块";
        quality.text = waiting ? "准确率  —" : "准确率  " + Mathf.RoundToInt(progress.Accuracy * 100) + "%";
        stats.text = splitting ? $"粗竹片  {progress.RoughCount} / {BambooCraftProgress.PiecesPerBatch}"
            : "刮青完成  " + Mathf.FloorToInt(progress.Coverage * 100) + "%";
        coverageFill.sizeDelta = new Vector2(246 * (splitting ? progress.Splits : progress.Coverage), 9);
        detail.text = "深痕 " + progress.Damage + "  /  残次风险 " + Mathf.RoundToInt(progress.DefectChance * 100) + "%\n精竹片 " + progress.FineCount + "   残次 " + progress.DefectiveCount;
        inventory.text = "青竹 " + GreenBambooBag.Count + "   粗片 " + progress.RoughCount + "   精片 " + progress.FineCount + "   残次 " + progress.DefectiveCount;
        instruction.text = progress.Message;
        prompt.text = splitting ? "左右均可；大致顺着亮线划过，无需紧贴或划满全长" : "按住左键持续持刀，移回竹面继续；竹节处避免猛划";
        action.text = waiting ? "空格  放置青竹"
            : step == BambooCraftProgress.Step.BatchFinished ? "空格  继续过剑门"
            : step == BambooCraftProgress.Step.RoughReady || step == BambooCraftProgress.Step.StripFinished ? "空格  开始下一轮" : "左键按住并拖动";
        if (step == BambooCraftProgress.Step.BatchFinished)
        {
            stage.text = "两轮剐青完成 / 合格 " + progress.FineCount + " · 残次 " + progress.DefectiveCount;
            prompt.text = "四片均可继续过剑门，残次标记会保留到后续成品";
        }
    }

    void RefreshFinishing()
    {
        var step = progress.Stage;
        bool gate = progress.IsGateStage;
        bool complete = step == BambooCraftProgress.Step.CraftFinished;
        int groups = BambooCraftProgress.FinishingGroups;
        int rods = BambooCraftProgress.RodsPerGroup;
        int gateNumber = Mathf.Min(progress.GatesCompleted + (step == BambooCraftProgress.Step.GatePassing ? 1 : 0), groups);
        stage.text = gate ? $"03 / 过剑门     第 {gateNumber} / {groups} 轮 · 每轮 {rods} 根"
            : complete ? "两轮抽丝完成 / 100 根已入库"
            : step == BambooCraftProgress.Step.DrawingReady ? $"04 / 抽竹丝     已完成 {progress.DrawingGroupsCompleted} / {groups} 轮"
            : $"04 / 抽竹丝     第 {progress.DrawingGroupsCompleted + 1} / {groups} 轮 · 每轮 {rods} 根";
        quality.text = "准确率  " + Mathf.RoundToInt(progress.Accuracy * 100) + "%";
        stats.text = gate ? $"已通过  {progress.GatesCompleted} / {groups}"
            : complete ? "抽丝完成  100%" : step == BambooCraftProgress.Step.DrawingReady ? $"下一轮  {rods} 根待加工"
            : "本组抽丝  " + Mathf.FloorToInt(progress.DrawProgress * 100) + "%";
        coverageFill.sizeDelta = new Vector2(246 * (gate ? progress.GatesCompleted / (float)groups : complete ? 1 : progress.DrawProgress), 9);
        detail.text = gate ? "均匀 " + progress.PerfectGates + "  /  碰刃 " + (progress.GatesCompleted - progress.PerfectGates + (step == BambooCraftProgress.Step.GatePassing && progress.GateDamaged ? 1 : 0))
            + "\n合格竹条 " + progress.RodCount
            : $"张力 {Mathf.RoundToInt(progress.Tension * 100)}%  /  本轮受损 {progress.DrawErrors}\n已完成 {progress.DrawingGroupsCompleted} / {groups} 轮";
        speedLabel.text = gate ? (Mathf.Abs(progress.PieceTilt) <= BambooCraftProgress.SafeGateTilt ? "竹片已回平" : "松开 W / S 让竹片回平")
            : progress.DrawNeedsRelease ? "请松手，等待张力回落"
            : step != BambooCraftProgress.Step.Drawing ? (complete ? "成品已入库" : "按空格开始下一组")
            : progress.DangerWarning ? "预警：危险区即将出现"
            : progress.DangerActive ? "注意红区，及时松手" : "分段拉拽 · 松手回弹";
        inventory.text = "竹条 " + progress.RodCount + "  残条 " + progress.DefectiveRodCount + "  成丝 " + progress.ThreadCount + "  残丝 " + progress.DefectiveThreadCount;
        finishingNote.text = gate ? "4 块已剐青竹片\n↓\n100 根竹条\n\n带入残次 " + progress.InheritedDefectivePieces + " 片"
            : $"{groups} 轮 × {rods} 根\n\n合格竹丝  {progress.ThreadCount}\n残次竹丝  {progress.DefectiveThreadCount}";
        instruction.text = progress.Message;
        workHint.text = gate ? "W 上移 / S 下移    ·    松开后自动回平    ·    每轮剑门高度不同" : "钳子夹持 → 穿过圆钢片 → 分段抽细     下方为张力条";
        prompt.text = gate ? "对齐刀孔后提前松手；允许小幅高度与角度偏差"
            : complete ? "成品已入库，退出工位或切换场景后本局库存保留"
            : "按住右键拉拽；松开降张力；橙色预警后出现红色危险区";
        action.text = gate ? "W / S  调整高度"
            : step == BambooCraftProgress.Step.DrawingReady ? "空格  开始下一组"
            : complete ? (WorkshopOrder.Current != null ? "Esc  返回工坊编帘" : "空格  加工下一批") : "右键按住 / 松开";
    }

    void Update()
    {
        if (lastFeedbackSerial != progress.FinishingFeedbackSerial)
        {
            lastFeedbackSerial = progress.FinishingFeedbackSerial;
            cutSource.PlayOneShot(metalClip, 0.35f);
        }
        Refresh();
        for (int i = 0; i < peel.Length; i++)
        {
            if (peelAge[i] >= 1 || peel[i] == null) continue;
            peelAge[i] += Time.unscaledDeltaTime * 1.5f;
            float t = Mathf.Clamp01(peelAge[i]);
            peel[i].anchoredPosition = Vector2.Lerp(peelStart[i], new Vector2(490 + i * 8, -180), t) + Vector2.up * Mathf.Sin(t * Mathf.PI) * 100;
            peel[i].localRotation = Quaternion.Euler(0, 0, t * 360 + i * 17);
            if (t >= 1) peel[i].gameObject.SetActive(false);
        }
    }

    void OnDisable()
    {
        if (scrapeSource != null) scrapeSource.Stop();
        if (cutSource != null) cutSource.Stop();
        for (int i = 0; i < peel.Length; i++)
        {
            peelAge[i] = 1;
            if (peel[i] != null) peel[i].gameObject.SetActive(false);
        }
    }

    void OnDestroy()
    {
        if (scrapeClip != null) Destroy(scrapeClip);
        if (cutClip != null) Destroy(cutClip);
        if (metalClip != null) Destroy(metalClip);
    }

    static AudioClip MakeMetalSound()
    {
        const int rate = 22050;
        float[] samples = new float[rate / 4];
        var noise = new System.Random(19);
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)rate;
            samples[i] = (Mathf.Sin(t * 2600 * Mathf.PI * 2) * 0.35f + ((float)noise.NextDouble() * 2 - 1) * 0.4f)
                * Mathf.Sin(Mathf.PI * i / samples.Length);
        }
        AudioClip clip = AudioClip.Create("BambooBladeScrape", samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    static AudioClip MakeSound(string name, float duration, bool impact)
    {
        const int rate = 22050;
        float[] data = new float[Mathf.RoundToInt(rate * duration)];
        var rng = new System.Random(73);
        float filtered = 0;
        for (int i = 0; i < data.Length; i++)
        {
            filtered = Mathf.Lerp(filtered, (float)rng.NextDouble() * 2 - 1, 0.65f);
            float envelope = impact ? Mathf.Exp(-i / (float)rate * 45) : 0.7f;
            data[i] = filtered * envelope;
        }
        AudioClip clip = AudioClip.Create(name, data.Length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static Image Panel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.raycastTarget = false; image.color = color;
        image.rectTransform.anchoredPosition = position; image.rectTransform.sizeDelta = size;
        return image;
    }

    static Text Label(Transform parent, string message, Vector2 position, Vector2 size, int fontSize, Color color)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Text text = go.AddComponent<Text>();
        text.raycastTarget = false; text.alignment = TextAnchor.MiddleLeft;
        text.fontSize = fontSize; text.color = color; text.text = message;
        text.rectTransform.pivot = new Vector2(0, 0.5f);
        text.rectTransform.anchoredPosition = position; text.rectTransform.sizeDelta = size;
        InteractionPromptUI.EnsureChineseFont(text);
        return text;
    }
}
