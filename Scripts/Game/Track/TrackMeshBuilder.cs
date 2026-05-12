using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Construye la malla final del track a partir de chunks de superficie continua.
/// 
/// Responsabilidades:
/// - Generar submeshes separados por material en track sólido.
/// - Construir railes cilíndricos usando RailMeshBuilder.
/// - Proteger la construcción ante segmentos degenerados.
/// </summary>
public static class TrackMeshBuilder
{
    #region Nested Types

    /// <summary>
    /// Resultado de construir un chunk de malla.
    /// </summary>
    public readonly struct TrackMeshBuildResult
    {
        /// <summary>
        /// Malla generada.
        /// </summary>
        public Mesh Mesh { get; }

        /// <summary>
        /// Materiales asociados a la malla.
        /// </summary>
        public Material[] Materials { get; }

        /// <summary>
        /// Crea un nuevo resultado de construcción.
        /// </summary>
        public TrackMeshBuildResult(Mesh mesh, Material[] materials)
        {
            Mesh = mesh;
            Materials = materials;
        }
    }

    #endregion

    #region Constants

    /// <summary>
    /// Longitud mínima de segmento para construir geometría.
    /// </summary>
    private const float MinimumSegmentLength = 0.01f;

    #endregion

    #region Public API

    /// <summary>
    /// Construye una malla para un chunk continuo del track.
    /// </summary>
    public static TrackMeshBuildResult BuildChunkMesh(
        TrackSurfaceChunkDefinition chunk,
        TrackGenerationProfile generationProfile)
    {
        if (chunk == null || chunk.Samples == null || chunk.Samples.Count < 2)
        {
            Mesh emptyMesh = new Mesh
            {
                name = "EmptyTrackChunk",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };

            return new TrackMeshBuildResult(emptyMesh, ResolveSolidMaterials(generationProfile));
        }

        switch (chunk.StructureType)
        {
            case TrackStructureType.RailTrack:
                return BuildRailChunk(chunk, generationProfile);

            case TrackStructureType.SolidTrack:
            default:
                return BuildSolidChunk(chunk, generationProfile);
        }
    }

    #endregion

    #region Solid Chunk

    /// <summary>
    /// Construye un chunk sólido tradicional.
    /// </summary>
    private static TrackMeshBuildResult BuildSolidChunk(
        TrackSurfaceChunkDefinition chunk,
        TrackGenerationProfile generationProfile)
    {
        Mesh mesh = new Mesh
        {
            name = $"TrackChunk_{chunk.ChunkIndex}",
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };

        BuildSolidChunkMesh(mesh, chunk, generationProfile);

        return new TrackMeshBuildResult(
            mesh,
            ResolveSolidMaterials(generationProfile));
    }

    /// <summary>
    /// Construye la malla de un chunk sólido tradicional.
    /// </summary>
    private static void BuildSolidChunkMesh(
        Mesh mesh,
        TrackSurfaceChunkDefinition chunk,
        TrackGenerationProfile generationProfile)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();

        List<int> topCenterTriangles = new List<int>();
        List<int> topBorderTriangles = new List<int>();
        List<int> bottomTriangles = new List<int>();
        List<int> sideTriangles = new List<int>();

        float borderRatio = 0.18f;
        float thickness = generationProfile.TrackThickness;
        float accumulatedV = 0f;

