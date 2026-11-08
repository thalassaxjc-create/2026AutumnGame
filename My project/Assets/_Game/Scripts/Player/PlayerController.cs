using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(PlayerInput))]
public sealed class PlayerController : MonoBehaviour
{
    private const string GameplayMapName = "Gameplay";
    private const string MoveActionName = "Move";
    private const string JumpActionName = "Jump";

    [Header("Movement")]
    [SerializeField, Min(0f)] private float movementSpeed = 6f;
    [SerializeField, Min(0f)] private float jumpVelocity = 7f;
    [SerializeField, Min(0f)] private float turnSpeed = 14f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private Vector3 groundCheckOffset = new(0f, -0.55f, 0f);
    [SerializeField, Min(0.01f)] private float groundCheckRadius = 0.2f;

    [Header("Debug")]
    [SerializeField] private bool drawGroundCheckGizmo = true;
    [SerializeField, Tooltip("Updated at runtime; useful for checking ground detection.")]
    private bool isGrounded;

    private readonly Collider[] groundHits = new Collider[8];

    private Rigidbody body;
    private InputAction moveAction;
    private InputAction jumpAction;
    private Vector2 moveInput;
    private bool jumpQueued;
    private bool jumpConsumed;
    private bool becameAirborneAfterJump;
    private UnityEngine.InputSystem.InputAction interactAction;
    private Vector3[] climbPath;
    private float climbDistance;
    private float climbLength;
    private float climbCooldown;
    [SerializeField, Min(0.1f)] private float climbSpeed = 5f;

    [Header("Rope Swing")]
    [SerializeField, Min(0.1f)] private float swingPumpAcceleration = 1.45f;
    [SerializeField, Range(0.9f, 1f)] private float swingDamping = 0.997f;
    [SerializeField, Min(0f)] private float swingReleaseForwardBoost = 0.7f;
    [SerializeField, Min(0f)] private float swingReleaseUpBoost = 2.8f;
    [SerializeField, Min(1f)] private float swingMaxTangentSpeed = 5.3f;
    [SerializeField, Range(45f, 88f)] private float maxSwingAngle = 72f;

    private ClimbableRope swingSource;
    private Vector3 swingAnchor;
    private Vector3 swingForward;
    private float swingLength;
    private float swingAngle;
    private float swingAngularVelocity;
    private float lastSwingReleaseTime = float.NegativeInfinity;
    private float swingReleaseMomentumUntil = float.NegativeInfinity;
    private Collider playerCollider;
    private bool throwPoseActive;
    private Vector3 throwFacing = Vector3.left;

    public event System.Action InteractRequested;
    public event System.Action<bool> ClimbFinished;
    public event System.Action SwingFinished;
    public System.Func<Vector3, Vector3, Vector3> ConstrainMovement { get; set; }
    public bool IsClimbing => climbPath != null;
    public bool IsSwinging => swingSource != null;
    public bool SwingTraversalActive => IsSwinging || Time.time - lastSwingReleaseTime < 3.5f;
    public float MovementSpeed => movementSpeed;
    public float JumpVelocity => jumpVelocity;
    public Vector3 Position => body != null ? body.position : transform.position;
    public Vector3 Velocity => body != null ? body.linearVelocity : Vector3.zero;
    public bool MovementLocked { get; set; }

    public bool IsGrounded => isGrounded;

    [Header("Parkour Prototype (opt-in)")]
    [SerializeField] private bool parkourEnabled;
    [SerializeField] private float laneChangeDuration = 0.20f;
    [SerializeField] private float inputBuffer = 0.12f;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float slideDuration = 0.65f;
    [SerializeField] private float slideCooldown = 0.15f;
    [SerializeField] private float fastFallVelocity = 12f;
    private PlayerGameplayMode gameplayMode = PlayerGameplayMode.FreePuzzle;
    private int lane = 2;
    private int bufferedLane;
    private float laneInputUntil;
    private float laneStarted = float.NegativeInfinity;
    private float laneStartX;
    private float jumpUntil = float.NegativeInfinity;
    private float groundedAt = float.NegativeInfinity;
    private float slideUntil = float.NegativeInfinity;
    private float slideReadyAt;
    private CapsuleCollider capsule;
    private MovingSurface groundedSurface;
    private float capsuleHeight;
    private Vector3 capsuleCenter;
    public float AutoRunSpeed { get; set; }
    public bool AllowParkourInteraction { get; set; }
    public bool ParkourEnabled => parkourEnabled;
    public bool IsSliding => parkourEnabled && Time.time < slideUntil;
    public int Lane => lane;
    public float LastLaneChangeAt { get; private set; } = float.NegativeInfinity;
    public PlayerGameplayMode GameplayMode => IsSwinging ? PlayerGameplayMode.Swinging :
        IsClimbing ? PlayerGameplayMode.Climbing : throwPoseActive ? PlayerGameplayMode.ThrowPose : gameplayMode;
    public event System.Action<int> LaneChanged;
    public event System.Action JumpStarted;
    public event System.Action SlideStarted;

