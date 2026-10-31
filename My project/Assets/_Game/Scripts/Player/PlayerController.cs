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

    public bool IsGrounded => isGrounded;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();

        InputActionAsset actions = GetComponent<PlayerInput>().actions;
        InputActionMap gameplay = actions.FindActionMap(GameplayMapName, true);
        moveAction = gameplay.FindAction(MoveActionName, true);
        jumpAction = gameplay.FindAction(JumpActionName, true);
    }

    private void Update()
    {
        moveInput = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);

        // Retain a short press until the next physics step.
        bool jumpPressed = jumpAction.WasPressedThisFrame();
        bool spacePressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;

        if (jumpPressed)
        {
            jumpQueued = true;
        }

        // Temporary diagnosis: logs only when Space or the Jump action is pressed.
        if (spacePressed || jumpPressed)
        {
            Debug.Log(
                $"[PlayerController/Input] Space={spacePressed}, Jump={jumpPressed}, " +
                $"ActionEnabled={jumpAction.enabled}, MapEnabled={jumpAction.actionMap.enabled}, " +
                $"Grounded(last physics step)={isGrounded}, Buffered={jumpQueued}",
                this);
        }
    }

    private void FixedUpdate()
    {
        Vector3 velocity = body.linearVelocity;
        isGrounded = CheckGrounded();
        bool hadBufferedJump = jumpQueued;
        bool groundedBeforeJump = isGrounded;
        bool jumpConsumedBefore = jumpConsumed;
        float verticalVelocityBefore = velocity.y;
        bool jumpApplied = false;

        if (!isGrounded && jumpConsumed)
        {
            becameAirborneAfterJump = true;
        }
        else if (isGrounded && becameAirborneAfterJump && velocity.y <= 0f)
        {
            jumpConsumed = false;
            becameAirborneAfterJump = false;
        }

        velocity.x = moveInput.x * movementSpeed;
        velocity.z = moveInput.y * movementSpeed;

        if (jumpQueued && isGrounded && !jumpConsumed)
        {
            velocity.y = jumpVelocity;
            jumpConsumed = true;
            becameAirborneAfterJump = false;
            isGrounded = false;
            jumpApplied = true;
        }

        jumpQueued = false;
        body.linearVelocity = velocity;

        // Temporary diagnosis: proves that the buffered press reached the physics step.
        if (hadBufferedJump)
        {
            Debug.Log(
                $"[PlayerController/Physics] Buffered=True, Grounded={groundedBeforeJump}, " +
                $"ConsumedBefore={jumpConsumedBefore}, JumpApplied={jumpApplied}, " +
                $"VelocityY={verticalVelocityBefore:F2}->{body.linearVelocity.y:F2}",
                this);
        }
    }

    private bool CheckGrounded()
    {
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
