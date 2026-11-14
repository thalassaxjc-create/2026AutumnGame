using UnityEngine;

/// <summary>
/// 固定斜俯视跟随。偏移在世界空间中保持不变，不随玩家旋转，也不响应鼠标。
/// </summary>
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class CameraFollow : MonoBehaviour
{
    [SerializeField, Tooltip("跟随目标，通常是玩家。")]
    Transform target;

    [SerializeField, Min(0.5f), Tooltip("摄像机在水平面上与目标的距离。与高度相等时俯视角约为 45 度。")]
    float horizontalDistance = 12f;

    [SerializeField, Min(0.5f), Tooltip("摄像机相对目标的高度。")]
    float height = 12f;

    [SerializeField, Min(0.01f), Tooltip("位置跟随的平滑时间，越小跟得越紧。")]
    float smoothTime = 0.15f;

    [SerializeField, Tooltip("注视点相对目标原点的高度。玩家胶囊中心在原点时保持 0。")]
    float lookAtHeight;

    Vector3 velocity;
    bool warnedMissingTarget;

    public Transform Target => target;

    void Awake()
    {
        Camera cameraComponent = GetComponent<Camera>();
        cameraComponent.orthographic = false;
        SnapToTarget();
    }

    void LateUpdate()
    {
        if (target == null)
        {
            if (!warnedMissingTarget)
            {
                Debug.LogWarning("CameraFollow 没有跟随目标。", this);
                warnedMissingTarget = true;
            }

            return;
        }

        Vector3 desiredPosition = target.position + new Vector3(0f, height, -horizontalDistance);
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime);
        transform.LookAt(target.position + Vector3.up * lookAtHeight);
    }

    public void SnapToTarget()
    {
        if (target == null)
            return;

        transform.position = target.position + new Vector3(0f, height, -horizontalDistance);
        transform.LookAt(target.position + Vector3.up * lookAtHeight);
        velocity = Vector3.zero;
    }

    void OnValidate()
    {
        horizontalDistance = Mathf.Max(0.5f, horizontalDistance);
        height = Mathf.Max(0.5f, height);
        smoothTime = Mathf.Max(0.01f, smoothTime);

        Camera cameraComponent = GetComponent<Camera>();
        if (cameraComponent != null)
            cameraComponent.orthographic = false;
    }
}