    public void SetGameplayMode(PlayerGameplayMode mode)
    {
        gameplayMode = mode;
        jumpQueued = false;
        jumpUntil = float.NegativeInfinity;
        bufferedLane = 0;
        if (mode == PlayerGameplayMode.AutoRun)
        {
            lane = Mathf.Clamp(Mathf.RoundToInt(Position.x / 3.6f) + 2, 0, 4);
            laneStartX = Position.x;
            laneStarted = Time.time;
        }
        if (mode != PlayerGameplayMode.AutoRun) EndSlide();
    }

    public void RequestLaneChange(int direction)
    {
        if (!parkourEnabled || GameplayMode != PlayerGameplayMode.AutoRun || MovementLocked) return;
        bufferedLane = direction < 0 ? -1 : 1;
        laneInputUntil = Time.time + inputBuffer;
    }

    public void RequestSlide()
    {
        if (!parkourEnabled || GameplayMode != PlayerGameplayMode.AutoRun || MovementLocked) return;
        if (!isGrounded)
        {
            Vector3 velocity = body.linearVelocity;
            velocity.y = Mathf.Min(velocity.y, -fastFallVelocity);
            body.linearVelocity = velocity;
            return;
        }
        if (Time.time < slideReadyAt || capsule == null) return;
        slideUntil = Time.time + slideDuration;
        slideReadyAt = slideUntil + slideCooldown;
        capsule.height = capsuleHeight * 0.5f;
        capsule.center = capsuleCenter + Vector3.down * capsuleHeight * 0.25f;
        SlideStarted?.Invoke();
    }

    private void EndSlide()
    {
        slideUntil = float.NegativeInfinity;
        if (capsule != null) { capsule.height = capsuleHeight; capsule.center = capsuleCenter; }
    }

    private void ApplyAutoRun(ref Vector3 velocity)
    {
        if (Time.time >= slideUntil) EndSlide();
        if (bufferedLane != 0 && Time.time >= laneStarted + laneChangeDuration)
        {
            if (Time.time <= laneInputUntil)
            {
                int next = Mathf.Clamp(lane + bufferedLane, 0, 4);
                if (next != lane)
                {
                    laneStartX = body.position.x;
                    lane = next;
                    laneStarted = Time.time;
                    LastLaneChangeAt = Time.time;
                    LaneChanged?.Invoke(lane);
                }
            }
            bufferedLane = 0;
        }
        float t = Mathf.Clamp01((Time.time + Time.fixedDeltaTime - laneStarted) / laneChangeDuration);
        float targetX = Mathf.Lerp(laneStartX, (lane - 2) * 3.6f, t * t * (3f - 2f * t));
        velocity.x = MovementLocked ? 0f : (targetX - body.position.x) / Time.fixedDeltaTime;
        velocity.z = MovementLocked ? 0f : AutoRunSpeed;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        playerCollider = GetComponent<Collider>();
        capsule = playerCollider as CapsuleCollider;
        if (capsule != null) { capsuleHeight = capsule.height; capsuleCenter = capsule.center; }
        UnityEngine.InputSystem.InputActionAsset actions = GetComponent<UnityEngine.InputSystem.PlayerInput>().actions;
        var gameplay = actions.FindActionMap(GameplayMapName, true);
        moveAction = gameplay.FindAction(MoveActionName, true);
        jumpAction = gameplay.FindAction(JumpActionName, true);
        interactAction = gameplay.FindAction("Interact", false);
    }

    private void OnEnable()
    {
        moveAction.performed += OnMove;
        moveAction.canceled += OnMove;
        jumpAction.performed += OnJump;
        if (interactAction != null) interactAction.performed += OnInteract;
    }

