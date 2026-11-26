using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

internal sealed class WorkshopJournalView : MonoBehaviour
{
    WorkshopJournal journal;
    Button open;
    GameObject page;
    SettlementExitController modal;
    Text title;
    Text notes;
    Text position;
    RawImage photo;
    AspectRatioFitter aspect;
    Button previous;
    Button next;
    Texture2D display;
    bool people;
    int index;
    Coroutine capture;

    List<WorkshopJournal.Entry> Entries => people ? journal.People : journal.Events;

    internal void Build(WorkshopJournal book)
    {
        journal = book;
        open = WorkshopStoryUI.Button(transform, "打开小册子", "小册子 [N]",
            new Rect(0, 0, 0, 0), WorkshopStoryUI.Wood, 24, OpenBook);
        var openRect = (RectTransform)open.transform;
        openRect.pivot = new Vector2(0, 0);
        openRect.anchoredPosition = new Vector2(32, 26);
        openRect.sizeDelta = new Vector2(176, 44);
        var modalOwner = new GameObject("小册子交互");
        modalOwner.transform.SetParent(transform, false);
        modal = modalOwner.AddComponent<SettlementExitController>();
    }

    void LateUpdate()
    {
        if (journal == null)
            return;
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            journal.Unphotographed.Clear();
        if (capture == null && journal.Unphotographed.Count > 0 && !SceneTransitionState.IsLoading)
            capture = StartCoroutine(CaptureEventFrame());

        bool available = !SceneTransitionState.IsLoading && !GameFlowModalState.IsModalOpen &&
            WorkshopOrder.Current?.CurrentPhase != WorkshopOrder.Phase.Opening;
        open.gameObject.SetActive(available);
        Keyboard keyboard = Keyboard.current;
        if (modal.IsSettlementActive && keyboard != null &&
            (keyboard.escapeKey.wasPressedThisFrame || keyboard.nKey.wasPressedThisFrame))
        {
            CloseBook();
            return;
        }
        if (!available || capture != null)
            return;
        if (journal.Unread.Count > 0 || (keyboard != null && keyboard.nKey.wasPressedThisFrame))
            OpenBook();
    }

    IEnumerator CaptureEventFrame()
    {
        yield return new WaitForEndOfFrame();
        Texture2D screenshot = null;
        Texture2D reduced = null;
        RenderTexture target = null;
        RenderTexture previousTarget = RenderTexture.active;
        try
        {
            byte[] jpeg = null;
            if (!SceneTransitionState.IsLoading)
            {
                screenshot = ScreenCapture.CaptureScreenshotAsTexture();
                if (screenshot != null)
                {
                    int width = Mathf.Min(800, screenshot.width);
                    int height = Mathf.Max(1, Mathf.RoundToInt(screenshot.height * width / (float)screenshot.width));
                    target = RenderTexture.GetTemporary(width, height, 0);
                    Graphics.Blit(screenshot, target);
                    RenderTexture.active = target;
                    reduced = new Texture2D(width, height, TextureFormat.RGB24, false);
                    reduced.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                    jpeg = ImageConversion.EncodeToJPG(reduced, 78);
                }
            }
            while (journal.Unphotographed.Count > 0)
                journal.Unphotographed.Dequeue().Photo = jpeg;
        }
        catch (System.Exception error)
        {
            journal.Unphotographed.Clear();
            Debug.LogWarning($"小册子截图失败，文字记录仍可查看：{error.Message}", this);
        }
        finally
        {
            RenderTexture.active = previousTarget;
            if (target != null) RenderTexture.ReleaseTemporary(target);
            if (screenshot != null) Destroy(screenshot);
            if (reduced != null) Destroy(reduced);
            capture = null;
        }
    }

    void OpenBook()
    {
        if (GameFlowModalState.IsModalOpen || SceneTransitionState.IsLoading)
            return;
        if (page == null)
            BuildPage();

        if (journal.Unread.Count > 0)
        {
            people = false;
            index = journal.Events.IndexOf(journal.Unread.Dequeue());
        }
        else
        {
            index = Mathf.Max(0, Entries.Count - 1);
        }
        RenderPage();
        modal.BeginSettlement(page);
    }

