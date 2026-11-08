using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the shallow crescent/boat plank used by every hanging platform.
/// The raised ends make the silhouette read as a wooden shuttle while the
/// recessed centre channel gives the rope a visible place to bind.
/// </summary>
public static class CrescentPlankMesh
{
    public static Mesh Build(
        float length = 3.5f,
        float width = 3.2f,
        float thickness = 0.45f,
        float rocker = 0.52f,
        float grooveWidth = 0.58f,
        float grooveDepth = 0.2f)
    {
        const int lengthSegments = 24;
        const int widthSegments = 14;

        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        float halfLength = length * 0.5f;
        float halfWidth = width * 0.5f;
        float halfThickness = thickness * 0.5f;
        float grooveHalf = Mathf.Max(0.01f, grooveWidth * 0.5f);

        Vector3 Point(float x, float z, bool top)
        {
            float along = z / Mathf.Max(0.0001f, halfLength);
            float edgeLift = rocker * along * along;
            float y = edgeLift + (top ? halfThickness : -halfThickness);

            if (top && Mathf.Abs(x) < grooveHalf)
            {
                float channel = 1f - Mathf.Abs(x) / grooveHalf;
                y -= grooveDepth * channel * channel;
            }

            // A very small edge roll keeps the top from reading as a flat cube.
            if (top)
            {
                float side = Mathf.Abs(x) / Mathf.Max(0.0001f, halfWidth);
                y += 0.06f * side * side;
            }

            return new Vector3(x, y, z);
        }

        int TopIndex(int x, int z) => z * (widthSegments + 1) + x;
        int BottomIndex(int x, int z) =>
            (lengthSegments + 1) * (widthSegments + 1) + z * (widthSegments + 1) + x;

        for (int zIndex = 0; zIndex <= lengthSegments; zIndex++)
        {
            float z = Mathf.Lerp(-halfLength, halfLength, zIndex / (float)lengthSegments);
            for (int xIndex = 0; xIndex <= widthSegments; xIndex++)
            {
                float x = Mathf.Lerp(-halfWidth, halfWidth, xIndex / (float)widthSegments);
                vertices.Add(Point(x, z, true));
                uvs.Add(new Vector2(xIndex / (float)widthSegments, zIndex / (float)lengthSegments));
            }
        }

        for (int zIndex = 0; zIndex <= lengthSegments; zIndex++)
        {
            float z = Mathf.Lerp(-halfLength, halfLength, zIndex / (float)lengthSegments);
            for (int xIndex = 0; xIndex <= widthSegments; xIndex++)
            {
                float x = Mathf.Lerp(-halfWidth, halfWidth, xIndex / (float)widthSegments);
                vertices.Add(Point(x, z, false));
                uvs.Add(new Vector2(xIndex / (float)widthSegments, zIndex / (float)lengthSegments));
            }
        }

        for (int z = 0; z < lengthSegments; z++)
        {
            for (int x = 0; x < widthSegments; x++)
            {
                int t00 = TopIndex(x, z);
                int t10 = TopIndex(x + 1, z);
                int t01 = TopIndex(x, z + 1);
                int t11 = TopIndex(x + 1, z + 1);
                triangles.Add(t00); triangles.Add(t01); triangles.Add(t11);
                triangles.Add(t00); triangles.Add(t11); triangles.Add(t10);

                int b00 = BottomIndex(x, z);
                int b10 = BottomIndex(x + 1, z);
                int b01 = BottomIndex(x, z + 1);
                int b11 = BottomIndex(x + 1, z + 1);
                triangles.Add(b00); triangles.Add(b11); triangles.Add(b01);
                triangles.Add(b00); triangles.Add(b10); triangles.Add(b11);
            }
        }

        void Quad(int a, int b, int c, int d)
        {
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(a); triangles.Add(c); triangles.Add(d);
        }

        for (int z = 0; z < lengthSegments; z++)
        {
            Quad(TopIndex(0, z), BottomIndex(0, z), BottomIndex(0, z + 1), TopIndex(0, z + 1));
            Quad(TopIndex(widthSegments, z), TopIndex(widthSegments, z + 1),
                BottomIndex(widthSegments, z + 1), BottomIndex(widthSegments, z));
        }

        for (int x = 0; x < widthSegments; x++)
        {
            Quad(TopIndex(x, 0), TopIndex(x + 1, 0), BottomIndex(x + 1, 0), BottomIndex(x, 0));
            Quad(TopIndex(x, lengthSegments), BottomIndex(x, lengthSegments),
                BottomIndex(x + 1, lengthSegments), TopIndex(x + 1, lengthSegments));
        }

        var mesh = new Mesh { name = "CrescentPlank" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }
}