        for (int i = 0; i < chunk.Samples.Count - 1; i++)
        {
            TrackLayoutSamplePoint a = chunk.Samples[i];
            TrackLayoutSamplePoint b = chunk.Samples[i + 1];

            if (!TryResolveSegmentFrame(a, b, out Vector3 rightA, out Vector3 rightB, out float segmentLength))
            {
                continue;
            }

            float v0 = accumulatedV;
            float v1 = accumulatedV + segmentLength;
            accumulatedV = v1;

            float halfWidthA = a.Width * 0.5f;
            float halfWidthB = b.Width * 0.5f;
            float borderWidthA = a.Width * borderRatio;
            float borderWidthB = b.Width * borderRatio;

            Vector3 aTopLeft = a.Position - (rightA * halfWidthA);
            Vector3 aTopRight = a.Position + (rightA * halfWidthA);
            Vector3 bTopLeft = b.Position - (rightB * halfWidthB);
            Vector3 bTopRight = b.Position + (rightB * halfWidthB);

            Vector3 aTopInnerLeft = a.Position - (rightA * (halfWidthA - borderWidthA));
            Vector3 aTopInnerRight = a.Position + (rightA * (halfWidthA - borderWidthA));
            Vector3 bTopInnerLeft = b.Position - (rightB * (halfWidthB - borderWidthB));
            Vector3 bTopInnerRight = b.Position + (rightB * (halfWidthB - borderWidthB));

            Vector3 aBottomLeft = aTopLeft - (Vector3.up * thickness);
            Vector3 aBottomRight = aTopRight - (Vector3.up * thickness);
            Vector3 bBottomLeft = bTopLeft - (Vector3.up * thickness);
            Vector3 bBottomRight = bTopRight - (Vector3.up * thickness);

            AddQuad(
                vertices, uvs, topCenterTriangles,
                aTopInnerLeft, bTopInnerLeft, bTopInnerRight, aTopInnerRight,
                new Vector2(0f, v0), new Vector2(0f, v1), new Vector2(1f, v1), new Vector2(1f, v0));

            AddQuad(
                vertices, uvs, topBorderTriangles,
                aTopLeft, bTopLeft, bTopInnerLeft, aTopInnerLeft,
                new Vector2(0f, v0), new Vector2(0f, v1), new Vector2(1f, v1), new Vector2(1f, v0));

            AddQuad(
                vertices, uvs, topBorderTriangles,
                aTopInnerRight, bTopInnerRight, bTopRight, aTopRight,
                new Vector2(0f, v0), new Vector2(0f, v1), new Vector2(1f, v1), new Vector2(1f, v0));

            AddQuad(
                vertices, uvs, bottomTriangles,
                aBottomRight, bBottomRight, bBottomLeft, aBottomLeft,
                new Vector2(0f, v0), new Vector2(0f, v1), new Vector2(1f, v1), new Vector2(1f, v0));

            AddQuad(
                vertices, uvs, sideTriangles,
                aTopLeft, aBottomLeft, bBottomLeft, bTopLeft,
                new Vector2(0f, v0), new Vector2(1f, v0), new Vector2(1f, v1), new Vector2(0f, v1));

            AddQuad(
                vertices, uvs, sideTriangles,
                bTopRight, bBottomRight, aBottomRight, aTopRight,
                new Vector2(0f, v1), new Vector2(1f, v1), new Vector2(1f, v0), new Vector2(0f, v0));
        }

        AddSolidCaps(
            chunk,
            generationProfile,
            vertices,
            uvs,
            sideTriangles);

        mesh.SetVertices(vertices);
        mesh.subMeshCount = 4;
        mesh.SetTriangles(topCenterTriangles, 0);
        mesh.SetTriangles(topBorderTriangles, 1);
        mesh.SetTriangles(bottomTriangles, 2);
        mesh.SetTriangles(sideTriangles, 3);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    /// <summary>
    /// Añade tapas en los extremos de un chunk sólido.
    /// </summary>
    private static void AddSolidCaps(
        TrackSurfaceChunkDefinition chunk,
        TrackGenerationProfile generationProfile,
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> sideTriangles)
    {
        if (chunk.Samples.Count < 2)
        {
            return;
        }

        float thickness = generationProfile.TrackThickness;

        AddSolidEndCap(chunk.Samples[0], thickness, vertices, uvs, sideTriangles, true);
        AddSolidEndCap(chunk.Samples[chunk.Samples.Count - 1], thickness, vertices, uvs, sideTriangles, false);
    }