    void BuildPage()
    {
        page = WorkshopStoryUI.Panel(transform, "宣纸帘小册子", new Rect(0, 0, 1, 1),
            new Color32(30, 24, 18, 209), true).gameObject;
        var sheet = WorkshopStoryUI.Panel(page.transform, "册页", new Rect(.16f, .07f, .68f, .86f),
            WorkshopStoryUI.Paper, true).transform;
        WorkshopStoryUI.Button(sheet, "事件书签", "事件", new Rect(.06f, .89f, .18f, .08f),
            WorkshopStoryUI.Green, 28, () => Select(false));
        WorkshopStoryUI.Button(sheet, "人物书签", "人物", new Rect(.26f, .89f, .18f, .08f),
            WorkshopStoryUI.Green, 28, () => Select(true));
        WorkshopStoryUI.Button(sheet, "关闭小册子", "合上 [Esc]", new Rect(.74f, .89f, .20f, .08f),
            WorkshopStoryUI.Wood, 24, CloseBook);
        title = WorkshopStoryUI.Label(sheet, "条目标题", "", new Rect(.06f, .77f, .88f, .1f),
            38, WorkshopStoryUI.Ink, bold: true);
        var photoArea = WorkshopStoryUI.Region(sheet, "截图区域", new Rect(.12f, .31f, .76f, .45f));
        photo = WorkshopStoryUI.Region(photoArea, "事件截图", new Rect(0, 0, 1, 1)).gameObject.AddComponent<RawImage>();
        photo.raycastTarget = false;
        aspect = photo.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        notes = WorkshopStoryUI.Label(sheet, "记录", "", new Rect(.08f, .12f, .84f, .18f),
            29, WorkshopStoryUI.Ink);
        position = WorkshopStoryUI.Label(sheet, "页码", "", new Rect(.33f, .02f, .34f, .075f),
            24, WorkshopStoryUI.Ink);
        previous = WorkshopStoryUI.Button(sheet, "上一页", "上一页", new Rect(.08f, .02f, .2f, .075f),
            WorkshopStoryUI.Wood, 24, () => Turn(-1));
        next = WorkshopStoryUI.Button(sheet, "下一页", "下一页", new Rect(.72f, .02f, .2f, .075f),
            WorkshopStoryUI.Wood, 24, () => Turn(1));
        page.SetActive(false);
    }

    void Select(bool showPeople)
    {
        people = showPeople;
        index = Mathf.Max(0, Entries.Count - 1);
        RenderPage();
    }

    void Turn(int offset)
    {
        index += offset;
        RenderPage();
    }

    void RenderPage()
    {
        ReleasePhoto();
        int count = Entries.Count;
        var entry = count == 0 ? null : Entries[index];
        title.text = entry?.Title ?? (people ? "人物" : "事件");
        notes.text = entry?.Notes ?? (people ? "和村镇的人交谈后，名字会记在这里。" : "完成订单或重要事件后，会在这里留下一页。");
        position.text = $"{(people ? "人物" : "事件")} · {(count == 0 ? 0 : index + 1)}/{count}";
        previous.interactable = index > 0;
        next.interactable = index + 1 < count;
        if (entry?.Photo != null)
        {
            display = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (ImageConversion.LoadImage(display, entry.Photo, true))
            {
                aspect.aspectRatio = display.width / (float)display.height;
                photo.texture = display;
            }
        }
        bool hasPhoto = photo.texture != null;
        photo.gameObject.SetActive(hasPhoto);
        notes.rectTransform.anchorMin = new Vector2(.08f, hasPhoto ? .12f : .31f);
        notes.rectTransform.anchorMax = new Vector2(.92f, hasPhoto ? .30f : .73f);
    }

    void CloseBook()
    {
        modal.CloseAndResume();
        ReleasePhoto();
    }

    void ReleasePhoto()
    {
        if (photo != null)
            photo.texture = null;
        if (display != null)
            Destroy(display);
        display = null;
    }

    void OnDisable()
    {
        if (capture != null)
            StopCoroutine(capture);
        capture = null;
        journal?.Unphotographed.Clear();
        if (modal != null && modal.IsSettlementActive)
            modal.CloseAndResume();
        ReleasePhoto();
    }
}
