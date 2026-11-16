using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一根可砍的青竹。空格连砍到指定次数后倒下，并留下可拾取的完整青竹。
/// </summary>
[DisallowMultipleComponent]
public class ChopableBamboo : MonoBehaviour
{
    public enum Stage
    {
        Standing,
        Falling,
        Dropped,
        Collected
    }

    [SerializeField, Min(1), Tooltip("砍断所需的空格次数。")]
    int chopsToFell = 8;

    [SerializeField, Min(0.2f), Tooltip("玩家水平距离小于该值时可以砍或拾取。")]
    float interactRadius = 3.2f;

    [SerializeField, Min(0.05f), Tooltip("竹竿倒下所用的时间。")]
    float fallDuration = 0.7f;

    [SerializeField, Tooltip("可选的砍击音效；留空时生成短促的竹木咔声。")]
    AudioClip chopSound;

    [SerializeField, Tooltip("可选的断裂音效。")]
    AudioClip breakSound;

    const float CutHeight = 0.4f;

    static readonly float[] ChopPitches = { 1.02f, 1.08f, 0.96f, 1.12f, 1f, 1.06f, 0.94f, 0.78f };
    static AudioClip chopClip;
    static AudioClip breakClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetAudioCache()
    {
        ReleaseAudioCache();
        Application.quitting -= ReleaseAudioCache;
        Application.quitting += ReleaseAudioCache;
#if UNITY_EDITOR
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ReleaseAudioCache;
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ReleaseAudioCache;
        UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif
    }

#if UNITY_EDITOR
    static void OnPlayModeChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.EnteredEditMode)
            ReleaseAudioCache();
    }