    /// <summary>
    /// Añade una tapa de extremo para track sólido.
    /// </summary>
    private static void AddSolidEndCap(
        TrackLayoutSamplePoint sample,
        float thickness,
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles,
        bool isStart)
    {
        Vector3 right = ResolveSafeRight(sample.Right, sample.Forward);
        float halfWidth = sample.Width * 0.5f;

        Vector3 topLeft = sample.Position - (right * halfWidth);
        Vector3 topRight = sample.Position + (right * halfWidth);
        Vector3 bottomLeft = topLeft - (Vector3.up * thickness);
        Vector3 bottomRight = topRight - (Vector3.up * thickness);

        if (isStart)
        {
            AddQuad(
                vertices, uvs, triangles,
                topRight, bottomRight, bottomLeft, topLeft,
                new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f));
        }
        else
        {
            AddQuad(
                vertices, uvs, triangles,
                topLeft, bottomLeft, bottomRight, topRight,
                new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f));
        }
    }

    #endregion

    #region Rail Chunk

    /// <summary>
    /// Construye un chunk de railes cilíndricos.
    /// </summary>
    private static TrackMeshBuildResult BuildRailChunk(
        TrackSurfaceChunkDefinition chunk,
        TrackGenerationProfile generationProfile)
    {
        Mesh mesh = RailMeshBuilder.BuildRailMesh(chunk, generationProfile);

        return new TrackMeshBuildResult(
            mesh,
            ResolveRailMaterials(generationProfile));
    }

    #endregion

    #region Segment Safety

    /// <summary>
    /// Resuelve un frame seguro para construir un segmento entre dos samples.
    /// </summary>
    private static bool TryResolveSegmentFrame(
        TrackLayoutSamplePoint a,
        TrackLayoutSamplePoint b,
        out Vector3 rightA,
        out Vector3 rightB,
        out float segmentLength)
    {
        Vector3 segment = b.Position - a.Position;
        segmentLength = segment.magnitude;

        rightA = Vector3.right;
        rightB = Vector3.right;

        if (segmentLength < MinimumSegmentLength)
        {
            return false;
        }

        rightA = ResolveSafeRight(a.Right, a.Forward);
        rightB = ResolveSafeRight(b.Right, b.Forward);

        if (Vector3.Dot(rightA, rightB) < 0f)
        {
            rightB = -rightB;
        }

        return true;
    }

    /// <summary>
    /// Resuelve un vector lateral seguro para construir geometría.
    /// </summary>
    private static Vector3 ResolveSafeRight(Vector3 right, Vector3 forward)
    {
        if (right.sqrMagnitude >= 0.0001f)
        {
            return right.normalized;
        }

        Vector3 horizontalForward = new Vector3(forward.x, 0f, forward.z);
        if (horizontalForward.sqrMagnitude < 0.0001f)
        {
            return Vector3.right;
        }

        horizontalForward.Normalize();
        Vector3 fallbackRight = Vector3.Cross(Vector3.up, horizontalForward);

        if (fallbackRight.sqrMagnitude < 0.0001f)
        {
            return Vector3.right;
        }

        return fallbackRight.normalized;
    }

    #endregion

    #region Common Helpers

    /// <summary>
    /// Añade un quad al submesh indicado.
    /// </summary>
    private static void AddQuad(
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles,
        Vector3 v0,
        Vector3 v1,
        Vector3 v2,
        Vector3 v3,
        Vector2 uv0,
        Vector2 uv1,
        Vector2 uv2,
        Vector2 uv3)
    {
        int startIndex = vertices.Count;

        vertices.Add(v0);
        vertices.Add(v1);
        vertices.Add(v2);
        vertices.Add(v3);

        uvs.Add(uv0);
        uvs.Add(uv1);
        uvs.Add(uv2);
        uvs.Add(uv3);

        triangles.Add(startIndex + 0);
        triangles.Add(startIndex + 1);
        triangles.Add(startIndex + 2);

        triangles.Add(startIndex + 0);
        triangles.Add(startIndex + 2);
        triangles.Add(startIndex + 3);
    }

    /// <summary>
    /// Resuelve materiales del chunk sólido.
    /// </summary>
    private static Material[] ResolveSolidMaterials(TrackGenerationProfile generationProfile)
    {
        return new[]
        {
            generationProfile.TopCenterMaterial,
            generationProfile.TopBorderMaterial,
            generationProfile.BottomMaterial,
            generationProfile.SideMaterial
        };
    }

    /// <summary>
    /// Resuelve materiales del chunk rail.
    /// </summary>
    private static Material[] ResolveRailMaterials(TrackGenerationProfile generationProfile)
    {
        Material railMaterial = generationProfile.RailMaterial != null
            ? generationProfile.RailMaterial
            : generationProfile.TopCenterMaterial;

        return new[]
        {
            railMaterial
        };
    }

    #endregion
}