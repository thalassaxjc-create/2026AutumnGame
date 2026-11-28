using UnityEngine;
using UnityEngine.UI;

/// <summary>The two upgraded tools as small workshop illustrations.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class WorkshopToolGraphic : MaskableGraphic
{
    static readonly Color32 Walnut = new(84, 49, 40, 255);
    static readonly Color32 WoodGrain = new(135, 78, 53, 255);
    static readonly Color32 Brass = new(218, 157, 66, 255);
    static readonly Color32 Steel = new(153, 187, 187, 255);
    static readonly Color32 Bevel = new(225, 239, 225, 255);
    static readonly Color32 Thread = new(50, 102, 103, 255);
    bool shuttle;

    internal void Show(bool showShuttle)
    {
        shuttle = showShuttle;
        raycastTarget = false;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = rectTransform.rect;
        float scale = Mathf.Min(rect.width / 2.75f, rect.height);
        rect = new Rect(rect.center - new Vector2(scale * 1.375f, scale * .5f), new Vector2(scale * 2.75f, scale));
        if (shuttle)
        {
            Quad(mesh, rect, new(.04f, .49f), new(.22f, .76f), new(.79f, .76f), new(.96f, .49f), WoodGrain);
            Quad(mesh, rect, new(.04f, .49f), new(.96f, .49f), new(.79f, .23f), new(.22f, .23f), Walnut);
            Band(mesh, rect, .22f, .28f, .35f, .66f, Brass);
            Band(mesh, rect, .72f, .78f, .35f, .66f, Brass);
            Band(mesh, rect, .35f, .65f, .36f, .65f, Thread);
            for (int i = 0; i < 5; i++)
                Band(mesh, rect, .38f + .052f * i, .39f + .052f * i, .38f, .63f, Bevel);
        }
        else
        {
            Quad(mesh, rect, new(.07f, .36f), new(.07f, .62f), new(.43f, .68f), new(.43f, .32f), Walnut);
            Band(mesh, rect, .11f, .39f, .52f, .58f, WoodGrain);
            Band(mesh, rect, .16f, .19f, .45f, .56f, Brass);
            Band(mesh, rect, .33f, .36f, .45f, .56f, Brass);
            Quad(mesh, rect, new(.45f, .33f), new(.45f, .71f), new(.93f, .78f), new(.88f, .37f), Steel);
            Quad(mesh, rect, new(.45f, .33f), new(.45f, .42f), new(.94f, .44f), new(.88f, .37f), Bevel);
            Band(mesh, rect, .40f, .47f, .29f, .73f, Brass);
        }
    }

    static void Band(VertexHelper mesh, Rect rect, float left, float right, float bottom, float top, Color32 tint) =>
        Quad(mesh, rect, new(left, bottom), new(left, top), new(right, top), new(right, bottom), tint);

    static void Quad(VertexHelper mesh, Rect rect, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 tint)
    {
        int n = mesh.currentVertCount;
        mesh.AddVert(rect.min + Vector2.Scale(a, rect.size), tint, Vector2.zero);
        mesh.AddVert(rect.min + Vector2.Scale(b, rect.size), tint, Vector2.zero);
        mesh.AddVert(rect.min + Vector2.Scale(c, rect.size), tint, Vector2.zero);
        mesh.AddVert(rect.min + Vector2.Scale(d, rect.size), tint, Vector2.zero);
        mesh.AddTriangle(n, n + 1, n + 2);
        mesh.AddTriangle(n, n + 2, n + 3);
    }
}
