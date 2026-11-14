using UnityEngine;

/// <summary>
/// 场景出生点。切换完成后按 SpawnId 查找，不依赖硬编码坐标。
/// </summary>
[DisallowMultipleComponent]
public class SceneSpawnPoint : MonoBehaviour
{
    [SerializeField] string spawnId;

    public string SpawnId => spawnId;

    public void SetSpawnId(string id)
    {
        spawnId = id;
    }

    void OnDrawGizmos()
    {
        Color previous = Gizmos.color;
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.95f);
        Gizmos.DrawWireSphere(transform.position, 0.35f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.1f);
        Gizmos.color = previous;
    }
}
