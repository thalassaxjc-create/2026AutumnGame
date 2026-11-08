using UnityEngine;

public sealed class FivePillarLevel : MonoBehaviour
{
    private const float FinishZ = 58f;
    private const float LevelEndZ = 59.2f;
    [Header("Parkour prototype")]
    [SerializeField] private bool useParkourRules;
    [SerializeField] private float parkourFinishZ = 172f;
    [SerializeField] private float parkourEndZ = 176f;
    public bool ExternalRespawn { get; set; }
    public Vector3 Checkpoint => checkpoint;
    public int CheckpointStage => checkpointStage;
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerInteraction interaction;
    [SerializeField] private PullBlockMechanism[] mechanisms = new PullBlockMechanism[0];
    [SerializeField] private GameObject[] ringObjects = new GameObject[0];
    [SerializeField] private int previewSeed = 2026;
    [SerializeField] private bool randomizeOnPlay = true;
    [SerializeField] private float fallRespawnHeight = -12f;

    private int[] masks;
    private PullBlockMechanism selected;
    private int checkpointStage;
    private Vector3 checkpoint = new Vector3(0f, 0.6f, -3f);

    public int RuntimeSeed { get; private set; }
    public int TransferCount { get; private set; }
    public int FailureCount { get; private set; }
    public int ResolvedCount { get; private set; }
    public bool AllResolved => mechanisms.Length > 0 && ResolvedCount >= mechanisms.Length;
    public bool ReachedFinish => (useParkourRules || AllResolved) && player != null && player.Position.z >= (useParkourRules ? parkourFinishZ : FinishZ) && player.Position.y > -1.5f;
    public bool PerfectComplete => ReachedFinish && TransferCount == mechanisms.Length;
    public bool Complete => AllResolved;
    public bool AtGoal => ReachedFinish;
    public int CompletionPercent
    {
        get
        {
            int objectiveCount = Mathf.Max(1, mechanisms.Length + 1);
            int achieved = TransferCount + (ReachedFinish ? 1 : 0);
            return Mathf.RoundToInt(100f * achieved / objectiveCount);
        }
    }

    public PlayerController Player => player;
    public PlayerInteraction Interaction => interaction;
    public PullBlockMechanism[] Mechanisms => mechanisms;
    public int[] Masks => masks;
    public bool IsSwinging
    {
        get
        {
            foreach (PullBlockMechanism mechanism in mechanisms)
            {
                if (mechanism != null && mechanism.IsBusy) return true;
            }
            return false;
        }
    }

    public PullBlockMechanism ActiveTimingMechanism
    {
        get
        {
            foreach (PullBlockMechanism mechanism in mechanisms)
            {
                if (mechanism != null && mechanism.State == PullBlockMechanism.PullState.Charging)
                    return mechanism;
            }
            return null;
        }
    }

    public void Configure(
        PlayerController controller,
        PlayerInteraction routing,
        PullBlockMechanism[] throws,
        GameObject[] rings)
    {
        player = controller;
        interaction = routing;
        mechanisms = throws;
        ringObjects = rings;
        ApplyLayout(previewSeed);
    }

    public void ApplyLayout(int seed)
    {
        masks = RingObstacleGenerator.Generate(seed);
        RuntimeSeed = seed;
        for (int row = 0; row < masks.Length; row++)
        {
            for (int lane = 0; lane < 5; lane++)
            {
                int index = row * 5 + lane;
                if (index < ringObjects.Length && ringObjects[index] != null)
                {
                    ringObjects[index].SetActive((masks[row] & (1 << lane)) != 0);
                }
            }
        }
    }

    private void Awake()
    {
        ApplyLayout(randomizeOnPlay ? System.Environment.TickCount : previewSeed);
    }

    private void OnEnable()
    {
        if (player != null) player.ConstrainMovement = Constrain;
        if (interaction != null)
        {
            interaction.PullDistance = SelectPull;
            interaction.PullAction = PullSelected;
        }

        for (int i = 0; i < mechanisms.Length; i++)
        {
            if (mechanisms[i] == null) continue;
            int index = i;
            mechanisms[i].ValidatePull = () => CanPull(index);
            mechanisms[i].Resolved += OnResolved;
        }
    }

    private void OnDisable()
    {
        if (player != null) player.ConstrainMovement = null;
        if (interaction != null)
        {
            interaction.PullDistance = null;
            interaction.PullAction = null;
        }

        foreach (PullBlockMechanism mechanism in mechanisms)
        {
            if (mechanism == null) continue;
            mechanism.ValidatePull = null;
            mechanism.Resolved -= OnResolved;
        }
    }

    private void OnResolved(PullBlockMechanism mechanism, bool success)
    {
        ResolvedCount++;
        if (success) TransferCount++; else FailureCount++;
    }

