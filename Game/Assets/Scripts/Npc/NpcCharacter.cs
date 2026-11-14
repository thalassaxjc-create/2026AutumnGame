using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 白盒 NPC。只站在原地，头顶显示名字，不参与玩家输入。
/// </summary>
[DisallowMultipleComponent]
public class NpcCharacter : MonoBehaviour
{
    [SerializeField] string displayName;
    Transform labelPivot;

    public string DisplayName => displayName;

    public void Configure(string npcName)
    {
        displayName = npcName;
        EnsureLabel();
    }

    void Awake()
    {
        EnsureLabel();
    }

    void LateUpdate()
    {
        if (labelPivot == null)
            return;

        Camera camera = Camera.main;
        if (camera == null)
            return;

        labelPivot.rotation = Quaternion.LookRotation(labelPivot.position - camera.transform.position, Vector3.up);
    }

    void EnsureLabel()
    {
        if (string.IsNullOrEmpty(displayName))
            displayName = gameObject.name;

        Transform existing = transform.Find("Nameplate");
        GameObject plate = existing != null ? existing.gameObject : new GameObject("Nameplate");
        plate.transform.SetParent(transform, false);
        plate.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        labelPivot = plate.transform;

        Canvas canvas = plate.GetComponent<Canvas>();
        if (canvas == null)
            canvas = plate.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 20;

        RectTransform canvasRect = plate.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(300f, 64f);
        canvasRect.localScale = new Vector3(0.018f, 0.018f, 0.018f);

        Transform textTransform = plate.transform.Find("Name");
        GameObject textGo = textTransform != null ? textTransform.gameObject : new GameObject("Name");
        textGo.transform.SetParent(plate.transform, false);
        Text text = textGo.GetComponent<Text>();
        if (text == null)
            text = textGo.AddComponent<Text>();

        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 36;
        text.color = Color.white;
        text.raycastTarget = false;
        text.text = displayName;
        InteractionPromptUI.EnsureChineseFont(text);

        Outline outline = textGo.GetComponent<Outline>();
        if (outline == null)
            outline = textGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);
    }
}
