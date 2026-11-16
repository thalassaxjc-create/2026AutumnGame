using UnityEngine;
using UnityEngine.UI;

/// <summary>直接绘制竹面网格，刮青只更新顶点颜色，不创建纹理或材质。</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class BambooSurfaceGraphic : MaskableGraphic
{
    public BambooCraftProgress Progress { get; set; }
    static readonly Color Flesh = new Color(0.91f, 0.8f, 0.48f);
    static readonly Color Green = new Color(0.15f, 0.31f, 0.15f);
    static readonly float[] SplitLines = { 0.5f, 0.25f, 0.75f };

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (Progress == null) return;
        Rect rect = rectTransform.rect;
        bool split = Progress.Stage == BambooCraftProgress.Step.Splitting;
        bool fourPieces = Progress.Stage == BambooCraftProgress.Step.RoughReady;
        if (split || fourPieces)
        {
            Quad(vh, rect, new Color(0.29f, 0.46f, 0.22f));
            for (int y = 0; y < 16; y++)
            {
                float light = Mathf.Sin(y / 15f * Mathf.PI) * 0.14f;
                Quad(vh, NormalRect(rect, 0, y / 16f, 1, 1f / 16), new Color(0.26f + light, 0.41f + light, 0.17f + light));
            }
            foreach (float node in BambooCraftProgress.Nodes)
            {
                Quad(vh, NormalRect(rect, node - 0.009f, 0, 0.018f, 1), new Color(0.62f, 0.65f, 0.34f));
                Quad(vh, NormalRect(rect, node + 0.009f, 0, 0.006f, 1), new Color(0.16f, 0.27f, 0.12f));
            }
            for (int i = 0; i < (fourPieces ? SplitLines.Length : 0); i++)
                Quad(vh, NormalRect(rect, 0, SplitLines[i] - 0.024f, 1, 0.048f), new Color(0.28f, 0.18f, 0.1f));
            if (split)
            {
                Quad(vh, NormalRect(rect, 0, Progress.GuideY - BambooCraftProgress.SplitGuideTolerance,
                    1, BambooCraftProgress.SplitGuideTolerance * 2), new Color(1f, 0.91f, 0.57f, 0.1f));
                for (int i = 0; i < 30; i++)
                    Quad(vh, NormalRect(rect, i / 30f, Progress.GuideY - 0.01f, 0.022f, 0.02f), new Color(1f, 0.91f, 0.57f));
                Quad(vh, NormalRect(rect, 0, Progress.GuideY - 0.05f, 0.018f, 0.1f), Color.white);
                Quad(vh, NormalRect(rect, 0.982f, Progress.GuideY - 0.05f, 0.018f, 0.1f), Color.white);
            }
        }
        else
        {
            for (int y = 0; y < BambooCraftProgress.Rows; y++)
            for (int x = 0; x < BambooCraftProgress.Columns; x++)
            {
                float variation = ((x * 13 + y * 7) % 9) * 0.005f;
                Color color = Progress.IsRemoved(x, y) ? Flesh : Green;
                color.r += variation; color.g += variation;
                Quad(vh, NormalRect(rect, x / (float)BambooCraftProgress.Columns, y / (float)BambooCraftProgress.Rows,
                    1f / BambooCraftProgress.Columns, 1f / BambooCraftProgress.Rows), color);
            }
            // Visible raised knots remain identifiable even after their green skin is removed.
            foreach (float node in BambooCraftProgress.Nodes)
            {
                Quad(vh, NormalRect(rect, node - 0.012f, 0, 0.024f, 1), new Color(0.75f, 0.61f, 0.3f, 0.65f));
                Quad(vh, NormalRect(rect, node - 0.014f, 0, 0.004f, 1), new Color(1f, 0.86f, 0.51f));
                Quad(vh, NormalRect(rect, node + 0.012f, 0, 0.005f, 1), new Color(0.25f, 0.2f, 0.09f));
            }
            foreach (Vector2 scar in Progress.Scars)
                Line(vh, Point(rect, scar - new Vector2(0.04f, 0.035f)), Point(rect, scar + new Vector2(0.055f, 0.035f)), 3f,
                    new Color(0.29f, 0.15f, 0.075f));
        }
        for (int i = 0; i < 8; i++)
            Quad(vh, NormalRect(rect, 0.015f, (i + 0.5f) / 8f, 0.97f, 0.004f), new Color(1f, 0.91f, 0.57f, 0.14f));
    }

    static Rect NormalRect(Rect r, float x, float y, float w, float h) => new Rect(r.x + x * r.width, r.y + y * r.height, w * r.width, h * r.height);
    static Vector2 Point(Rect r, Vector2 p) => new Vector2(r.x + p.x * r.width, r.y + p.y * r.height);

    static void Quad(VertexHelper vh, Rect r, Color color)
    {
        int i = vh.currentVertCount;
        vh.AddVert(new Vector3(r.xMin, r.yMin), color, Vector2.zero);
        vh.AddVert(new Vector3(r.xMin, r.yMax), color, Vector2.zero);
        vh.AddVert(new Vector3(r.xMax, r.yMax), color, Vector2.zero);
        vh.AddVert(new Vector3(r.xMax, r.yMin), color, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i + 2, i + 3, i);
    }

    static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
    {
        Vector2 d = (b - a).normalized;
        Vector2 n = new Vector2(-d.y, d.x) * width * 0.5f;
        int i = vh.currentVertCount;
        vh.AddVert(a - n, color, Vector2.zero); vh.AddVert(a + n, color, Vector2.zero);
        vh.AddVert(b + n, color, Vector2.zero); vh.AddVert(b - n, color, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i + 2, i + 3, i);
    }
}
