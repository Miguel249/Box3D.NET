// SPDX-License-Identifier: MIT

using System;
using System.Numerics;

namespace Box3D.Visualizer.Rendering;

/// <summary>
/// Turns the analytic shapes Box3D describes with a handful of floats into
/// triangles.
/// </summary>
/// <remarks>
/// Spheres and capsules arrive whole through <c>DebugShape</c> precisely so
/// that a renderer can pick its own resolution here. Everything else - hulls,
/// meshes, height fields - is already triangles or polygons and is read out of
/// the engine instead; see <c>ShapeMeshFactory</c>.
/// </remarks>
internal static class Tessellate
{
    /// <summary>Builds a sphere with smooth normals.</summary>
    /// <param name="center">The centre, in shape-local space.</param>
    /// <param name="radius">The radius.</param>
    /// <param name="segments">The number of divisions around the equator.</param>
    /// <param name="rings">The number of divisions from pole to pole.</param>
    /// <returns>The mesh.</returns>
    public static Mesh Sphere(Vector3 center, float radius, int segments = 24, int rings = 14)
    {
        var builder = new MeshBuilder();

        for (int ring = 0; ring <= rings; ring++)
        {
            float phi = (ring / (float)rings * MathF.PI) - (MathF.PI * 0.5f);
            float y = MathF.Sin(phi);
            float r = MathF.Cos(phi);

            for (int segment = 0; segment <= segments; segment++)
            {
                float theta = segment / (float)segments * MathF.Tau;
                Vector3 normal = new(r * MathF.Cos(theta), y, r * MathF.Sin(theta));

                builder.AddVertex(center + (normal * radius), normal);
            }
        }

        int stride = segments + 1;
        for (int ring = 0; ring < rings; ring++)
        {
            for (int segment = 0; segment < segments; segment++)
            {
                int a = (ring * stride) + segment;
                int b = a + stride;

                builder.AddQuad(a, b, b + 1, a + 1);
            }
        }

        return builder.Build()!;
    }

    /// <summary>Builds a capsule with smooth normals.</summary>
    /// <param name="start">The centre of one hemisphere.</param>
    /// <param name="end">The centre of the other hemisphere.</param>
    /// <param name="radius">The radius.</param>
    /// <param name="segments">The number of divisions around the axis.</param>
    /// <param name="rings">The number of divisions in each cap.</param>
    /// <returns>The mesh.</returns>
    public static Mesh Capsule(Vector3 start, Vector3 end, float radius, int segments = 24, int rings = 7)
    {
        Vector3 axis = end - start;
        float length = axis.Length();

        // A capsule with coincident centres is a sphere, and normalising a zero
        // axis would produce NaNs that spread through every vertex.
        if (length <= 1e-6f)
        {
            return Sphere(start, radius, segments, rings * 2);
        }

        Vector3 w = axis / length;
        Basis(w, out Vector3 u, out Vector3 v);

        var builder = new MeshBuilder();
        int rowCount = (rings + 1) * 2;

        // Two hemispheres, walked as one strip. The seam between them is the
        // cylinder: both rows sit on the equator, one about each centre.
        for (int row = 0; row < rowCount; row++)
        {
            bool upper = row > rings;
            int index = upper ? row - rings - 1 : row;
            float phi = index / (float)rings * (MathF.PI * 0.5f);

            Vector3 centre = upper ? end : start;
            float along = upper ? MathF.Sin(phi) : -MathF.Cos(phi);
            float around = upper ? MathF.Cos(phi) : MathF.Sin(phi);

            for (int segment = 0; segment <= segments; segment++)
            {
                float theta = segment / (float)segments * MathF.Tau;
                Vector3 normal = (w * along) + (((u * MathF.Cos(theta)) + (v * MathF.Sin(theta))) * around);

                builder.AddVertex(centre + (normal * radius), normal);
            }
        }

        int stride = segments + 1;
        for (int row = 0; row < rowCount - 1; row++)
        {
            for (int segment = 0; segment < segments; segment++)
            {
                int a = (row * stride) + segment;
                int b = a + stride;

                builder.AddQuad(a, b, b + 1, a + 1);
            }
        }

        return builder.Build()!;
    }

    /// <summary>Completes a unit vector into an orthonormal basis.</summary>
    /// <param name="w">The unit vector to build around.</param>
    /// <param name="u">The first perpendicular axis.</param>
    /// <param name="v">The second perpendicular axis.</param>
    public static void Basis(Vector3 w, out Vector3 u, out Vector3 v)
    {
        // Crossing with whichever world axis w is least aligned with keeps the
        // result well conditioned; crossing with a parallel axis gives zero.
        Vector3 reference = MathF.Abs(w.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX;

        u = Vector3.Normalize(Vector3.Cross(reference, w));
        v = Vector3.Cross(w, u);
    }
}
