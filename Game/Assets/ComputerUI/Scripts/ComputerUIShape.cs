using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class ComputerUIShape : MaskableGraphic
{
    [SerializeField] bool speechBubble;
    [SerializeField] Color borderColor = Color.black;

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = GetPixelAdjustedRect();
        if (rect.width <= 0f || rect.height <= 0f)
            return;

        if (speechBubble)
            DrawBubble(mesh, rect);
        else
            DrawCapsule(mesh, rect);
    }

    void DrawCapsule(VertexHelper mesh, Rect rect)
    {
        const int segments = 24;
        float radius = Mathf.Min(rect.width, rect.height) / 2f;
        mesh.AddVert(rect.center, color, Vector2.zero);
        for (int side = 0; side < 2; side++)
        {
            float centerX = side == 0 ? rect.xMax - radius : rect.xMin + radius;
            for (int i = 0; i <= segments; i++)
            {
                float angle = (side - 0.5f + (float)i / segments) * Mathf.PI;
                Vector2 point = new Vector2(centerX + Mathf.Cos(angle) * radius,
                    rect.center.y + Mathf.Sin(angle) * rect.height / 2f);
                mesh.AddVert(point, color, Vector2.zero);
            }
        }

        int count = 2 * (segments + 1);
        for (int i = 0; i < count; i++)
            mesh.AddTriangle(0, 1 + i, 1 + (i + 1) % count);
    }

    void DrawBubble(VertexHelper mesh, Rect rect)
    {
        Vector2[] outline =
        {
            new Vector2(101.74f, 0f), new Vector2(1579.43f, 0f),
            new Vector2(1579.43f, 592.99f), new Vector2(101.74f, 592.99f),
            new Vector2(101.74f, 244.06f), new Vector2(0f, 92.63f)
        };

        for (int i = 0; i < outline.Length; i++)
        {
            outline[i] = new Vector2(rect.xMin + rect.width * outline[i].x / 1579.43f,
                rect.yMax - rect.height * outline[i].y / 592.99f);
            mesh.AddVert(outline[i], color, Vector2.zero);
        }

        mesh.AddTriangle(0, 1, 2);
        mesh.AddTriangle(2, 3, 0);
        mesh.AddTriangle(0, 4, 5);

        for (int i = 0; i < outline.Length; i++)
        {
            Vector2 from = outline[i];
            Vector2 to = outline[(i + 1) % outline.Length];
            Vector2 direction = (to - from).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x);
            int index = mesh.currentVertCount;
            mesh.AddVert(from - direction + normal, borderColor, Vector2.zero);
            mesh.AddVert(to + direction + normal, borderColor, Vector2.zero);
            mesh.AddVert(to + direction - normal, borderColor, Vector2.zero);
            mesh.AddVert(from - direction - normal, borderColor, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