    private void OnDisable()
    {
        moveAction.performed -= OnMove;
        moveAction.canceled -= OnMove;
        jumpAction.performed -= OnJump;
        if (interactAction != null) interactAction.performed -= OnInteract;
        moveInput = Vector2.zero;
        jumpQueued = false;
    }

    private void OnMove(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        Vector2 previous = moveInput;
        moveInput = Vector2.ClampMagnitude(context.ReadValue<Vector2>(), 1f);
        if (parkourEnabled && GameplayMode == PlayerGameplayMode.AutoRun)
        {
            if (moveInput.x < -0.5f && previous.x >= -0.5f) RequestLaneChange(-1);
            if (moveInput.x > 0.5f && previous.x <= 0.5f) RequestLaneChange(1);
            if (moveInput.y < -0.5f && previous.y >= -0.5f) RequestSlide();
        }
    }

    private void OnJump(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (!IsClimbing && !MovementLocked)
        {
            jumpQueued = true;
            jumpUntil = Time.time + inputBuffer;
        }
    }

    private void OnInteract(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (MovementLocked || (parkourEnabled && GameplayMode == PlayerGameplayMode.AutoRun && !AllowParkourInteraction)) return;
        if (IsSwinging)
        {
            EndSwing(false);
            return;
        }
        if (IsClimbing)
        {
            if (climbDistance < 0.2f) FinishClimb(false);
            else if (climbLength - climbDistance < 0.2f) FinishClimb(true);
            return;
        }
        if (Time.time >= climbCooldown) InteractRequested?.Invoke();
    }

    // Paths are ordered bottom to top. Both endpoints are safe standing positions.
    public bool BeginClimb(Vector3[] path, bool fromTop)
    {
        if (IsClimbing || IsSwinging || MovementLocked || Time.time < climbCooldown || path == null || path.Length < 2) return false;
        climbPath = (Vector3[])path.Clone();
        climbLength = 0f;
        for (int i = 1; i < path.Length; i++) climbLength += Vector3.Distance(path[i - 1], path[i]);
        if (climbLength < 0.01f) { climbPath = null; return false; }
        climbDistance = fromTop ? climbLength : 0f;
        body.linearVelocity = Vector3.zero;
        body.useGravity = false;
        body.isKinematic = true;
        body.position = fromTop ? path[path.Length - 1] : path[0];
        jumpQueued = false;
        return true;
    }

    public void BeginThrowPose(Vector3 facing)
    {
        facing.y = 0f;
        if (facing.sqrMagnitude < 0.001f) facing = Vector3.left;
        throwFacing = facing.normalized;
        throwPoseActive = true;
        Vector3 velocity = body.linearVelocity;
        velocity.x = 0f;
        velocity.z = 0f;
        body.linearVelocity = velocity;
        Quaternion rotation = Quaternion.LookRotation(throwFacing, Vector3.up);
        body.rotation = rotation;
        transform.rotation = rotation;
    }

    public void EndThrowPose()
    {
        throwPoseActive = false;
    }

    public bool BeginSwing(
        ClimbableRope source,
        Vector3 anchor,
        Vector3 forward,
        float length,
        float startAngle,
        float startAngularVelocity)
    {
        if (source == null || IsClimbing || IsSwinging || MovementLocked ||
            Time.time < climbCooldown || length < 1f)
        {
            return false;
        }

        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        swingSource = source;
        swingAnchor = anchor;
        swingForward = forward.normalized;
        swingLength = Mathf.Clamp(length, 2.5f, 8f);

        swingAngle = Mathf.Clamp(
            startAngle,
            -maxSwingAngle * Mathf.Deg2Rad,
            maxSwingAngle * Mathf.Deg2Rad);
        swingAngularVelocity = Mathf.Abs(startAngularVelocity) > 0.01f
            ? startAngularVelocity
            : 0.05f;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.useGravity = false;
        body.isKinematic = true;
        body.position = source.DriveSwingPlatform(
            swingAngle,
            swingAnchor,
            swingForward,
            swingLength);
        body.rotation = Quaternion.LookRotation(swingForward, Vector3.up);
        isGrounded = false;
        jumpQueued = false;
        return true;
    }