#endif

    static void ReleaseAudioCache()
    {
#if UNITY_EDITOR
        if (chopClip != null) DestroyImmediate(chopClip);
        if (breakClip != null) DestroyImmediate(breakClip);
#else
        if (chopClip != null) Destroy(chopClip);
        if (breakClip != null) Destroy(breakClip);
#endif
        chopClip = null;
        breakClip = null;
    }

    Stage stage = Stage.Standing;
    int chops;
    float punch;
    float fallT;
    Vector3 fallDirection = Vector3.forward;
    Quaternion fallFrom;
    Quaternion fallTo;
    Transform stalk;
    Transform visualRoot;
    Material bambooMaterial;
    AudioSource audioSource;
    GameObject mark;
    GameObject drop;
    Transform dropLabel;

    public Stage CurrentStage => stage;
    public int Chops => chops;
    public int ChopsToFell => chopsToFell;
    public float InteractRadius => interactRadius;
    public bool CanChop => isActiveAndEnabled && stage == Stage.Standing;
    public bool CanPickup => isActiveAndEnabled && stage == Stage.Dropped && drop != null;
    public bool IsInteractable => CanChop || CanPickup;

    public Vector3 InteractPoint
    {
        get
        {
            if (stage == Stage.Dropped && drop != null)
                return drop.transform.position;
            return transform.position;
        }
    }

    public string PromptText
    {
        get
        {
            if (CanPickup)
                return "按空格键拾取完整青竹";
            if (chops <= 0)
                return "按空格键砍伐";
            return "按空格键砍伐（" + chops + "/" + chopsToFell + "）";
        }
    }

    void Awake()
    {
        chopsToFell = Mathf.Max(1, chopsToFell);
        interactRadius = Mathf.Max(0.2f, interactRadius);
        fallDuration = Mathf.Max(0.05f, fallDuration);

        stalk = transform.Find("Stalk");
        if (stalk != null)
            bambooMaterial = stalk.GetComponent<Renderer>().sharedMaterial;

        EnsureSource();
        if (Application.isPlaying && enabled)
            BuildPlayVisuals();
    }

    void Update()
    {
        UpdatePunch();
        UpdateFall();
    }

    void LateUpdate()
    {
        if (dropLabel == null)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        dropLabel.rotation = Quaternion.LookRotation(dropLabel.position - cam.transform.position, Vector3.up);
    }

    public bool TryChop(Vector3 playerPosition)
    {
        if (!CanChop || !IsInRange(playerPosition))
            return false;

        // Input is edge-triggered by BambooHarvest: each new press is exactly one chop.
        chops++;
        punch = 0.16f;
        bool breaking = chops >= chopsToFell;
        PlayChopSound(breaking);
        ChopPop.Spawn(PopPosition(), "咔", breaking ? 1.35f : 1f);

        if (breaking)
            BeginFall(playerPosition);

        return true;
    }

    public bool IsInRange(Vector3 playerPosition)
    {
        Vector3 offset = playerPosition - InteractPoint;
        offset.y = 0f;
        return offset.sqrMagnitude <= interactRadius * interactRadius;
    }

    public void SetHighlighted(bool highlighted)
    {
        if (mark == null)
            return;
        mark.transform.localScale = new Vector3(highlighted ? 1.8f : 1.05f, 0.025f, highlighted ? 1.8f : 1.05f);
        Tint(mark.GetComponent<Renderer>(), highlighted
            ? new Color(1f, 0.85f, 0.28f) : new Color(0.42f, 0.6f, 0.25f));
    }

    public bool TryPickup(Vector3 playerPosition)
    {
        if (!CanPickup || !IsInRange(playerPosition))
            return false;

        if (dropLabel != null)
            Destroy(dropLabel.gameObject);
        if (drop != null)
            Destroy(drop);

        dropLabel = null;
        drop = null;
        stage = Stage.Collected;
        GreenBambooBag.AddOne();
        return true;
    }

    void BeginFall(Vector3 playerPosition)
    {
        stage = Stage.Falling;
        fallT = 0f;
        punch = 0f;
        if (visualRoot != null)
            visualRoot.localRotation = Quaternion.identity;

        Vector3 away = transform.position - playerPosition;
        away.y = 0f;
        if (away.sqrMagnitude < 0.04f)
            away = Vector3.forward;
        fallDirection = away.normalized;

        fallFrom = visualRoot != null ? visualRoot.rotation : transform.rotation;
        // Rotate the upper stem around the cut, away from the player (positive tilt).
        Vector3 fallAxis = Vector3.Cross(Vector3.up, fallDirection);
        fallTo = Quaternion.AngleAxis(86f, fallAxis) * fallFrom;

        if (mark != null)
            mark.SetActive(false);

        Collider[] cols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null)
                cols[i].enabled = false;
        }
    }

    void UpdateFall()
    {
        if (stage != Stage.Falling)
            return;

        fallT += Time.deltaTime / fallDuration;
        float t = Mathf.Clamp01(fallT);
        float eased = t * t;
        if (visualRoot != null)
            visualRoot.rotation = Quaternion.Slerp(fallFrom, fallTo, eased);
        if (fallT < 1f)
            return;

        if (visualRoot != null)
            visualRoot.rotation = fallTo;
        SpawnDrop();
    }

    void UpdatePunch()
    {
        if (visualRoot == null || stage != Stage.Standing)
            return;

        if (punch <= 0f)
        {
            visualRoot.localRotation = Quaternion.identity;
            return;
        }

        punch -= Time.deltaTime;
        float k = Mathf.Clamp01(punch / 0.16f);
        float swing = Mathf.Sin(k * Mathf.PI) * 8f;
        visualRoot.localRotation = Quaternion.Euler(swing, 0f, swing * 0.3f);
    }

    void SpawnDrop()
    {
        stage = Stage.Dropped;
        if (visualRoot != null)
            visualRoot.gameObject.SetActive(false);

        float height = stalk != null ? stalk.localScale.y * 2f : 4f;
        Vector3 pos = transform.position + fallDirection * Mathf.Clamp(height * 0.42f, 1.1f, 2.4f);
        pos.y = transform.position.y + 0.18f;

        drop = MakeCylinder(GreenBambooBag.ItemName, transform, Vector3.zero, new Vector3(0.22f, 0.85f, 0.22f));
        drop.transform.SetPositionAndRotation(
            pos,
            Quaternion.LookRotation(fallDirection, Vector3.up) * Quaternion.Euler(90f, 0f, 0f));

        Renderer renderer = drop.GetComponent<Renderer>();
        Tint(renderer, new Color(0.36f, 0.74f, 0.32f));

        for (int i = -1; i <= 1; i++)
        {
            GameObject node = MakeCylinder("DropNode", drop.transform,
                new Vector3(0f, i * 0.65f, 0f), new Vector3(1.12f, 0.025f, 1.12f));
            Tint(node.GetComponent<Renderer>(), new Color(0.66f, 0.83f, 0.39f));
        }

        dropLabel = CreateLabel(transform, "完整青竹", pos + Vector3.up * 0.85f);
    }

    void BuildPlayVisuals()
    {
        // Keep the stump/root fixed; the stem, nodes and leaves move as a single piece.
        visualRoot = new GameObject("FallingStem").transform;
        visualRoot.SetParent(transform, false);
        visualRoot.localPosition = Vector3.up * CutHeight;
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != visualRoot)
                child.SetParent(visualRoot, true);
        }
        if (stalk != null)
        {
            stalk.localPosition += Vector3.up * (CutHeight * 0.5f);
            Vector3 scale = stalk.localScale;
            scale.y = Mathf.Max(0.1f, scale.y - CutHeight * 0.5f);
            stalk.localScale = scale;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || renderers[i].gameObject.name.StartsWith("Leaf"))
                continue;
            Tint(renderers[i], new Color(0.64f, 0.8f, 0.36f));
        }

        mark = MakeCylinder("HarvestMark", transform, new Vector3(0f, 0.05f, 0f), Vector3.one);
        SetHighlighted(false);

        GameObject stump = MakeCylinder("BambooStump", transform,
            new Vector3(0f, CutHeight * 0.5f, 0f),
            new Vector3(0.27f, CutHeight * 0.5f, 0.27f));
        Tint(stump.GetComponent<Renderer>(), new Color(0.66f, 0.76f, 0.38f));
    }

    GameObject MakeCylinder(string objectName, Transform parent, Vector3 position, Vector3 scale)
    {
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = objectName;
        cylinder.transform.SetParent(parent, false);
        cylinder.transform.localPosition = position;
        cylinder.transform.localScale = scale;
        Collider collider = cylinder.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        if (bambooMaterial != null)
            cylinder.GetComponent<Renderer>().sharedMaterial = bambooMaterial;
        return cylinder;
    }

    void EnsureSource()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 1f;
        audioSource.loop = false;
    }

    void PlayChopSound(bool breaking)
    {
        EnsureSource();
        int index = Mathf.Clamp(chops - 1, 0, ChopPitches.Length - 1);
        audioSource.pitch = ChopPitches[index];
        audioSource.PlayOneShot(chopSound != null ? chopSound : ChopClip, 0.9f);
        if (breaking)
            audioSource.PlayOneShot(breakSound != null ? breakSound : BreakClip, 0.85f);
    }

    Vector3 PopPosition()
    {
        float y = stalk != null ? stalk.localPosition.y : 1.8f;
        return transform.position + Vector3.up * Mathf.Clamp(y, 1.2f, 3.2f);
    }

    static void Tint(Renderer renderer, Color color)
    {
        if (renderer == null || renderer.sharedMaterial == null)
            return;

        var properties = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(properties);
        properties.SetColor("_BaseColor", color);
        properties.SetColor("_Color", color);
        renderer.SetPropertyBlock(properties);
    }

    static Transform CreateLabel(Transform parent, string message, Vector3 worldPosition)
    {
        var root = new GameObject("DropLabel");
        root.transform.SetParent(parent, true);
        root.transform.position = worldPosition;

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(280f, 64f);
        rect.localScale = new Vector3(0.014f, 0.014f, 0.014f);

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(root.transform, false);
        Text text = textGo.AddComponent<Text>();
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 36;
        text.color = new Color(0.86f, 0.96f, 0.62f);
        text.raycastTarget = false;
        text.text = message;
        InteractionPromptUI.EnsureChineseFont(text);

        Outline outline = textGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);
        return root.transform;
    }

    static AudioClip ChopClip
    {
        get
        {
            if (chopClip == null)
                chopClip = MakeNoiseClip("BambooChop", 0.07f, 240f, 1680f, 78f);
            return chopClip;
        }
    }

    static AudioClip BreakClip
    {
        get
        {
            if (breakClip == null)
                breakClip = MakeNoiseClip("BambooBreak", 0.24f, 80f, 620f, 22f);
            return breakClip;
        }
    }

    static AudioClip MakeNoiseClip(string clipName, float duration, float lowHz, float highHz, float decay)
    {
        const int rate = 44100;
        int samples = Mathf.Max(1, Mathf.RoundToInt(rate * duration));
        float[] data = new float[samples];
        var rng = new System.Random(clipName.GetHashCode());
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)rate;
            float env = Mathf.Exp(-t * decay);
            float noise = (float)rng.NextDouble() * 2f - 1f;
            float low = Mathf.Sin(t * lowHz * Mathf.PI * 2f);
            float high = Mathf.Sin(t * highHz * Mathf.PI * 2f);
            data[i] = (noise * 0.55f + low * 0.34f + high * 0.28f) * env;
        }

        float peak = 0.001f;
        for (int i = 0; i < samples; i++)
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));

        float gain = 0.9f / peak;
        for (int i = 0; i < samples; i++)
            data[i] *= gain;

        AudioClip clip = AudioClip.Create(clipName, samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void OnDrawGizmos()
    {
        Color previous = Gizmos.color;
        Gizmos.color = new Color(0.95f, 0.76f, 0.2f, 0.95f);
        Vector3 basePoint = transform.position + Vector3.up * 0.35f;
        Gizmos.DrawWireSphere(basePoint, 0.42f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 3.4f);
        Gizmos.color = previous;
    }

    void OnDrawGizmosSelected()
    {
        Color previous = Gizmos.color;
        Gizmos.color = new Color(1f, 0.84f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, interactRadius);
        Gizmos.color = previous;
    }
}

