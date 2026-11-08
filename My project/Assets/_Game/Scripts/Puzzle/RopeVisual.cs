using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public sealed class RopeVisual : MonoBehaviour
{
    [SerializeField] private Vector3[] points = new Vector3[0];
    [SerializeField, Min(0.02f)] private float thickness = 0.22f;
    [SerializeField] private Material material;

    [Header("Soft rope simulation")]
    [SerializeField, Range(0f, 0.25f)] private float slack = 0.08f;
    [SerializeField, Range(0f, 1.5f)] private float gravityScale = 0.55f;
    [SerializeField, Range(4, 16)] private int constraintIterations = 9;
    [SerializeField, Range(0.85f, 0.999f)] private float damping = 0.982f;
    [SerializeField, Range(0.18f, 0.8f)] private float nodeSpacing = 0.34f;

    private struct Node
    {
        public Vector3 position;
        public Vector3 previous;
    }

    private LineRenderer line;
    private Node[] nodes;
    private int[] guideNodeIndices;
    private float restLength;
    private bool broken;
    private bool initialized;

    public float Thickness => thickness;
    public bool IsBroken => broken;

    private void Awake()
    {
        EnsureLine();
        if (Application.isPlaying) RebuildSimulation(); else RefreshStatic();
    }

    private void OnEnable()
    {
        EnsureLine();
        if (Application.isPlaying) RebuildSimulation(); else RefreshStatic();
    }

    private void OnValidate()
    {
        thickness = Mathf.Max(0.02f, thickness);
        nodeSpacing = Mathf.Max(0.18f, nodeSpacing);
        EnsureLine();
        if (!Application.isPlaying) RefreshStatic();
    }

    public void Configure(Vector3[] positions, Material ropeMaterial, float width = 0.22f)
    {
        points = positions == null ? new Vector3[0] : (Vector3[])positions.Clone();
        material = ropeMaterial;
        thickness = width;
        broken = false;
        ApplyRendererStyle();
        if (Application.isPlaying) RebuildSimulation(); else RefreshStatic();
    }

    public void SetStyle(Material ropeMaterial, float width, float slackAmount)
    {
        material = ropeMaterial;
        thickness = Mathf.Max(0.02f, width);
        slack = Mathf.Clamp(slackAmount, 0f, 0.25f);
        ApplyRendererStyle();
        if (Application.isPlaying) RebuildSimulation(); else RefreshStatic();
    }

    public void SetPoints(Vector3[] positions)
    {
        points = positions == null ? new Vector3[0] : (Vector3[])positions.Clone();
        broken = false;

        if (!Application.isPlaying)
        {
            RefreshStatic();
            return;
        }

        if (!initialized || nodes == null || nodes.Length < 2)
        {
            RebuildSimulation();
            return;
        }

        UpdateGuideMapping();
        PinGuides();
    }

    public void SetPlaneZ(float z)
    {
        if (points == null || points.Length == 0) return;
        Vector3[] moved = (Vector3[])points.Clone();
        for (int i = 0; i < moved.Length; i++) moved[i].z = z;
        SetPoints(moved);
    }

    /// <summary>
    /// Releases the load-bearing end. The remaining particles retain momentum
    /// and fall under gravity, making a missed timing input visibly snap.
    /// </summary>
    public void BreakEnd()
    {
        if (!Application.isPlaying) return;
        EnsureReady();
        broken = true;
    }

    public void ResetRope()
    {
        broken = false;
        if (Application.isPlaying) RebuildSimulation(); else RefreshStatic();
    }

    private void FixedUpdate()
    {
        if (!Application.isPlaying) return;

        EnsureReady();
        if (!initialized || nodes == null || nodes.Length < 2) return;

        float dt = Time.fixedDeltaTime;
        Vector3 acceleration = Physics.gravity * gravityScale;
        int fixedGuideCount = FixedGuideCount();

        for (int i = 0; i < nodes.Length; i++)
        {
            if (IsPinnedNode(i, fixedGuideCount)) continue;

            Vector3 current = nodes[i].position;
            Vector3 velocity = (current - nodes[i].previous) * damping;
            nodes[i].previous = current;
            nodes[i].position = current + velocity + acceleration * (dt * dt);
        }

        for (int iteration = 0; iteration < constraintIterations; iteration++)
        {
            SolveDistanceConstraints(fixedGuideCount);
            PinGuides();
        }
    }

    private void LateUpdate()
    {
        EnsureLine();
        if (!Application.isPlaying) return;

        EnsureReady();
        if (nodes == null || nodes.Length == 0)
        {
            line.positionCount = 0;
            return;
        }

        line.positionCount = nodes.Length;
        for (int i = 0; i < nodes.Length; i++) line.SetPosition(i, nodes[i].position);
    }

    private void EnsureReady()
    {
        if (!initialized) RebuildSimulation();
    }

    private void RebuildSimulation()
    {
        EnsureLine();
        initialized = false;

        if (points == null || points.Length < 2)
        {
            nodes = null;
            guideNodeIndices = null;
            line.positionCount = 0;
            return;
        }

        float length = PathLength(points);
        int count = Mathf.Clamp(Mathf.CeilToInt(length / nodeSpacing) + 1, 8, 96);
        Vector3[] sampled = Resample(points, count);
        nodes = new Node[count];
        for (int i = 0; i < count; i++)
        {
            nodes[i].position = sampled[i];
            nodes[i].previous = sampled[i];
        }

        restLength = length * (1f + slack) / Mathf.Max(1, count - 1);
        UpdateGuideMapping();
        PinGuides();
        initialized = true;
    }

    private void UpdateGuideMapping()
    {
        if (points == null || points.Length < 2 || nodes == null || nodes.Length < 2) return;

        guideNodeIndices = new int[points.Length];
        float total = PathLength(points);
        float traversed = 0f;
        guideNodeIndices[0] = 0;
        for (int i = 1; i < points.Length; i++)
        {
            traversed += Vector3.Distance(points[i - 1], points[i]);
            guideNodeIndices[i] = Mathf.Clamp(
                Mathf.RoundToInt((traversed / Mathf.Max(0.0001f, total)) * (nodes.Length - 1)),
                0,
                nodes.Length - 1);
        }

        guideNodeIndices[guideNodeIndices.Length - 1] = nodes.Length - 1;
        restLength = total * (1f + slack) / Mathf.Max(1, nodes.Length - 1);
    }

    private int FixedGuideCount()
    {
        if (guideNodeIndices == null) return 0;
        if (!broken) return guideNodeIndices.Length;

        // Keep the wall/edge turn attached while the plank end falls away.
        return Mathf.Min(2, Mathf.Max(1, guideNodeIndices.Length - 1));
    }

    private bool IsPinnedNode(int nodeIndex, int fixedGuideCount)
    {
        if (guideNodeIndices == null) return false;
        for (int i = 0; i < fixedGuideCount; i++)
        {
            if (guideNodeIndices[i] == nodeIndex) return true;
        }

        return false;
    }

    private void PinGuides()
    {
        if (nodes == null || guideNodeIndices == null || points == null) return;

        int fixedCount = FixedGuideCount();
        for (int i = 0; i < fixedCount; i++)
        {
            int nodeIndex = guideNodeIndices[i];
            nodes[nodeIndex].position = points[i];
            nodes[nodeIndex].previous = points[i];
        }
    }

    private void SolveDistanceConstraints(int fixedGuideCount)
    {
        for (int i = 0; i < nodes.Length - 1; i++)
        {
            Vector3 delta = nodes[i + 1].position - nodes[i].position;
            float distance = delta.magnitude;
            if (distance < 0.0001f) continue;

            Vector3 correction = delta * ((distance - restLength) / distance);
            bool aPinned = IsPinnedNode(i, fixedGuideCount);
            bool bPinned = IsPinnedNode(i + 1, fixedGuideCount);

            if (aPinned && bPinned) continue;
            if (aPinned)
            {
                nodes[i + 1].position -= correction;
            }
            else if (bPinned)
            {
                nodes[i].position += correction;
            }
            else
            {
                nodes[i].position += correction * 0.5f;
                nodes[i + 1].position -= correction * 0.5f;
            }
        }
    }

    private void RefreshStatic()
    {
        EnsureLine();
        ApplyRendererStyle();

        if (points == null || points.Length < 2)
        {
            line.positionCount = 0;
            return;
        }

        var preview = new List<Vector3>();
        const int samples = 8;
        for (int segment = 0; segment < points.Length - 1; segment++)
        {
            Vector3 a = points[segment];
            Vector3 b = points[segment + 1];
            for (int sample = 0; sample < samples; sample++)
            {
                if (segment > 0 && sample == 0) continue;

                float t = sample / (float)(samples - 1);
                Vector3 position = Vector3.Lerp(a, b, t);
                position += Vector3.down * (Mathf.Sin(t * Mathf.PI) * slack * Vector3.Distance(a, b));
                preview.Add(position);
            }
        }

        line.positionCount = preview.Count;
        line.SetPositions(preview.ToArray());
    }

    private void EnsureLine()
    {
        if (line == null) line = GetComponent<LineRenderer>();
        ApplyRendererStyle();
    }

    private void ApplyRendererStyle()
    {
        if (line == null) return;

        line.useWorldSpace = true;
        line.startWidth = thickness;
        line.endWidth = thickness;
        line.numCapVertices = 8;
        line.numCornerVertices = 5;
        line.textureMode = LineTextureMode.Tile;
        if (material != null) line.sharedMaterial = material;
    }

    private static float PathLength(IReadOnlyList<Vector3> source)
    {
        float length = 0f;
        for (int i = 1; i < source.Count; i++) length += Vector3.Distance(source[i - 1], source[i]);
        return length;
    }

    private static Vector3[] Resample(IReadOnlyList<Vector3> source, int count)
    {
        var result = new Vector3[count];
        float total = PathLength(source);
        result[0] = source[0];
        result[count - 1] = source[source.Count - 1];

        for (int i = 1; i < count - 1; i++)
        {
            float target = total * i / (count - 1f);
            float walked = 0f;
            for (int segment = 1; segment < source.Count; segment++)
            {
                float segmentLength = Vector3.Distance(source[segment - 1], source[segment]);
                if (walked + segmentLength >= target)
                {
                    float t = segmentLength > 0f ? (target - walked) / segmentLength : 0f;
                    result[i] = Vector3.Lerp(source[segment - 1], source[segment], t);
                    break;
                }
                walked += segmentLength;
            }
        }

        return result;
    }
}
