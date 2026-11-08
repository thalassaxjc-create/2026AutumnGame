using UnityEngine;

public sealed class ClimbableRope : MonoBehaviour
{
    [SerializeField] private Vector3[] path = new Vector3[0];
    [SerializeField, Min(0.2f)] private float interactionRadius = 1.25f;
    [SerializeField] private bool available = true;
    [SerializeField] private bool swingAvailable;
    [SerializeField] private Vector3 swingLandingTarget;

    [Header("Suspended plank swing")]
    [SerializeField] private Transform swingPlatform;
    [SerializeField] private RopeVisual swingRope;
    [SerializeField, Range(0.9f, 1f)] private float freeSwingDamping = 0.995f;

    private Transform swingBinding;
    private Vector3 platformStandingOffset;
    private Vector3 restPlatformPosition;
    private Quaternion restPlatformRotation;
    private bool restPoseCaptured;
    private bool riderAttached;
    private bool residualSwing;
    private float residualAngle;
    private float residualAngularVelocity;
    private float residualLength;
    private Vector3 residualAnchor;
    private Vector3 residualForward;
    private Collider ignoredPlayerCollider;
    private Collider platformCollider;
    private float restoreCollisionAt = float.NegativeInfinity;

    public Vector3 Bottom => path[0];
    public Vector3 Top => path[path.Length - 1];
    public bool Available => available && path != null && path.Length >= 2;
    public bool SwingAvailable => Available && swingAvailable && swingPlatform != null;
    public Transform SwingPlatform => swingPlatform;
    public Vector3 SwingStandingPoint => swingPlatform != null
        ? swingPlatform.position + platformStandingOffset
        : Bottom;

    private void Awake()
    {
        CaptureRestPose(true);
    }

    private void OnEnable()
    {
        CaptureRestPose(!Application.isPlaying);
    }

    public void Configure(Vector3[] safeStandingPath, bool enabledInitially)
    {
        path = safeStandingPath;
        available = enabledInitially;
        CaptureRestPose(true);
    }

    public void ConfigureSwingPlatform(Transform platform, RopeVisual visual)
    {
        swingPlatform = platform;
        swingRope = visual;
        swingBinding = swingPlatform != null ? swingPlatform.Find("RopeBinding") : null;
        platformCollider = swingPlatform != null ? swingPlatform.GetComponent<Collider>() : null;
        CaptureRestPose(true);
    }

    public void SetAvailable(bool value) { available = value; }

    public void SetSwingTarget(Vector3 landingTarget, bool enabled)
    {
        swingLandingTarget = landingTarget;
        swingAvailable = enabled;
        if (!enabled)
        {
            riderAttached = false;
            residualSwing = false;
            RestoreRestPose();
        }
    }

    public float InteractionDistance(Vector3 position)
    {
        if (!Available) return float.PositiveInfinity;
        Vector3 bottom = SwingAvailable ? SwingStandingPoint : Bottom;
        float d = Mathf.Min(Vector3.Distance(position, bottom), Vector3.Distance(position, Top));
        return d <= interactionRadius ? d : float.PositiveInfinity;
    }

    public bool TryClimb(PlayerController player)
    {
        if (player == null || float.IsPositiveInfinity(InteractionDistance(player.Position))) return false;

        Vector3 dynamicBottom = SwingAvailable ? SwingStandingPoint : Bottom;
        bool fromTop = Vector3.Distance(player.Position, Top) <
                       Vector3.Distance(player.Position, dynamicBottom);
        if (!player.IsGrounded) return false;
        if (!fromTop && Mathf.Abs(player.Position.y - dynamicBottom.y) > .2f) return false;

        if (!fromTop && SwingAvailable)
        {
            Vector3 anchor = FindSwingAnchor();
            Vector3 forward = swingLandingTarget - dynamicBottom;
            forward.y = 0f;
            float length = Vector3.Distance(anchor, CurrentTiePoint());
            float startAngle = residualSwing ? residualAngle : 0f;
            float startVelocity = residualSwing ? residualAngularVelocity : 0f;
            bool started = player.BeginSwing(
                this,
                anchor,
                forward,
                length,
                startAngle,
                startVelocity);
            if (started)
            {
                residualSwing = false;
                riderAttached = true;
            }
            return started;
        }

        return player.BeginClimb(path, fromTop);
    }

    public Vector3 DriveSwingPlatform(
        float angle,
        Vector3 anchor,
        Vector3 forward,
        float length)
    {
        if (swingPlatform == null) return anchor + Vector3.down * length;

        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 oldTieOffset = CurrentTiePoint() - swingPlatform.position;
        Vector3 tie = anchor +
            forward * (Mathf.Sin(angle) * length) +
            Vector3.down * (Mathf.Cos(angle) * length);
        swingPlatform.SetPositionAndRotation(tie - oldTieOffset, restPlatformRotation);
        UpdateSwingRope(tie, anchor);
        return SwingStandingPoint;
    }

