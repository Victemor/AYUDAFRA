using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Construye la malla de railes cilíndricos a partir de un chunk continuo.
/// </summary>
public static class RailMeshBuilder
{
    #region Constants

    /// <summary>
    /// Radio mínimo de seguridad para evitar anillos degenerados.
    /// </summary>
    private const float MinimumRailRadius = 0.01f;

    #endregion

    #region Public API

    /// <summary>
    /// Construye una malla cilíndrica de railes para un chunk rail.
    /// </summary>
    public static Mesh BuildRailMesh(
        TrackSurfaceChunkDefinition chunk,
        TrackGenerationProfile generationProfile)
    {
        Mesh mesh = new Mesh
        {
            name = chunk != null ? $"RailChunk_{chunk.ChunkIndex}" : "RailChunk_Empty",
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };

        if (chunk == null || chunk.Samples == null || chunk.Samples.Count < 2 || generationProfile == null)
        {
            return mesh;
        }

        int radialSegments = Mathf.Max(3, generationProfile.RailRadialSegments);

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        BuildSingleRailTube(
            chunk,
            generationProfile,
            radialSegments,
            -1f,
            vertices,
            uvs,
            triangles);

        BuildSingleRailTube(
            chunk,
            generationProfile,
            radialSegments,
            1f,
            vertices,
            uvs,
            triangles);

        mesh.SetVertices(vertices);
        mesh.subMeshCount = 1;
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    #endregion

    #region Tube Build

    /// <summary>
    /// Construye un cilindro extruido para un solo rail.
    /// </summary>
    private static void BuildSingleRailTube(
        TrackSurfaceChunkDefinition chunk,
        TrackGenerationProfile generationProfile,
        int radialSegments,
        float sideSign,
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles)
    {
        int ringCount = chunk.Samples.Count;
        int ringVertexCount = radialSegments;
        float totalDistance = Mathf.Max(0.001f, chunk.EndDistance - chunk.StartDistance);
        int startVertexIndex = vertices.Count;

        for (int i = 0; i < ringCount; i++)
        {
            TrackLayoutSamplePoint sample = chunk.Samples[i];

            ResolveRailFrame(
                sample,
                generationProfile,
                sideSign,
                chunk.StartDistance,
                chunk.EndDistance,
                out Vector3 center,
                out Vector3 right,
                out Vector3 up,
                out float radius);

            float v = (sample.Distance - chunk.StartDistance) / totalDistance;

            for (int s = 0; s < radialSegments; s++)
            {
                float angle01 = s / (float)radialSegments;
                float angleRadians = angle01 * Mathf.PI * 2f;

                Vector3 radial =
                    (right * Mathf.Cos(angleRadians))
                    + (up * Mathf.Sin(angleRadians));

                vertices.Add(center + (radial * radius));
                uvs.Add(new Vector2(angle01, v));
            }
        }

        for (int ring = 0; ring < ringCount - 1; ring++)
        {
            int ringAStart = startVertexIndex + (ring * ringVertexCount);
            int ringBStart = startVertexIndex + ((ring + 1) * ringVertexCount);

            for (int s = 0; s < radialSegments; s++)
            {
                int next = (s + 1) % radialSegments;

                int a0 = ringAStart + s;
                int a1 = ringAStart + next;
                int b0 = ringBStart + s;
                int b1 = ringBStart + next;

                triangles.Add(a0);
                triangles.Add(b0);
                triangles.Add(b1);

                triangles.Add(a0);
                triangles.Add(b1);
                triangles.Add(a1);
            }
        }
    }

    #endregion

    #region Frame

    /// <summary>
    /// Resuelve centro, frame y radio constante del rail.
    /// Usa valores constantes del perfil para evitar que los extremos converjan al centro.
    /// </summary>
    private static void ResolveRailFrame(
        TrackLayoutSamplePoint sample,
        TrackGenerationProfile generationProfile,
        float sideSign,
        float chunkStartDistance,
        float chunkEndDistance,
        out Vector3 center,
        out Vector3 right,
        out Vector3 up,
        out float radius)
    {
        Vector3 forward = sample.Forward.sqrMagnitude >= 0.0001f
            ? sample.Forward.normalized
            : Vector3.forward;

        right = sample.Right.sqrMagnitude >= 0.0001f
            ? sample.Right.normalized
            : Vector3.right;

        if (Vector3.Dot(Vector3.Cross(forward, right), Vector3.up) < 0f)
        {
            right = -right;
        }

        up = Vector3.Cross(forward, right).normalized;

        if (up.sqrMagnitude < 0.0001f)
        {
            up = Vector3.up;
            right = Vector3.Cross(up, forward).normalized;
        }

        float lateralOffset = generationProfile.RailSeparation * 0.5f * sideSign;
        float verticalOffset = ResolveRailVerticalOffset(
            sample.Distance,
            chunkStartDistance,
            chunkEndDistance,
            generationProfile);

        radius = Mathf.Max(MinimumRailRadius, generationProfile.RailWidth * 0.5f);
        center = sample.Position + (right * lateralOffset) + (Vector3.up * verticalOffset);
    }

    /// <summary>
    /// Resuelve el offset vertical del rail sin deformar su separación ni su radio.
    /// </summary>
    private static float ResolveRailVerticalOffset(
        float sampleDistance,
        float chunkStartDistance,
        float chunkEndDistance,
        TrackGenerationProfile generationProfile)
    {
        float totalLength = Mathf.Max(0.001f, chunkEndDistance - chunkStartDistance);
        float t = Mathf.Clamp01((sampleDistance - chunkStartDistance) / totalLength);

        return Mathf.Lerp(
            generationProfile.RailEntryVerticalOffset,
            generationProfile.RailExitVerticalOffset,
            t);
    }

    #endregion
}