/// <summary>
/// 砍击时弹出的「咔」。
/// </summary>
[DisallowMultipleComponent]
public class ChopPop : MonoBehaviour
{
    const float Life = 0.32f;

    float age;
    float punch = 1f;
    Text text;
    Color color = Color.white;
    Vector3 baseScale;

    public static void Spawn(Vector3 position, string message, float punchScale)
    {
        var root = new GameObject("KaPop");
        root.transform.position = position;
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(180f, 90f);
        rect.localScale = Vector3.one * 0.03f;

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(root.transform, false);
        Text label = textGo.AddComponent<Text>();
        RectTransform textRect = label.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        label.alignment = TextAnchor.MiddleCenter;
        label.fontSize = 72;
        label.color = Color.white;
        label.raycastTarget = false;
        label.text = message;
        InteractionPromptUI.EnsureChineseFont(label);

        Outline outline = textGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1.4f, -1.4f);

        ChopPop pop = root.AddComponent<ChopPop>();
        pop.text = label;
        pop.color = label.color;
        pop.punch = Mathf.Max(0.5f, punchScale);
        pop.baseScale = rect.localScale;
    }

    void LateUpdate()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / Life);
        transform.position += Vector3.up * Time.deltaTime * 1.1f;

        Camera cam = Camera.main;
        if (cam != null)
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);

        float scale = Mathf.Lerp(1.35f * punch, 1f, t);
        transform.localScale = baseScale * scale;

        if (text != null)
        {
            color.a = 1f - Mathf.SmoothStep(0.45f, 1f, t);
            text.color = color;
        }

        if (age >= Life)
            Destroy(gameObject);
    }
}
