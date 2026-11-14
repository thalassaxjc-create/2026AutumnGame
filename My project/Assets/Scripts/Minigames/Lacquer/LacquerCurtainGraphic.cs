using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 不依赖图片资源的刷漆工作台背景：木架、抄纸竹帘、漆盘和刷子。
/// 漆面会随小游戏进度从左向右显出光泽。
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class LacquerCurtainGraphic : MaskableGraphic
{
    float progress;
    float brushFlash;
    Color brushColor = Color.white;

    public void SetProgress(float value)
    {
        value = Mathf.Clamp01(value);
        if (Mathf.Approximately(progress, value))
            return;

        progress = value;
        SetVerticesDirty();
    }

    public void PlayBrush(Color color)
    {
        brushColor = color;
        brushFlash = 0.38f;
        SetVerticesDirty();
    }

    public void ResetVisual()
    {
        progress = 0f;
        brushFlash = 0f;
        SetVerticesDirty();
    }

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    void Update()
    {
        if (brushFlash <= 0f)
            return;

        brushFlash = Mathf.Max(0f, brushFlash - Time.unscaledDeltaTime);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        float w = r.width;
        float h = r.height;

        AddGradientQuad(vh, r, new Color32(16, 12, 10, 255), new Color32(55, 36, 27, 255));
        AddQuad(vh, new Rect(r.xMin, r.yMin, w * 0.075f, h), new Color(0f, 0f, 0f, 0.30f));
        AddQuad(vh, new Rect(r.xMax - w * 0.075f, r.yMin, w * 0.075f, h), new Color(0f, 0f, 0f, 0.30f));

        Color woodDark = new Color32(73, 39, 27, 255);
        Color wood = new Color32(135, 78, 47, 255);
        Color woodLight = new Color32(178, 113, 65, 255);
        Color bamboo = new Color32(189, 158, 104, 255);
        Color thread = new Color32(224, 211, 180, 190);
        Color rawPaper = new Color32(214, 196, 150, 255);
        Color lacquerDark = new Color32(67, 22, 18, 245);
        Color lacquerLight = new Color32(139, 49, 36, 246);

        float left = r.xMin + w * 0.105f;
        float right = r.xMin + w * 0.785f;
        float bottom = r.yMin + h * 0.205f;
        float top = r.yMin + h * 0.735f;

        // 工作台与木框。
        AddQuad(vh, new Rect(left - 38f, bottom - 70f, right - left + 76f, 30f), new Color(0f, 0f, 0f, 0.28f));
        AddQuad(vh, new Rect(left - 48f, bottom - 55f, right - left + 96f, 26f), woodDark);
        AddQuad(vh, new Rect(left - 48f, bottom - 50f, right - left + 96f, 12f), woodLight);
        AddQuad(vh, new Rect(left - 28f, bottom - 40f, 24f, top - bottom + 92f), woodDark);
        AddQuad(vh, new Rect(right + 4f, bottom - 40f, 24f, top - bottom + 92f), woodDark);
        AddQuad(vh, new Rect(left - 22f, bottom - 34f, 10f, top - bottom + 80f), wood);
        AddQuad(vh, new Rect(right + 10f, bottom - 34f, 10f, top - bottom + 80f), wood);
        AddQuad(vh, new Rect(left - 34f, top + 8f, right - left + 68f, 34f), woodDark);
        AddQuad(vh, new Rect(left - 28f, top + 15f, right - left + 56f, 17f), woodLight);

        Rect curtain = new Rect(left, bottom, right - left, top - bottom);
        AddGradientQuad(vh, curtain, new Color32(163, 137, 91, 255), rawPaper);
        AddOutline(vh, curtain, 3f, new Color(0.95f, 0.80f, 0.52f, 0.42f));

        // 竹帘细丝与横向绑线。
        for (int i = 0; i < 96; i++)
        {
            float x = Mathf.Lerp(curtain.xMin + 2f, curtain.xMax - 2f, (i + 0.5f) / 96f);
            AddLine(vh, new Vector2(x, curtain.yMin), new Vector2(x, curtain.yMax),
                i % 4 == 0 ? 1.25f : 0.7f, i % 2 == 0 ? bamboo : new Color32(229, 208, 158, 180));
        }

        for (int i = 0; i < 11; i++)
        {
            float y = Mathf.Lerp(curtain.yMin + 10f, curtain.yMax - 10f, i / 10f);
            AddLine(vh, new Vector2(curtain.xMin, y), new Vector2(curtain.xMax, y), 1.25f, thread);
        }

        // 已刷区域与漆面高光。
        float coatedWidth = curtain.width * progress;
        if (coatedWidth > 0.5f)
        {
            Rect coated = new Rect(curtain.xMin, curtain.yMin, coatedWidth, curtain.height);
            AddGradientQuad(vh, coated, lacquerDark, lacquerLight);

            for (int i = 0; i < 12; i++)
            {
                float y = Mathf.Lerp(coated.yMin + 12f, coated.yMax - 12f, i / 11f);
                float alpha = i % 2 == 0 ? 0.14f : 0.07f;
                AddLine(vh, new Vector2(coated.xMin + 4f, y), new Vector2(coated.xMax - 4f, y + 5f),
                    i % 3 == 0 ? 5f : 2.5f, new Color(1f, 0.75f, 0.52f, alpha));
            }

            float sheenX = Mathf.Lerp(coated.xMin, coated.xMax, 0.72f);
            AddLine(vh, new Vector2(sheenX, coated.yMin + 8f), new Vector2(sheenX + 52f, coated.yMax - 8f),
                16f, new Color(1f, 0.86f, 0.68f, 0.10f));
        }

        if (progress < 1f)
        {
            float edgeX = curtain.xMin + coatedWidth;
            float pulse = 0.38f + Mathf.Sin(Time.unscaledTime * 4.5f) * 0.10f;
            AddLine(vh, new Vector2(edgeX, curtain.yMin), new Vector2(edgeX, curtain.yMax), 4f,
                new Color(1f, 0.83f, 0.56f, pulse));
        }

        // 右侧漆盘：朱漆、青漆和横放的漆刷。
        Vector2 redBowl = new Vector2(r.xMin + w * 0.865f, r.yMin + h * 0.56f);
        Vector2 greenBowl = new Vector2(r.xMin + w * 0.865f, r.yMin + h * 0.38f);
        DrawBowl(vh, redBowl, new Color32(158, 48, 37, 255), woodDark);
        DrawBowl(vh, greenBowl, new Color32(48, 112, 76, 255), woodDark);

        Vector2 brushStart = new Vector2(r.xMin + w * 0.815f, r.yMin + h * 0.245f);
        Vector2 brushEnd = new Vector2(r.xMin + w * 0.925f, r.yMin + h * 0.315f);
        AddLine(vh, brushStart + new Vector2(5f, -6f), brushEnd + new Vector2(5f, -6f), 18f,
            new Color(0f, 0f, 0f, 0.30f));
        AddLine(vh, brushStart, brushEnd, 15f, woodLight);
        Vector2 bristleDir = (brushStart - brushEnd).normalized;
        AddLine(vh, brushStart, brushStart + bristleDir * 58f, 31f, new Color32(39, 28, 22, 255));
        AddLine(vh, brushStart + bristleDir * 8f, brushStart + bristleDir * 51f, 12f,
            new Color32(180, 131, 77, 210));

        if (brushFlash > 0f)
        {
            float normalized = brushFlash / 0.38f;
            float x = curtain.xMin + Mathf.Clamp01(progress) * curtain.width;
            float radius = Mathf.Lerp(70f, 20f, 1f - normalized);
            Color flash = brushColor;
            flash.a = normalized * 0.45f;
            AddCircle(vh, new Vector2(x, curtain.center.y), radius, flash, 28);
        }
    }

    static void DrawBowl(VertexHelper vh, Vector2 center, Color lacquer, Color rim)
    {
        AddCircle(vh, center + new Vector2(5f, -8f), 46f, new Color(0f, 0f, 0f, 0.30f), 30);
        AddCircle(vh, center, 45f, rim, 30);
        AddCircle(vh, center, 35f, lacquer, 30);
        AddCircle(vh, center + new Vector2(-10f, 12f), 9f, new Color(1f, 0.88f, 0.72f, 0.28f), 18);
    }

    static void AddOutline(VertexHelper vh, Rect rect, float thickness, Color color)
    {
        AddQuad(vh, new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
        AddQuad(vh, new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
        AddQuad(vh, new Rect(rect.xMin, rect.yMin, thickness, rect.height), color);
        AddQuad(vh, new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), color);
    }

    static void AddGradientQuad(VertexHelper vh, Rect rect, Color bottom, Color top)
    {
        int index = vh.currentVertCount;
        AddVert(vh, new Vector2(rect.xMin, rect.yMin), bottom);
        AddVert(vh, new Vector2(rect.xMin, rect.yMax), top);
        AddVert(vh, new Vector2(rect.xMax, rect.yMax), top);
        AddVert(vh, new Vector2(rect.xMax, rect.yMin), bottom);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }

    static void AddQuad(VertexHelper vh, Rect rect, Color color)
    {
        int index = vh.currentVertCount;
        AddVert(vh, new Vector2(rect.xMin, rect.yMin), color);
        AddVert(vh, new Vector2(rect.xMin, rect.yMax), color);
        AddVert(vh, new Vector2(rect.xMax, rect.yMax), color);
        AddVert(vh, new Vector2(rect.xMax, rect.yMin), color);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }

    static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
    {
        Vector2 direction = b - a;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (width * 0.5f);
        int index = vh.currentVertCount;
        AddVert(vh, a - normal, color);
        AddVert(vh, a + normal, color);
        AddVert(vh, b + normal, color);
        AddVert(vh, b - normal, color);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }

    static void AddCircle(VertexHelper vh, Vector2 center, float radius, Color color, int segments)
    {
        int centerIndex = vh.currentVertCount;
        AddVert(vh, center, color);
        for (int i = 0; i <= segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            AddVert(vh, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color);
        }

        for (int i = 0; i < segments; i++)
            vh.AddTriangle(centerIndex, centerIndex + i + 1, centerIndex + i + 2);
    }

    static void AddVert(VertexHelper vh, Vector2 position, Color color)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        vertex.uv0 = Vector2.zero;
        vh.AddVert(vertex);
    }
}
