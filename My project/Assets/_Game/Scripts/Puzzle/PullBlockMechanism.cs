using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PullBlockMechanism : MonoBehaviour
{
    public const float PlatformGapHalf = 4.5f;
    public enum PullState
    {
        Ready,
        Charging,
        Throwing,
        Failing,
        Transferred,
        Failed
    }

    [SerializeField] private Transform block;
    [SerializeField] private RopeVisual rope;
    [SerializeField] private ClimbableRope ascent;
    [SerializeField] private ClimbableRope descent;
    [SerializeField] private GameObject barrier;
    [SerializeField] private float sectionZ;

    [Header("Timing throw")]
    [SerializeField, Min(0.4f)] private float throwDuration = 2.1f;
    [SerializeField, Min(0.2f)] private float interactionRadius = 1.35f;
    [SerializeField, Range(0.1f, 0.9f)] private float greenStart = 0.42f;
    [SerializeField, Range(0.1f, 0.9f)] private float greenEnd = 0.58f;
    [SerializeField, Min(0.1f)] private float timingSpeed = 0.72f;
    [SerializeField, Min(0.4f)] private float failedFallDuration = 1.35f;
    [SerializeField, Min(0f)] private float poseBlendDuration = 0.45f;
    [SerializeField, Min(1.35f)] private float parkourHeldOffset = 2.1f;
    [SerializeField] private Vector3 pullPoint;

    private PullState state;
    private float elapsed;
    private float timingPointer;
    private PlayerController activePlayer;
    private Collider[] blockColliders;
    private Vector3 throwStart;
    private Quaternion throwStartRotation;
    private PuzzleCamera puzzleCamera;
    private readonly Vector3[] movingRopePoints = new Vector3[3];

    public System.Func<bool> ValidatePull { get; set; }
    public event System.Action<PullBlockMechanism> Transferred;
    public event System.Action<PullBlockMechanism, bool> Resolved;
    public event System.Action<PullBlockMechanism> ThrowStarted;
    public event System.Action<PullBlockMechanism, bool> ThrowResolved
    {
        add { Resolved += value; }
        remove { Resolved -= value; }
    }

    public PullState State => state;
    public float SectionZ => sectionZ;
    public Vector3 PullPoint => pullPoint;
    public Transform Block => block;
    public ClimbableRope Descent => descent;
    public ClimbableRope Ascent => ascent;
    public Vector3 FinalPosition => new Vector3(-10.5f, -6.3f, sectionZ + PlatformGapHalf);
    public float TimingPointer => timingPointer;
    public float GreenStart => greenStart;
    public float GreenEnd => greenEnd;
    public bool IsResolved => state == PullState.Transferred || state == PullState.Failed;
    public bool IsBusy => state == PullState.Charging || state == PullState.Throwing || state == PullState.Failing;
    public bool BlocksPath => state == PullState.Transferred;

    private Vector3 RopeAnchor => new Vector3(8.4f, 0.8f, sectionZ);
    private Vector3 OuterGuide => new Vector3(10.5f, 0.8f, sectionZ);
    private Vector3 BlockTie => block.position + Vector3.up * 0.78f;

    public void Configure(
        Transform plank,
        RopeVisual visual,
        ClimbableRope down,
        ClimbableRope up,
        GameObject fullWidthBarrier,
        float z)
    {
        block = plank;
        rope = visual;
        descent = down;
        ascent = up;
        barrier = fullWidthBarrier;
        sectionZ = z;
        pullPoint = new Vector3(7.7f, 0.55f, z - 1.95f);
        state = PullState.Ready;
        elapsed = 0f;
        timingPointer = 0f;
        activePlayer = null;
        if (block != null)
        {
            block.gameObject.SetActive(true);
            blockColliders = block.GetComponentsInChildren<Collider>(true);
            foreach (Collider collider in blockColliders) collider.enabled = true;
        }
        if (rope != null) rope.gameObject.SetActive(true);
        if (barrier != null) barrier.SetActive(false);
        if (ascent != null)
        {
            ascent.SetAvailable(false);
            ascent.SetSwingTarget(Vector3.zero, false);
            ascent.gameObject.SetActive(false);
        }
        if (descent != null)
        {
            descent.gameObject.SetActive(true);
            descent.SetAvailable(true);
            descent.SetSwingTarget(Vector3.zero, false);
        }
        SetInitialRope();
    }

    private void Awake()
    {
        if (block != null)
        {
            blockColliders = block.GetComponentsInChildren<Collider>(true);
        }
        else
        {
            blockColliders = new Collider[0];
        }

        state = PullState.Ready;
        timingPointer = 0f;
        if (barrier != null) barrier.SetActive(false);
        if (ascent != null)
        {
            ascent.SetAvailable(false);
            ascent.SetSwingTarget(Vector3.zero, false);
            ascent.gameObject.SetActive(false);
        }
        if (descent != null) descent.SetSwingTarget(Vector3.zero, false);
        SetInitialRope();
    }

    private void SetInitialRope()
    {
        if (rope == null || block == null) return;
        rope.gameObject.SetActive(true);
        rope.ResetRope();
        rope.SetPoints(new[] { RopeAnchor, OuterGuide, BlockTie });
    }

    public float InteractionDistance(Vector3 position)
    {
        if (state != PullState.Ready) return float.PositiveInfinity;
        float distance = Vector3.Distance(position, pullPoint);
        return distance <= interactionRadius ? distance : float.PositiveInfinity;
    }

    public bool TryPull(PlayerController player)
    {
        if (player == null || player.IsClimbing || player.IsSwinging || player.MovementLocked ||
            float.IsPositiveInfinity(InteractionDistance(player.Position)))
        {
            return false;
        }

        if (ValidatePull == null || !ValidatePull())
        {
            Debug.LogWarning("Throw rejected: the previous route or left recovery plank is not accessible.", this);
            return false;
        }

        state = PullState.Charging;
        elapsed = 0f;
        timingPointer = 0f;
        activePlayer = player;
        player.MovementLocked = true;
        player.BeginThrowPose(Vector3.left);
        if (Camera.main != null)
        {
            puzzleCamera = Camera.main.GetComponent<PuzzleCamera>();
            if (puzzleCamera != null) puzzleCamera.BeginThrowLook(Vector3.left);
        }
        foreach (Collider collider in blockColliders) collider.enabled = false;
        UpdateHeldPlank(true);
        if (barrier != null) barrier.SetActive(false);
        ThrowStarted?.Invoke(this);
        return true;
    }

    private void Update()
    {
        switch (state)
        {
            case PullState.Charging:
                UpdateTiming();
                break;
            case PullState.Throwing:
                UpdateThrow();
                break;
            case PullState.Failing:
                UpdateFailure();
                break;
        }
    }

    private void UpdateTiming()
    {
        elapsed += Time.unscaledDeltaTime;
        UpdateHeldPlank(false);
        float timingAge = Mathf.Max(0f, elapsed - poseBlendDuration);
        timingPointer = Mathf.PingPong(timingAge * timingSpeed, 1f);

        if (elapsed < poseBlendDuration) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
        {
            ResolveTiming(timingPointer);
        }
    }

    public void ResolveTiming(float pointer)
    {
        if (state != PullState.Charging) return;

        timingPointer = Mathf.Clamp01(pointer);
        if (timingPointer >= greenStart && timingPointer <= greenEnd)
        {
            BeginThrow();
        }
        else
        {
            BeginFailure();
        }
    }

    private void BeginThrow()
    {
        state = PullState.Throwing;
        elapsed = 0f;
        throwStart = block.position;
        throwStartRotation = block.rotation;
        foreach (Collider collider in blockColliders) collider.enabled = false;
    }

    private void BeginFailure()
    {
        state = PullState.Failing;
        elapsed = 0f;
        foreach (Collider collider in blockColliders) collider.enabled = false;
        if (rope != null) rope.BreakEnd();
    }

    private void UpdateThrow()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / throwDuration);
        float smooth = Mathf.SmoothStep(0f, 1f, t);
        block.position = EvaluateTrajectory(smooth);
        block.rotation = Quaternion.Slerp(
            throwStartRotation,
            Quaternion.Euler(0f, 180f, 0f),
            smooth);
        UpdateMovingRope();

        if (t >= 1f) Finish();
    }

    private void UpdateFailure()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / failedFallDuration);
        block.position += (Vector3.down * 7.5f + Vector3.right * 1.2f) * Time.deltaTime;
        block.Rotate(new Vector3(130f, 75f, 95f) * Time.deltaTime, Space.World);

        if (t < 1f) return;

        if (block != null) block.gameObject.SetActive(false);
        if (rope != null) rope.gameObject.SetActive(false);
        state = PullState.Failed;
        UnlockPlayer();
        Resolved?.Invoke(this, false);
    }

    public Vector3 EvaluateTrajectory(float t)
    {
        Vector3 end = FinalPosition;
        Vector3 apex = new Vector3(0f, 7.2f, sectionZ + 1.5f);
        Vector3 first = Vector3.Lerp(throwStart, apex, t);
        Vector3 second = Vector3.Lerp(apex, end, t);
        return Vector3.Lerp(first, second, t);
    }

    private void UpdateMovingRope()
    {
        if (rope == null) return;
        movingRopePoints[0] = RopeAnchor;
        movingRopePoints[1] = OuterGuide;
        movingRopePoints[2] = BlockTie;
        rope.SetPoints(movingRopePoints);
    }

    private void UpdateHeldPlank(bool immediate)
    {
        if (block == null || activePlayer == null) return;
        float distance = activePlayer.ParkourEnabled ? parkourHeldOffset : 1.35f;
        Vector3 target = activePlayer.Position + Vector3.left * distance + Vector3.up * 0.1f;
        float blend = immediate
            ? 1f
            : 1f - Mathf.Exp(-12f * Mathf.Max(0.001f, Time.unscaledDeltaTime));
        block.position = Vector3.Lerp(block.position, target, blend);
        block.rotation = Quaternion.Slerp(block.rotation, Quaternion.identity, blend);
        UpdateMovingRope();
    }

    // Kept as a named method because the editor smoke test and scene tooling
    // can deterministically place a mechanism in its successful end state.
    private void Finish()
    {
        if (block == null) return;

        block.position = FinalPosition;
        block.rotation = Quaternion.identity;
        block.gameObject.SetActive(true);
        foreach (Collider collider in blockColliders) collider.enabled = true;

        if (rope != null)
        {
            rope.gameObject.SetActive(true);
            rope.ResetRope();
            rope.SetPoints(new[]
            {
                RopeAnchor,
                new Vector3(4.2f, 0.62f, sectionZ),
                new Vector3(0f, 0.48f, sectionZ),
                new Vector3(-4.2f, 0.62f, sectionZ),
                new Vector3(-8.4f, 0.8f, sectionZ),
                new Vector3(-10.5f, 0.35f, sectionZ + PlatformGapHalf),
                BlockTie
            });
        }

        if (barrier != null) barrier.SetActive(true);
        if (ascent != null)
        {
            ascent.gameObject.SetActive(true);
            ascent.SetAvailable(true);
        }
        if (descent != null && ascent != null)
        {
            descent.SetSwingTarget(ascent.Bottom, true);
        }

        bool firstResolution = !IsResolved;
        state = PullState.Transferred;
        UnlockPlayer();
        if (firstResolution)
        {
            Transferred?.Invoke(this);
            Resolved?.Invoke(this, true);
        }
    }

    private void UnlockPlayer()
    {
        if (activePlayer != null)
        {
            activePlayer.EndThrowPose();
            activePlayer.MovementLocked = false;
        }
        if (puzzleCamera != null) puzzleCamera.EndThrowLook();
        puzzleCamera = null;
        activePlayer = null;
    }

    private void OnDisable()
    {
        UnlockPlayer();
    }
}
