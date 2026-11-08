using UnityEngine;
using System.Collections.Generic;

public static class RingObstacleGenerator
{
    // The original 42-unit table was stretched by 1.5x along Z so each
    // suspended-plank jump has a real launch gap instead of a short hop.
    public static readonly float[] Rows = { 1.25f, 5.75f, 10.25f, 29f, 33.5f, 38f, 56f };
    public const float LaneSpacing = 3.6f;
    public const float BlockedHalfWidth = 1.97f;
    public const float BlockedHalfDepth = 0.5f;

    public static int[] Generate(int seed)
    {
        var random = new System.Random(seed);
        for (int attempt = 0; attempt < 8; attempt++)
        {
            int[] masks = new int[Rows.Length];
            for (int row = 0; row < masks.Length; row++) masks[row] = random.Next(1, 31);
            if (Validate(masks)) return masks;
        }
        int[] fallback = { 3, 12, 19, 7, 24, 13, 15 };
        if (!Validate(fallback)) throw new System.InvalidOperationException("Fallback ring layout is not traversable.");
        return fallback;
    }
    public static bool Contains(int[] masks, float x, float z)
    {
        for (int row = 0; row < Rows.Length; row++)
        {
            if (Mathf.Abs(z - Rows[row]) > BlockedHalfDepth) continue;
            for (int lane = 0; lane < 5; lane++)
                if ((masks[row] & (1 << lane)) != 0 && Mathf.Abs(x - (lane - 2) * LaneSpacing) <= BlockedHalfWidth) return true;
        }
        return false;
    }
    public static bool Validate(int[] masks)
    {
        if (masks == null || masks.Length != Rows.Length) return false;
        for (int i = 0; i < masks.Length; i++)
        {
            if (masks[i] < 1 || masks[i] >= 31) return false;
            if (i > 0 && Rows[i] - Rows[i - 1] < 2.5f) return false;
            foreach (float reserved in new[] { 15.5f, 20f, 24.5f, 42.5f, 47f, 51.5f })
                if (Mathf.Abs(Rows[i] - reserved) < 2.2f) return false;
        }
        return Reachable(masks, -3f, 18f, 2, 4) &&
               Reachable(masks, 24.5f, 45f, 0, 4) &&
               Reachable(masks, 51.5f, 59f, 0, 2);
    }
    // Each node is a lane center or a midpoint between lanes. Edges sample swept clearance.
    private static bool Reachable(int[] masks, float startZ, float endZ, int startLane, int endLane)
    {
        const float step = 0.5f;
        int depth = Mathf.RoundToInt((endZ - startZ) / step) + 1;
        bool[,] visited = new bool[9, depth];
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(new Vector2Int(startLane * 2, 0));
        visited[startLane * 2, 0] = true;
        int[] dx = { -1, 1, 0, 0 }; int[] dz = { 0, 0, -1, 1 };
        while (queue.Count > 0)
        {
            var n = queue.Dequeue();
            if (n.x == endLane * 2 && n.y == depth - 1) return true;
            for (int d = 0; d < 4; d++)
            {
                int x = n.x + dx[d], z = n.y + dz[d];
                if (x < 0 || x >= 9 || z < 0 || z >= depth || visited[x, z]) continue;
                bool blocked = false;
                for (int sample = 0; sample <= 8; sample++)
                {
                    float t = sample / 8f;
                    float px = Mathf.Lerp(n.x, x, t) * LaneSpacing * .5f - 2 * LaneSpacing;
                    float pz = startZ + Mathf.Lerp(n.y, z, t) * step;
                    if (Contains(masks, px, pz)) { blocked = true; break; }
                }
                if (!blocked) { visited[x, z] = true; queue.Enqueue(new Vector2Int(x, z)); }
            }
        }
        return false;
    }
}
