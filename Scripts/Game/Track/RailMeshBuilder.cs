using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Construye la malla de railes cilíndricos a partir de un chunk continuo.
///
/// Fix de unión entre chunks:
/// Los offsets verticales de entrada (<c>RailEntryVerticalOffset</c>) y salida
/// (<c>RailExitVerticalOffset</c>) solo se aplican en los extremos de la SECUENCIA
/// completa de raíles, no en cada chunk individual. Los chunks intermedios dentro de
/// una misma secuencia usan offset 0 en ambos extremos, garantizando que el seam entre
/// chunks adyacentes esté siempre a la misma altura.
/// </summary>
public static class RailMeshBuilder
{
    #region Constants

    private const float MinimumRailRadius = 0.01f;

    #endregion

    #region Public API

    /// <summary>
    /// Construye una malla cilíndrica de railes para un chunk rail.
    /// </summary>
    /// <param name="chunk">Chunk de datos del rail.</param>
    /// <param name="generationProfile">Perfil de generación con geometría y materiales.</param>
    /// <param name="isFirstInRailSequence">
    ///   <c>true</c> si no hay otro chunk de rail inmediatamente antes de este.
    ///   Habilita <c>RailEntryVerticalOffset</c> en el extremo inicial.
    /// </param>
    /// <param name="isLastInRailSequence">
    ///   <c>true</c> si no hay otro chunk de rail inmediatamente después de este.
    ///   Habilita <c>RailExitVerticalOffset</c> en el extremo final.
    /// </param>
    public static Mesh BuildRailMesh(
        TrackSurfaceChunkDefinition chunk,
        TrackGenerationProfile      generationProfile,
        bool                        isFirstInRailSequence = true,
        bool                        isLastInRailSequence  = true)
    {
        Mesh mesh = new Mesh
        {
            name         = chunk != null ? $"RailChunk_{chunk.ChunkIndex}" : "RailChunk_Empty",
            indexFormat  = UnityEngine.Rendering.IndexFormat.UInt32,
        };

        if (chunk == null || chunk.Samples == null || chunk.Samples.Count < 2 || generationProfile == null)
            return mesh;

        int radialSegments = Mathf.Max(3, generationProfile.RailRadialSegments);

        List<Vector3> vertices  = new List<Vector3>();
        List<Vector2> uvs       = new List<Vector2>();
        List<int>     triangles = new List<int>();

        BuildSingleRailTube(chunk, generationProfile, radialSegments, -1f,
            vertices, uvs, triangles, isFirstInRailSequence, isLastInRailSequence);

        BuildSingleRailTube(chunk, generationProfile, radialSegments,  1f,
            vertices, uvs, triangles, isFirstInRailSequence, isLastInRailSequence);

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
    /// Construye un cilindro extruido para un solo rail (izquierdo o derecho).
    /// </summary>
    private static void BuildSingleRailTube(
        TrackSurfaceChunkDefinition chunk,
        TrackGenerationProfile      generationProfile,
        int                         radialSegments,
        float                       sideSign,
        List<Vector3>               vertices,
        List<Vector2>               uvs,
        List<int>                   triangles,
        bool                        isFirstInRailSequence,
        bool                        isLastInRailSequence)
    {
        int   ringCount       = chunk.Samples.Count;
        int   ringVertexCount = radialSegments;
        float totalDistance   = Mathf.Max(0.001f, chunk.EndDistance - chunk.StartDistance);
        int   startVertexIndex = vertices.Count;

        for (int i = 0; i < ringCount; i++)
        {
            TrackLayoutSamplePoint sample = chunk.Samples[i];

            ResolveRailFrame(
                sample,
                generationProfile,
                sideSign,
                chunk.StartDistance,
                chunk.EndDistance,
                isFirstInRailSequence,
                isLastInRailSequence,
                out Vector3 center,
                out Vector3 right,
                out Vector3 up,
                out float   radius);

            float v = (sample.Distance - chunk.StartDistance) / totalDistance;

            for (int s = 0; s < radialSegments; s++)
            {
                float   angle01      = s / (float)radialSegments;
                float   angleRadians = angle01 * Mathf.PI * 2f;
                Vector3 radial       = (right * Mathf.Cos(angleRadians))
                                     + (up    * Mathf.Sin(angleRadians));

                vertices.Add(center + radial * radius);
                uvs.Add(new Vector2(angle01, v));
            }
        }

        for (int ring = 0; ring < ringCount - 1; ring++)
        {
            int ringAStart = startVertexIndex + (ring       * ringVertexCount);
            int ringBStart = startVertexIndex + ((ring + 1) * ringVertexCount);

            for (int s = 0; s < radialSegments; s++)
            {
                int next = (s + 1) % radialSegments;

                int a0 = ringAStart + s;
                int a1 = ringAStart + next;
                int b0 = ringBStart + s;
                int b1 = ringBStart + next;

                triangles.Add(a0); triangles.Add(b0); triangles.Add(b1);
                triangles.Add(a0); triangles.Add(b1); triangles.Add(a1);
            }
        }
    }

    #endregion

    #region Frame

    /// <summary>
    /// Resuelve centro, frame y radio de un anillo del rail.
    ///
    /// El offset vertical solo se aplica en los extremos de la SECUENCIA completa
    /// de raíles. Si <paramref name="isFirstInRailSequence"/> es <c>false</c>, el
    /// offset de entrada se fuerza a 0, garantizando que el seam con el chunk
    /// anterior esté a la misma altura. Ídem para la salida.
    /// </summary>
    private static void ResolveRailFrame(
        TrackLayoutSamplePoint sample,
        TrackGenerationProfile generationProfile,
        float                  sideSign,
        float                  chunkStartDistance,
        float                  chunkEndDistance,
        bool                   isFirstInRailSequence,
        bool                   isLastInRailSequence,
        out Vector3            center,
        out Vector3            right,
        out Vector3            up,
        out float              radius)
    {
        // — Calcular frame ortogonal —
        Vector3 forward = sample.Forward.sqrMagnitude >= 0.0001f
            ? sample.Forward.normalized
            : Vector3.forward;

        right = sample.Right.sqrMagnitude >= 0.0001f
            ? sample.Right.normalized
            : Vector3.right;

        if (Vector3.Dot(Vector3.Cross(forward, right), Vector3.up) < 0f)
            right = -right;

        up = Vector3.Cross(forward, right).normalized;

        if (up.sqrMagnitude < 0.0001f)
        {
            up    = Vector3.up;
            right = Vector3.Cross(up, forward).normalized;
        }

        // — Offset vertical condicionado al contexto de secuencia —
        float lateralOffset  = generationProfile.RailSeparation * 0.5f * sideSign;
        float verticalOffset = ResolveRailVerticalOffset(
            sample.Distance,
            chunkStartDistance,
            chunkEndDistance,
            generationProfile,
            isFirstInRailSequence,
            isLastInRailSequence);

        radius = Mathf.Max(MinimumRailRadius, generationProfile.RailWidth * 0.5f);
        center = sample.Position + (right * lateralOffset) + (Vector3.up * verticalOffset);
    }

    /// <summary>
    /// Interpola el offset vertical a lo largo del chunk.
    ///
    /// Los valores de entrada y salida se activan solo en los extremos de la secuencia
    /// de raíles completa, no en los límites de cada chunk individual.
    ///
    /// Ejemplo con dos chunks adyacentes [A][B] dentro de la misma secuencia de raíles:
    /// <list type="bullet">
    ///   <item>Chunk A: isFirst=true, isLast=false → entry=-0.08, exit=0</item>
    ///   <item>Chunk B: isFirst=false, isLast=true → entry=0, exit=+0.06</item>
    ///   <item>Seam A/B: Chunk A termina en 0, Chunk B empieza en 0 → sin discontinuidad.</item>
    /// </list>
    /// </summary>
    private static float ResolveRailVerticalOffset(
        float                  sampleDistance,
        float                  chunkStartDistance,
        float                  chunkEndDistance,
        TrackGenerationProfile generationProfile,
        bool                   isFirstInRailSequence,
        bool                   isLastInRailSequence)
    {
        float totalLength = Mathf.Max(0.001f, chunkEndDistance - chunkStartDistance);
        float t           = Mathf.Clamp01((sampleDistance - chunkStartDistance) / totalLength);

        // Solo aplicar los offsets en los extremos de la secuencia real.
        // En chunks internos los offsets son 0, garantizando costura continua.
        float entryOffset = isFirstInRailSequence ? generationProfile.RailEntryVerticalOffset : 0f;
        float exitOffset  = isLastInRailSequence  ? generationProfile.RailExitVerticalOffset  : 0f;

        return Mathf.Lerp(entryOffset, exitOffset, t);
    }

    #endregion
}