    public bool CanPull(int index)
    {
        if (index < 0 || index >= mechanisms.Length || IsSwinging ||
            !RingObstacleGenerator.Validate(masks))
        {
            return false;
        }

        PullBlockMechanism mechanism = mechanisms[index];
        if (mechanism == null || index > checkpointStage ||
            (index > 0 && !mechanisms[index - 1].IsResolved))
        {
            return false;
        }

        if (!mechanism.Descent.Available ||
            mechanism.Descent.Top.z >= mechanism.SectionZ ||
            mechanism.Ascent.Top.z <= mechanism.SectionZ)
        {
            return false;
        }

        if (index > 0 && mechanism.Descent.Top.z <= mechanisms[index - 1].SectionZ + 1f)
        {
            return false;
        }

        float swingGap = mechanism.Ascent.Bottom.z - mechanism.Descent.Bottom.z;
        float ropeLength = Vector3.Distance(mechanism.Descent.Bottom, mechanism.Descent.Top);
        if (swingGap > ropeLength * 1.8f + 1f || swingGap < 2f)
        {
            return false;
        }

        return player.Position.z < mechanism.SectionZ - 0.5f;
    }

    private float SelectPull(Vector3 position)
    {
        selected = null;
        if (IsSwinging) return float.PositiveInfinity;

        float best = float.PositiveInfinity;
        for (int i = 0; i < mechanisms.Length; i++)
        {
            if (i > checkpointStage || mechanisms[i] == null) continue;
            float distance = mechanisms[i].InteractionDistance(position);
            if (distance < best)
            {
                best = distance;
                selected = mechanisms[i];
            }
        }
        return best;
    }

    private bool PullSelected()
    {
        return selected != null && selected.TryPull(player);
    }

    private void Update()
    {
        for (int i = checkpointStage; i < mechanisms.Length; i++)
        {
            PullBlockMechanism mechanism = mechanisms[i];
            if (mechanism == null || !mechanism.IsResolved || player.IsClimbing || player.IsSwinging)
                break;

            if (mechanism.State == PullBlockMechanism.PullState.Transferred)
            {
                if (player.Position.y < 0.3f ||
                    player.Position.z < mechanism.Ascent.Top.z - 0.5f) break;
                checkpoint = mechanism.Ascent.Top + new Vector3(0.5f, 0.05f, 0f);
            }
            else
            {
                if (player.Position.y < 0.3f || player.Position.z < mechanism.SectionZ + 1.5f) break;
                checkpoint = new Vector3(0f, 0.6f, mechanism.SectionZ + 2.5f);
            }

            checkpointStage = i + 1;
        }

        if (!ExternalRespawn && !player.IsClimbing && !player.IsSwinging && !IsSwinging && player.Position.y < fallRespawnHeight)
        {
            player.Respawn(checkpoint);
        }
    }

    public Vector3 Constrain(Vector3 origin, Vector3 target)
    {
        // A rope release is the intended way through the lower route. Let its
        // ballistic arc cross the same Z plane that blocks the upper walkway.
        if (player.SwingTraversalActive && origin.y < 0.3f)
        {
            return target;
        }

        if (origin.y < -1.5f)
        {
            // A regular running jump may not bypass the lower puzzle. Crossing
            // the midpoint is enabled only while holding/releasing a swing rope.
            if (!player.SwingTraversalActive)
            {
                foreach (PullBlockMechanism mechanism in mechanisms)
                {
                    if (mechanism == null || mechanism.State != PullBlockMechanism.PullState.Transferred)
                        continue;

                    float gateZ = (mechanism.Descent.Bottom.z + mechanism.Ascent.Bottom.z) * 0.5f;
                    target = ClipRect(origin, target, -20f, 20f, gateZ);
                }
            }
            return target;
        }

        target.x = Mathf.Clamp(target.x, -8.45f, 8.45f);
        target.z = Mathf.Clamp(target.z, -3.7f, useParkourRules ? parkourEndZ : LevelEndZ);
        if (masks == null) return target;

        for (int row = 0; !useParkourRules && row < masks.Length; row++)
        {
            for (int lane = 0; lane < 5; lane++)
            {
                if ((masks[row] & (1 << lane)) == 0) continue;
                target = ClipRect(
                    origin,
                    target,
                    (lane - 2) * 3.6f - RingObstacleGenerator.BlockedHalfWidth,
                    (lane - 2) * 3.6f + RingObstacleGenerator.BlockedHalfWidth,
                    RingObstacleGenerator.Rows[row]);
            }
        }

        foreach (PullBlockMechanism mechanism in mechanisms)
        {
            if (mechanism != null && mechanism.BlocksPath)
            {
                target = ClipRect(origin, target, -20f, 20f, mechanism.SectionZ);
            }
        }
        return target;
    }

    private static Vector3 ClipRect(Vector3 origin, Vector3 target, float minX, float maxX, float z)
    {
        const float depth = 0.5f;
        if (Mathf.Abs(origin.z - z) <= depth && origin.x < minX && target.x >= minX)
            target.x = minX - 0.01f;
        else if (Mathf.Abs(origin.z - z) <= depth && origin.x > maxX && target.x <= maxX)
            target.x = maxX + 0.01f;

        bool overlapsX = Mathf.Max(origin.x, target.x) >= minX && Mathf.Min(origin.x, target.x) <= maxX;
        if (overlapsX && origin.z < z - depth && target.z >= z - depth)
            target.z = z - depth - 0.01f;
        else if (overlapsX && origin.z > z + depth && target.z <= z + depth)
            target.z = z + depth + 0.01f;
        return target;
    }
}
