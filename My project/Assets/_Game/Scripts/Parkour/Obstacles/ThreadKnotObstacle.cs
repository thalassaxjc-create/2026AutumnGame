using UnityEngine;

public sealed class ThreadKnotObstacle : ParkourObstacle
{
    [SerializeField] private Transform[] knots = new Transform[5];
    [SerializeField] private Collider[] knotColliders = new Collider[5];
    [SerializeField] private Renderer[] shadows = new Renderer[5];
    [SerializeField] private MovingSurface[] laneSurfaces = new MovingSurface[5];

    private readonly float[] knotHeightOffsets = new float[5];
    private readonly float[] shadowHeightOffsets = new float[5];

    // Kept for compatibility with scene diagnostics. Thread knots are fixed obstacles now.
    public int SafeLane => -1;
    public bool IsMoving => false;

    public void SetGeometry(
        Transform[] models,
        Collider[] colliders,
        Renderer[] shadowModels,
        MovingSurface[] surfaces = null)
    {
        knots = models;
        knotColliders = colliders;
        shadows = shadowModels;
        if (surfaces != null) laneSurfaces = surfaces;
    }

    public void SetLaneSurfaces(MovingSurface[] surfaces)
    {
        laneSurfaces = surfaces;
    }

    private void Awake()
    {
        ResolveLaneSurfaces();
        CaptureHeightOffsets();
    }

    private void ResolveLaneSurfaces()
    {
        if (laneSurfaces == null || laneSurfaces.Length != 5)
            laneSurfaces = new MovingSurface[5];

        FivePillarLevel level = GetComponentInParent<FivePillarLevel>();
        Transform pillars = level != null ? level.transform.Find("FivePillars") : null;
        if (pillars == null) return;

        for (int lane = 0; lane < 5; lane++)
        {
            if (laneSurfaces[lane] != null) continue;
            Transform pillar = pillars.Find("Pillar_" + (lane + 1));
            if (pillar != null) laneSurfaces[lane] = pillar.GetComponent<MovingSurface>();
        }
    }

    private void CaptureHeightOffsets()
    {
        for (int lane = 0; lane < 5; lane++)
        {
            if (laneSurfaces[lane] == null) continue;
            float surfaceY = laneSurfaces[lane].transform.position.y;

            if (lane < knots.Length && knots[lane] != null)
                knotHeightOffsets[lane] = knots[lane].position.y - surfaceY;

            if (lane < shadows.Length && shadows[lane] != null)
                shadowHeightOffsets[lane] = shadows[lane].transform.position.y - surfaceY;
        }
    }

    private void FollowLaneSurface(int lane)
    {
        if (laneSurfaces == null || lane >= laneSurfaces.Length || laneSurfaces[lane] == null)
            return;

        float surfaceY = laneSurfaces[lane].transform.position.y;

        Vector3 knotPosition = knots[lane].position;
        knotPosition.y = surfaceY + knotHeightOffsets[lane];
        knots[lane].position = knotPosition;

        if (lane < shadows.Length && shadows[lane] != null)
        {
            Transform shadow = shadows[lane].transform;
            Vector3 shadowPosition = shadow.position;
            shadowPosition.y = surfaceY + shadowHeightOffsets[lane];
            shadow.position = shadowPosition;
        }
    }

    private void Update()
    {
        if (player == null || obstacle == null) return;

        float distance = obstacle.z - player.Position.z;
        for (int lane = 0; lane < knots.Length; lane++)
        {
            if (knots[lane] == null) continue;

            FollowLaneSurface(lane);

            bool blocked = (obstacle.laneMask & (1 << lane)) != 0;
            knots[lane].gameObject.SetActive(blocked);

            if (lane < knotColliders.Length && knotColliders[lane] != null)
                knotColliders[lane].enabled = blocked;

            if (lane < shadows.Length && shadows[lane] != null)
                shadows[lane].enabled = blocked && distance < obstacle.activationDistance;
        }

        Pass(100);
    }
}