    private void EndSwing(bool release)
    {
        if (!IsSwinging) return;

        ClimbableRope finishedSource = swingSource;
        Vector3 tangent = swingForward * Mathf.Cos(swingAngle) + Vector3.up * Mathf.Sin(swingAngle);
        Vector3 velocity = Vector3.ClampMagnitude(
            tangent * (swingAngularVelocity * swingLength),
            swingMaxTangentSpeed);
        if (release)
        {
            velocity += swingForward * swingReleaseForwardBoost + Vector3.up * swingReleaseUpBoost;
            lastSwingReleaseTime = Time.time;
            swingReleaseMomentumUntil = Time.time + 1.25f;
            jumpConsumed = true;
            becameAirborneAfterJump = true;
        }

        if (finishedSource != null)
        {
            finishedSource.ReleaseRider(
                swingAngle,
                swingAngularVelocity,
                swingAnchor,
                swingForward,
                swingLength,
                playerCollider);
        }

        swingSource = null;
        if (parkourEnabled) gameplayMode = release ? PlayerGameplayMode.AirborneFromSwing : PlayerGameplayMode.FreePuzzle;
        body.isKinematic = false;
        body.useGravity = true;
        body.linearVelocity = release ? velocity : Vector3.zero;
        body.angularVelocity = Vector3.zero;
        jumpQueued = false;
        moveInput = Vector2.zero;
        climbCooldown = Time.time + 0.25f;
        SwingFinished?.Invoke();
    }

    private Vector3 EvaluateClimb(float distance)
    {
        for (int i = 1; i < climbPath.Length; i++)
        {
            float segment = Vector3.Distance(climbPath[i - 1], climbPath[i]);
            if (distance <= segment) return Vector3.Lerp(climbPath[i - 1], climbPath[i], segment > 0f ? distance / segment : 0f);
            distance -= segment;
        }
        return climbPath[climbPath.Length - 1];
    }

    private void FinishClimb(bool atTop)
    {
        body.position = atTop ? climbPath[climbPath.Length - 1] : climbPath[0];
        climbPath = null;
        body.isKinematic = false;
        body.useGravity = true;
        body.linearVelocity = Vector3.zero;
        jumpConsumed = false;
        becameAirborneAfterJump = false;
        swingReleaseMomentumUntil = float.NegativeInfinity;
        moveInput = Vector2.zero;
        jumpQueued = false;
        climbCooldown = Time.time + 0.3f;
        ClimbFinished?.Invoke(atTop);
    }

    public void Respawn(Vector3 position)
    {
        if (IsSwinging)
        {
            swingSource.CancelRiderAndRestore();
            swingSource = null;
        }
        climbPath = null;
        body.isKinematic = false;
        body.useGravity = true;
        body.position = position;
        body.linearVelocity = Vector3.zero;
        jumpQueued = false;
        jumpConsumed = false;
        becameAirborneAfterJump = false;
        swingReleaseMomentumUntil = float.NegativeInfinity;
        throwPoseActive = false;
        EndSlide();
        jumpUntil = float.NegativeInfinity;
        groundedAt = float.NegativeInfinity;
        bufferedLane = 0;
    }

