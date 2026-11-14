using UnityEngine;

/// <summary>
/// 固定斜俯视跟随。镜头锁死不转、不晃，角色大致在画面中下，世界跟着平移。
/// 略微仰起并朝前看，避免正俯视。
/// </summary>
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class CameraFollow : MonoBehaviour
{
    [SerializeField, Tooltip("跟随目标，通常是玩家。")]
    Transform target;

    [SerializeField, Min(0.5f), Tooltip("摄像机在水平面上落在目标后方的距离。")]
    float horizontalDistance = 22f;

    [SerializeField, Min(0.5f), Tooltip("摄像机相对目标的高度。")]
    float height = 19.7f;

    [SerializeField, Min(0.01f), Tooltip("保留字段。跟随已锁死，不再用它拖尾。")]
    float smoothTime = 0.01f;

    [SerializeField, Tooltip("注视点相对目标原点的高度。")]
    float lookAtHeight = 1.2f;

    [SerializeField, Min(0f), Tooltip("注视点沿世界前方再偏出的距离，让画面多看前方。")]
    float lookAhead = 4.4f;

    [SerializeField, Range(20f, 70f), Tooltip("透视视野。")]
    float fieldOfView = 44f;

    [SerializeField, Min(4f), Tooltip("已不再使用正交。")]
    float orthographicSize = 13f;

    bool warnedMissingTarget;

    public Transform Target => target;

    public void ApplyGameplayView(Transform followTarget)
    {
        target = followTarget;
        horizontalDistance = 22f;
        height = 19.7f;
        smoothTime = 0.01f;
        lookAtHeight = 1.2f;
        lookAhead = 4.4f;
        fieldOfView = 44f;
        ApplyLens();
        SnapToTarget();
    }

    public void ApplyStardewView(Transform followTarget)
    {
        ApplyGameplayView(followTarget);
    }

    void Awake()
    {
        ApplyLens();
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

        transform.SetPositionAndRotation(LockedPosition(target.position), LockedRotation(target.position));
    }

    public void SnapToTarget()
    {
        if (target == null)
            return;

        ApplyLens();
        transform.SetPositionAndRotation(LockedPosition(target.position), LockedRotation(target.position));
    }

    void OnValidate()
    {
        horizontalDistance = Mathf.Max(0.5f, horizontalDistance);
        height = Mathf.Max(0.5f, height);
        smoothTime = Mathf.Max(0.01f, smoothTime);
        lookAtHeight = Mathf.Clamp(lookAtHeight, -1f, 4f);
        lookAhead = Mathf.Max(0f, lookAhead);
        fieldOfView = Mathf.Clamp(fieldOfView, 20f, 70f);
        ApplyLens();
    }

    Vector3 LockedPosition(Vector3 focus)
    {
        return focus + new Vector3(0f, height, -horizontalDistance);
    }

    Quaternion LockedRotation(Vector3 focus)
    {
        Vector3 lookAt = focus + new Vector3(0f, lookAtHeight, lookAhead);
        Vector3 towardFocus = lookAt - LockedPosition(focus);
        if (towardFocus.sqrMagnitude < 0.0001f)
            towardFocus = Vector3.forward;
        return Quaternion.LookRotation(towardFocus, Vector3.up);
    }

    void ApplyLens()
    {
        Camera cameraComponent = GetComponent<Camera>();
        if (cameraComponent == null)
            return;

        cameraComponent.orthographic = false;
        cameraComponent.fieldOfView = fieldOfView;
        cameraComponent.nearClipPlane = 0.2f;
        cameraComponent.farClipPlane = 600f;
    }
}
