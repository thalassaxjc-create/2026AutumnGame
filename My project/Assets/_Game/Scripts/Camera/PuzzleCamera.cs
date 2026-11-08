using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class PuzzleCamera : MonoBehaviour
{
    [SerializeField] private FivePillarLevel level;
    [Header("Third-person follow")]
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 3f, -6f);
    [SerializeField] private Vector3 lookOffset = new Vector3(0f, .6f, 1.5f);
    [SerializeField, Range(35f, 90f)] private float fieldOfView = 65f;
    [SerializeField, Min(.1f)] private float smoothing = 10f;
    [SerializeField, Min(.05f)] private float throwViewBlendTime = .45f;
    private Camera view;
    private bool throwViewRequested;
    private float throwViewWeight;
    private float swingViewWeight;
    private Vector3 throwLookDirection = Vector3.left;
    [SerializeField] private ParkourDirector parkour;
    private float sideViewUntil;
    private bool wasSwinging;
    private float shakeUntil;
    private float shakeStrength;
    public void PulseShake(float strength = .035f, float duration = .18f)
    { shakeStrength = Mathf.Clamp(strength, 0f, .06f); shakeUntil = Time.unscaledTime + duration; }
    public void ConfigureParkour(ParkourDirector director) { parkour = director; }

    public void Configure(FivePillarLevel puzzle) { level = puzzle; }

    public void BeginThrowLook(Vector3 acrossDirection)
    {
        acrossDirection.y = 0f;
        if (acrossDirection.sqrMagnitude < .001f) acrossDirection = Vector3.left;
        throwLookDirection = acrossDirection.normalized;
        throwViewRequested = true;
    }

    public void EndThrowLook()
    {
        throwViewRequested = false;
    }
    private void Awake()
    {
        view = GetComponent<Camera>();
        view.orthographic = false;
        view.fieldOfView = fieldOfView;
        view.nearClipPlane = .1f;
        SnapToPlayer();
    }
    public void SnapToPlayer()
    {
        if (level == null || level.Player == null) return;
        Vector3 position = level.Player.Position;
        transform.position = position + followOffset;
        transform.rotation = Quaternion.LookRotation(position + lookOffset - transform.position);
    }
    private void LateUpdate()
    {
        if (level == null) return;
        // Stay with the controlled capsule, including climbing, jumping and pulling.
        // Do not switch to an overhead overview during puzzle animations.
        Vector3 position = level.Player.Position;
        float viewStep = Time.unscaledDeltaTime / Mathf.Max(.05f, throwViewBlendTime);
        throwViewWeight = Mathf.MoveTowards(
            throwViewWeight,
            throwViewRequested ? 1f : 0f,
            viewStep);
        bool finale = parkour != null && position.z >= 140f && position.z <= 170f && level.Player.GameplayMode == PlayerGameplayMode.AutoRun;
        if (parkour != null)
        {
            if (wasSwinging && !level.Player.IsSwinging) sideViewUntil = Time.time + 1.5f;
            wasSwinging = level.Player.IsSwinging;
        }
        bool sideRequested = parkour != null ? level.Player.IsSwinging || Time.time < sideViewUntil : level.Player.SwingTraversalActive;
        swingViewWeight = Mathf.MoveTowards(
            swingViewWeight,
            sideRequested ? 1f : 0f,
            viewStep);

        Vector3 followPosition = position + (finale ? new Vector3(0f, 2.6f, -10f) : followOffset);
        Quaternion followRotation = Quaternion.LookRotation(position + lookOffset - followPosition);
        view.fieldOfView = Mathf.Lerp(view.fieldOfView, finale ? 72f : fieldOfView, 1f - Mathf.Exp(-2f * Time.unscaledDeltaTime));

        Vector3 swingPosition = position + Vector3.left * 7.2f +
                                Vector3.up * 2.7f +
                                Vector3.back * 1.4f;
        Vector3 swingTarget = position + Vector3.forward * 3f + Vector3.up * .2f;
        Quaternion swingRotation = Quaternion.LookRotation(swingTarget - swingPosition);
        Vector3 basePosition = Vector3.Lerp(followPosition, swingPosition, swingViewWeight);
        Quaternion baseRotation = Quaternion.Slerp(followRotation, swingRotation, swingViewWeight);

        Vector3 cinematicPosition = position - throwLookDirection * 6.2f +
                                    Vector3.up * 3.15f +
                                    Vector3.back * 1.6f;
        Vector3 cinematicTarget = position + throwLookDirection * 2.3f + Vector3.up * .45f;
        Quaternion cinematicRotation = Quaternion.LookRotation(cinematicTarget - cinematicPosition);

        Vector3 targetPosition = Vector3.Lerp(basePosition, cinematicPosition, throwViewWeight);
        Quaternion targetRotation = Quaternion.Slerp(baseRotation, cinematicRotation, throwViewWeight);
        float blend = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
        Vector3 cameraPosition = Vector3.Lerp(transform.position, targetPosition, blend);
        if (parkour != null && swingViewWeight < .01f && throwViewWeight < .01f)
            cameraPosition.x = Mathf.Lerp(transform.position.x, targetPosition.x, 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime));
        if (parkour != null && Time.unscaledTime < shakeUntil)
            cameraPosition += new Vector3(Mathf.Sin(Time.unscaledTime*73f),Mathf.Sin(Time.unscaledTime*89f),0f)*shakeStrength;
        transform.position = cameraPosition;
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, blend);
    }
}
