using UnityEngine;

/// <summary>On the teaching day, Ailin walks with the player and stays on the ground.</summary>
[DisallowMultipleComponent]
public sealed class AilinCompanion : MonoBehaviour
{
    const float FollowDistance = 1.35f;
    PlayerController player;
    CharacterController body;

    public static void JoinPlayer()
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();
        Vector3 position = player != null
            ? player.transform.position + player.transform.right * .9f
            : Vector3.zero;
        Ensure(position);
    }

    public static void Ensure(Vector3 position)
    {
        if (GameObject.Find("艾琳") != null)
            return;
        var root = new GameObject("艾琳");
        root.transform.position = position;
        CreatePart(root.transform, PrimitiveType.Capsule, "Body", Vector3.zero,
            new Vector3(.64f, .9f, .64f), new Color32(148, 86, 94, 255));
        CreatePart(root.transform, PrimitiveType.Sphere, "Head", new Vector3(0f, .94f, 0f),
            Vector3.one * .55f, new Color32(230, 190, 152, 255));
        var body = root.AddComponent<CharacterController>();
        body.height = 1.7f;
        body.radius = .3f;
        body.center = new Vector3(0f, .85f, 0f);
        body.stepOffset = .3f;
        body.skinWidth = .04f;
        body.minMoveDistance = 0f;
        root.AddComponent<NpcCharacter>().Configure("艾琳");
        root.AddComponent<AilinCompanion>();
        var dialogue = root.AddComponent<WorkshopCustomerDialogue>();
        root.AddComponent<NpcDialogueInteraction>().Configure(dialogue, "按 E 交谈", 2f);
    }

    void Awake()
    {
        body = GetComponent<CharacterController>();
    }

    void OnEnable() => RefreshTalk(WorkshopOrder.Current);

    void LateUpdate()
    {
        WorkshopOrder order = WorkshopOrder.Current;
        RefreshTalk(order);
        if (order == null || !order.LessonPrepared || order.TaughtLesson.HasValue || SceneTransitionState.IsLoading)
            return;
        if (player == null)
            player = FindFirstObjectByType<PlayerController>();
        if (player == null || body == null)
            return;

        Vector3 follow = player.transform.position - player.transform.forward * FollowDistance;
        Vector3 offset = follow - transform.position;
        offset.y = 0f;
        Vector3 motion = offset.sqrMagnitude <= 1.1f * 1.1f
            ? Vector3.zero
            : Vector3.ClampMagnitude(offset, 8f * Time.deltaTime);
        motion.y = body.isGrounded ? -1.5f * Time.deltaTime : -8f * Time.deltaTime;
        body.Move(motion);
        Vector3 facing = new Vector3(motion.x, 0f, motion.z);
        if (facing.sqrMagnitude > .0004f)
            transform.rotation = Quaternion.LookRotation(facing);
    }

    void RefreshTalk(WorkshopOrder order)
    {
        var talk = GetComponent<NpcDialogueInteraction>();
        if (talk == null || order == null)
            return;
        bool introduce = order.CurrentLesson == WorkshopOrder.LessonBeat.None;
        bool debrief = order.CurrentLesson == WorkshopOrder.LessonBeat.Advice;
        talk.enabled = introduce || debrief;
    }

    static void CreatePart(Transform parent, PrimitiveType type, string name, Vector3 localPosition, Vector3 scale, Color color)
    {
        var part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = scale;
        Destroy(part.GetComponent<Collider>());
        var renderer = part.GetComponent<Renderer>();
        renderer.material.color = color;
    }
}