    public void ReleaseRider(
        float angle,
        float angularVelocity,
        Vector3 anchor,
        Vector3 forward,
        float length,
        Collider playerCollider)
    {
        riderAttached = false;
        residualSwing = true;
        residualAngle = angle;
        residualAngularVelocity = angularVelocity;
        residualAnchor = anchor;
        residualForward = forward;
        residualLength = length;

        if (playerCollider != null && platformCollider != null)
        {
            Physics.IgnoreCollision(playerCollider, platformCollider, true);
            ignoredPlayerCollider = playerCollider;
            restoreCollisionAt = Time.time + 0.45f;
        }
    }

    public void CancelRiderAndRestore()
    {
        riderAttached = false;
        residualSwing = false;
        if (ignoredPlayerCollider != null && platformCollider != null)
            Physics.IgnoreCollision(ignoredPlayerCollider, platformCollider, false);
        ignoredPlayerCollider = null;
        RestoreRestPose();
    }

    private void FixedUpdate()
    {
        if (ignoredPlayerCollider != null && Time.time >= restoreCollisionAt)
        {
            if (platformCollider != null)
                Physics.IgnoreCollision(ignoredPlayerCollider, platformCollider, false);
            ignoredPlayerCollider = null;
        }

        if (!residualSwing || riderAttached || residualLength < 0.1f) return;

        float acceleration =
            -(Mathf.Abs(Physics.gravity.y) / residualLength) * Mathf.Sin(residualAngle);
        residualAngularVelocity += acceleration * Time.fixedDeltaTime;
        residualAngularVelocity *= freeSwingDamping;
        residualAngle += residualAngularVelocity * Time.fixedDeltaTime;
        DriveSwingPlatform(
            residualAngle,
            residualAnchor,
            residualForward,
            residualLength);

        if (Mathf.Abs(residualAngle) < 0.008f &&
            Mathf.Abs(residualAngularVelocity) < 0.018f)
        {
            residualSwing = false;
            RestoreRestPose();
        }
    }

    private Vector3 FindSwingAnchor()
    {
        Vector3 anchor = Top;
        float bestHorizontal = float.PositiveInfinity;
        for (int i = 1; i < path.Length; i++)
        {
            Vector2 delta = new Vector2(
                path[i].x - Bottom.x,
                path[i].z - Bottom.z);
            if (delta.sqrMagnitude < bestHorizontal &&
                path[i].y > Bottom.y + 1f)
            {
                bestHorizontal = delta.sqrMagnitude;
                anchor = path[i];
            }
        }

        return anchor;
    }

    private void AutoBindSwingPlatform()
    {
        if (swingRope == null) swingRope = GetComponent<RopeVisual>();
        if (swingPlatform == null && transform.parent != null && name.Contains("Descent"))
        {
            foreach (Transform child in transform.parent)
            {
                if (child.name.StartsWith("LeftDescentBlock"))
                {
                    swingPlatform = child;
                    break;
                }
            }
        }

        if (swingPlatform != null)
        {
            swingBinding = swingPlatform.Find("RopeBinding");
            platformCollider = swingPlatform.GetComponent<Collider>();
        }
    }

    private void CaptureRestPose(bool force)
    {
        AutoBindSwingPlatform();
        if (swingPlatform == null || path == null || path.Length < 2) return;
        if (restPoseCaptured && !force) return;

        restPlatformPosition = swingPlatform.position;
        restPlatformRotation = swingPlatform.rotation;
        platformStandingOffset = Bottom - restPlatformPosition;
        restPoseCaptured = true;
    }

    private Vector3 CurrentTiePoint()
    {
        if (swingBinding != null) return swingBinding.position;
        return swingPlatform != null
            ? swingPlatform.position + Vector3.up * 0.73f
            : Bottom + Vector3.down * 0.57f;
    }

    private void UpdateSwingRope(Vector3 tie, Vector3 anchor)
    {
        if (swingRope == null) return;
        if (path != null && path.Length > 2)
            swingRope.SetPoints(new[] { tie, anchor, path[path.Length - 1] });
        else
            swingRope.SetPoints(new[] { tie, anchor });
    }

    private void RestoreRestPose()
    {
        if (!restPoseCaptured || swingPlatform == null) return;
        swingPlatform.SetPositionAndRotation(restPlatformPosition, restPlatformRotation);
        UpdateSwingRope(CurrentTiePoint(), FindSwingAnchor());
    }

    private void OnDrawGizmosSelected()
    {
        if (path == null || path.Length < 2) return;
        Gizmos.color = Color.cyan;
        for (int i = 1; i < path.Length; i++) Gizmos.DrawLine(path[i - 1], path[i]);
        Gizmos.DrawWireSphere(Top, interactionRadius);
        Gizmos.DrawWireSphere(SwingAvailable ? SwingStandingPoint : Bottom, interactionRadius);
        if (swingAvailable)
        {
            Gizmos.color = new Color(1f, 0.75f, 0.15f);
            Gizmos.DrawLine(SwingAvailable ? SwingStandingPoint : Bottom, swingLandingTarget);
            Gizmos.DrawWireSphere(swingLandingTarget, 0.35f);
        }
    }
}
