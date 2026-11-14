using UnityEngine;

/// <summary>
/// 门口自动切换。玩家走进触发器即加载目标场景，不需要按键。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class DoorTransition : MonoBehaviour
{
    [SerializeField] string targetSceneName;
    [SerializeField] string targetSpawnId;
    [SerializeField] string promptText = "";

    int playerOverlap;
    static readonly Collider[] OverlapBuffer = new Collider[12];

    public string TargetSceneName => targetSceneName;
    public string TargetSpawnId => targetSpawnId;
    public string PromptText => promptText;
    public bool PlayerIsInside => playerOverlap > 0;

    public void Configure(string sceneName, string spawnId, string prompt)
    {
        targetSceneName = sceneName;
        targetSpawnId = spawnId;
        promptText = prompt ?? string.Empty;
    }

    void OnDisable()
    {
        playerOverlap = 0;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
            return;

        playerOverlap++;
        TryAutoTransition();
    }

    void OnTriggerStay(Collider other)
    {
        if (!IsPlayer(other))
            return;
        if (playerOverlap <= 0)
            playerOverlap = 1;
        TryAutoTransition();
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other))
            return;

        playerOverlap = Mathf.Max(0, playerOverlap - 1);
        if (playerOverlap == 0)
            SceneTransitionState.MustLeaveTrigger = false;
    }

    void TryAutoTransition()
    {
        if (SceneTransitionState.IsLoading)
            return;
        if (SceneTransitionState.MustLeaveTrigger)
            return;
        if (!PlayerIsInside && !PlayerOverlappingNow())
            return;

        SceneTransitionManager.Request(targetSceneName, targetSpawnId);
    }

    bool PlayerOverlappingNow()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null || !box.enabled)
            return false;

        Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, transform.lossyScale);
        int count = Physics.OverlapBoxNonAlloc(
            transform.TransformPoint(box.center),
            halfExtents,
            OverlapBuffer,
            transform.rotation,
            ~0,
            QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            if (IsPlayer(OverlapBuffer[i]))
                return true;
        }

        return false;
    }

    public bool OverlapsPlayer(Transform player)
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null || player == null)
            return false;

        Vector3 point = player.position;
        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null)
            point += controller.center;

        Vector3 closest = box.ClosestPoint(point);
        return (closest - point).sqrMagnitude <= 0.01f;
    }

    static bool IsPlayer(Collider other)
    {
        if (other == null)
            return false;
        if (other.GetComponent<PlayerController>() != null)
            return true;
        if (other.GetComponentInParent<PlayerController>() != null)
            return true;
        return other.CompareTag("Player");
    }

    void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
            return;

        Color previous = Gizmos.color;
        Gizmos.color = new Color(1f, 0.75f, 0.15f, 0.35f);
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
        Gizmos.DrawWireCube(box.center, box.size);
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previous;
    }
}
