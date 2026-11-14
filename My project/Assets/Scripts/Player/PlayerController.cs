using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 白盒玩家移动。使用项目已启用的 Input System，不读取旧 Input Manager。
/// </summary>
[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class PlayerController : MonoBehaviour
{
    [SerializeField, Min(0f), Tooltip("水平移动速度，单位：米/秒。")]
    float moveSpeed = 5f;

    [SerializeField, Min(0f), Tooltip("转向速度，单位：度/秒。")]
    float turnSpeed = 540f;

    [SerializeField, Tooltip("用于计算水平移动方向的摄像机。留空时使用 Main Camera。")]
    Camera gameplayCamera;

    CharacterController characterController;
    InputAction moveAction;
    float verticalVelocity;
    bool warnedMissingCamera;

    public Camera GameplayCamera => gameplayCamera;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        ResolveCamera();
    }

    void OnEnable()
    {
        CacheMoveAction();
    }

    void Update()
    {
        if (characterController == null)
            return;

        CacheMoveAction();
        ResolveCamera();

        Vector3 move = CameraRelativeDirection(ReadMoveInput());
        if (move.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(move, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                turnSpeed * Time.deltaTime);
        }

        if (characterController.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        else
            verticalVelocity += Physics.gravity.y * Time.deltaTime;

        Vector3 velocity = move * moveSpeed;
        velocity.y = verticalVelocity;
        characterController.Move(velocity * Time.deltaTime);
    }

    void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        turnSpeed = Mathf.Max(0f, turnSpeed);
    }

    void CacheMoveAction()
    {
        if (moveAction != null || InputSystem.actions == null)
            return;

        moveAction = InputSystem.actions.FindAction("Player/Move", false);
        if (moveAction != null && !moveAction.enabled)
            moveAction.Enable();
    }

    void ResolveCamera()
    {
        if (gameplayCamera != null)
            return;

        gameplayCamera = Camera.main;
    }

    Vector2 ReadMoveInput()
    {
        if (moveAction != null)
            return Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return Vector2.zero;

        float x = 0f;
        float y = 0f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            x += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            y -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            y += 1f;

        return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
    }

    Vector3 CameraRelativeDirection(Vector2 input)
    {
        if (input.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        Transform cameraTransform = gameplayCamera != null ? gameplayCamera.transform : null;
        if (cameraTransform == null)
        {
            if (!warnedMissingCamera)
            {
                Debug.LogWarning("PlayerController 未找到摄像机，移动改用世界坐标。", this);
                warnedMissingCamera = true;
            }

            return Vector3.ClampMagnitude(new Vector3(input.x, 0f, input.y), 1f);
        }

        Vector3 forward = cameraTransform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        else
            forward.Normalize();

        Vector3 right = cameraTransform.right;
        right.y = 0f;
        if (right.sqrMagnitude < 0.0001f)
            right = Vector3.right;
        else
            right.Normalize();

        return Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
    }
}