    private void FixedUpdate()
    {
        if (IsSwinging)
        {
            float gravity = Mathf.Abs(Physics.gravity.y);
            float pumpInput = moveInput.y;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) pumpInput = 1f;
                else if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) pumpInput = -1f;
            }

            float acceleration = -(gravity / swingLength) * Mathf.Sin(swingAngle);
            acceleration += pumpInput * swingPumpAcceleration;
            swingAngularVelocity += acceleration * Time.fixedDeltaTime;
            swingAngularVelocity *= swingDamping;
            swingAngularVelocity = Mathf.Clamp(swingAngularVelocity, -3.2f, 3.2f);
            swingAngle += swingAngularVelocity * Time.fixedDeltaTime;

            float limit = maxSwingAngle * Mathf.Deg2Rad;
            if (Mathf.Abs(swingAngle) > limit)
            {
                swingAngle = Mathf.Clamp(swingAngle, -limit, limit);
                swingAngularVelocity *= -0.25f;
            }

            Vector3 position = swingSource.DriveSwingPlatform(
                swingAngle,
                swingAnchor,
                swingForward,
                swingLength);
            body.MovePosition(position);
            body.MoveRotation(Quaternion.LookRotation(swingForward, Vector3.up));
            isGrounded = false;

            if (jumpQueued)
            {
                EndSwing(true);
            }
            jumpQueued = false;
            return;
        }

        if (IsClimbing)
        {
            float input = MovementLocked ? 0f : moveInput.y;
            climbDistance = Mathf.Clamp(climbDistance + input * climbSpeed * Time.fixedDeltaTime, 0f, climbLength);
            if (input > 0f && climbDistance >= climbLength) FinishClimb(true);
            else if (input < 0f && climbDistance <= 0f) FinishClimb(false);
            else body.MovePosition(EvaluateClimb(climbDistance));
            return;
        }
        Vector3 velocity = body.linearVelocity;
        isGrounded = CheckGrounded();
        if (isGrounded) groundedAt = Time.time;
        if (!isGrounded && jumpConsumed) becameAirborneAfterJump = true;
        else if (isGrounded && becameAirborneAfterJump && velocity.y <= 0f)
        {
            jumpConsumed = false;
            becameAirborneAfterJump = false;
        }
        bool preserveSwingMomentum = Time.time < swingReleaseMomentumUntil;
        if (parkourEnabled && GameplayMode == PlayerGameplayMode.AutoRun)
        {
            ApplyAutoRun(ref velocity);
        }
        else if (!preserveSwingMomentum)
        {
            bool locked = MovementLocked || gameplayMode == PlayerGameplayMode.Stunned ||
                gameplayMode == PlayerGameplayMode.Respawning || gameplayMode == PlayerGameplayMode.Finished;
            velocity.x = locked ? 0f : moveInput.x * movementSpeed;
            velocity.z = locked ? 0f : moveInput.y * movementSpeed;
        }
        if (parkourEnabled && isGrounded && groundedSurface != null && !jumpConsumed)
        {
            velocity.y = groundedSurface.Velocity.y;
            velocity.x += groundedSurface.Velocity.x;
            velocity.z += groundedSurface.Velocity.z;
        }
        Vector3 planarVelocity = new Vector3(velocity.x, 0f, velocity.z);
        if (throwPoseActive)
        {
            body.MoveRotation(Quaternion.LookRotation(throwFacing, Vector3.up));
        }
        else if (!MovementLocked && planarVelocity.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(planarVelocity.normalized, Vector3.up);
            body.MoveRotation(Quaternion.Slerp(
                body.rotation,
                targetRotation,
                1f - Mathf.Exp(-turnSpeed * Time.fixedDeltaTime)));
        }
        bool requestedJump = parkourEnabled ? Time.time <= jumpUntil : jumpQueued;
        bool canJump = isGrounded || (parkourEnabled && Time.time - groundedAt <= coyoteTime);
        if (requestedJump && canJump && !jumpConsumed && !MovementLocked)
        {
            EndSlide();
            velocity.y = jumpVelocity;
            jumpConsumed = true;
            becameAirborneAfterJump = false;
            isGrounded = false;
            jumpUntil = float.NegativeInfinity;
            JumpStarted?.Invoke();
        }
        jumpQueued = false;
        if (ConstrainMovement != null)
        {
            Vector3 intended = body.position + new Vector3(velocity.x, 0f, velocity.z) * Time.fixedDeltaTime;
            Vector3 allowed = ConstrainMovement(body.position, intended);
            velocity.x = (allowed.x - body.position.x) / Time.fixedDeltaTime;
            velocity.z = (allowed.z - body.position.z) / Time.fixedDeltaTime;
        }
        body.linearVelocity = velocity;
    }

private bool CheckGrounded()
    {
        groundedSurface = null;
        Vector3 center = body.position + groundCheckOffset;
        int hitCount = Physics.OverlapSphereNonAlloc(
            center,
            groundCheckRadius,
            groundHits,
            groundLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = groundHits[i];

            // Ignore this Rigidbody and any colliders under the player hierarchy.
            if (hit == null ||
                hit.attachedRigidbody == body ||
                hit.transform == transform ||
                hit.transform.IsChildOf(transform))
            {
                continue;
            }

            MovingSurface.TryGet(hit.attachedRigidbody, out groundedSurface);
            return true;
        }

        return false;
    }

    private void OnValidate()
    {
        movementSpeed = Mathf.Max(0f, movementSpeed);
        jumpVelocity = Mathf.Max(0f, jumpVelocity);
        groundCheckRadius = Mathf.Max(0.01f, groundCheckRadius);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGroundCheckGizmo)
        {
            return;
        }

        Vector3 origin = Application.isPlaying && body != null ? body.position : transform.position;
        Gizmos.color = Application.isPlaying
            ? (isGrounded ? Color.green : Color.red)
            : Color.yellow;
        Gizmos.DrawWireSphere(origin + groundCheckOffset, groundCheckRadius);
    }
}
