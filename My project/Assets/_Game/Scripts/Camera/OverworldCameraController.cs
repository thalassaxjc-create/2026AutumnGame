using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class OverworldCameraController : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 9f, -11f);
    [SerializeField] private Vector3 lookOffset = new Vector3(0f, 0.8f, 1.8f);
    [SerializeField, Min(0.1f)] private float smoothing = 7f;

    public void Configure(Transform followTarget)
    {
        target = followTarget;
        SnapToTarget();
    }

    private void Awake()
    {
        SnapToTarget();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 desiredPosition = target.position + followOffset;
        Quaternion desiredRotation = Quaternion.LookRotation(
            target.position + lookOffset - desiredPosition,
            Vector3.up);
        float blend = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);

        transform.position = Vector3.Lerp(transform.position, desiredPosition, blend);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, blend);
    }

    public void SnapToTarget()
    {
        if (target == null)
        {
            return;
        }

        transform.position = target.position + followOffset;
        transform.rotation = Quaternion.LookRotation(
            target.position + lookOffset - transform.position,
            Vector3.up);
    }
}
