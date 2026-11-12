using UnityEngine;

/// <summary>
/// 工位交互预留点。仅用于场景标记，不包含按键、触发或小游戏逻辑。
/// </summary>
[DisallowMultipleComponent]
public class InteractionPoint : MonoBehaviour
{
    [SerializeField] string pointId;
    [SerializeField] Color gizmoColor = new Color(1f, 0.82f, 0.2f, 0.95f);

    public string PointId => pointId;

    public void SetPointId(string id)
    {
        pointId = id;
    }

    void OnDrawGizmos()
    {
        Color previous = Gizmos.color;
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, 0.28f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.7f);
        Gizmos.color = previous;
    }
}
