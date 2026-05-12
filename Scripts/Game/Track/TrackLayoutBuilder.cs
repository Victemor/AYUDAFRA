using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Convierte la trayectoria continua del track en chunks de superficie sampleados.
/// </summary>
public static class TrackLayoutBuilder
{
    #region Constants

    /// <summary>
    /// Distancia máxima permitida para un chunk de superficie.
    /// Reducir este valor aumenta la precisión de sistemas que trabajan por chunk,
    /// como barreras, colliders y selección por porcentaje de pista.
    /// </summary>
    private const float MaxSurfaceChunkLength = 15f;

    /// <summary>
    /// Distancia mínima entre samples ordinarios dentro de un mismo chunk.
    /// </summary>
    private const float MinimumChunkPointSpacing = 0.01f;

    /// <summary>
    /// Distancia mínima usada únicamente para costuras forzadas entre estructuras o cortes internos.
    /// </summary>
    private const float MinimumForcedSeamSpacing = 0.0005f;

    #endregion

    #region Public API

    /// <summary>
    /// Construye chunks de superficie continua a partir de puntos continuos ya resueltos.
    /// </summary>
    public static List<TrackSurfaceChunkDefinition> BuildSurfaceChunks(
        IReadOnlyList<TrackSplinePoint> splinePoints)
    {
        List<TrackSurfaceChunkDefinition> chunks = new List<TrackSurfaceChunkDefinition>();

        if (splinePoints == null || splinePoints.Count < 2)
        {
            return chunks;
        }

        List<TrackLayoutSamplePoint> currentChunkSamples = null;
        TrackLayoutSamplePoint? pendingSeamSample = null;

        int chunkIndex = 0;
        float chunkStartDistance = 0f;
        TrackStructureType currentStructureType = TrackStructureType.SolidTrack;

        for (int i = 0; i < splinePoints.Count; i++)
        {
            TrackSplinePoint point = splinePoints[i];

            if (!point.HasSurface || point.StructureType == TrackStructureType.Gap)
            {
                FinalizeCurrentChunkIfValid(
                    chunks,
                    ref currentChunkSamples,
                    ref chunkIndex,
                    chunkStartDistance,
                    currentStructureType);

                pendingSeamSample = null;
                continue;
            }

            TrackLayoutSamplePoint currentSample = ConvertToLayoutSample(point);

            bool shouldStartNewChunk =
                currentChunkSamples == null
                || currentStructureType != point.StructureType;

            if (shouldStartNewChunk)
            {
                if (currentChunkSamples != null)
                {
                    AddSampleIfNeeded(
                        currentChunkSamples,
                        currentSample,
                        forceSeamSample: true);

                    FinalizeCurrentChunkIfValid(
                        chunks,
                        ref currentChunkSamples,
                        ref chunkIndex,
                        chunkStartDistance,
                        currentStructureType);

                    pendingSeamSample = currentSample;
                }

                StartNewChunk(
                    ref currentChunkSamples,
                    ref chunkStartDistance,
                    ref currentStructureType,
                    point.StructureType,
                    pendingSeamSample,
                    currentSample);

                pendingSeamSample = null;
                continue;
            }

            AddSampleIfNeeded(
                currentChunkSamples,
                currentSample,
                forceSeamSample: false);

            if (ShouldSplitCurrentChunk(currentChunkSamples, chunkStartDistance))
            {
                TrackLayoutSamplePoint seamSample = currentChunkSamples[currentChunkSamples.Count - 1];

                FinalizeCurrentChunkIfValid(
                    chunks,
                    ref currentChunkSamples,
                    ref chunkIndex,
                    chunkStartDistance,
                    currentStructureType);

                pendingSeamSample = seamSample;
            }
        }

        FinalizeCurrentChunkIfValid(
            chunks,
            ref currentChunkSamples,
            ref chunkIndex,
            chunkStartDistance,
            currentStructureType);

        return chunks;
    }

    #endregion

    #region Chunk Flow

    /// <summary>
    /// Inicializa un nuevo chunk usando una costura previa cuando existe.
    /// Esto permite cortar chunks largos sin abrir huecos entre mallas.
    /// </summary>
    private static void StartNewChunk(
        ref List<TrackLayoutSamplePoint> currentChunkSamples,
        ref float chunkStartDistance,
        ref TrackStructureType currentStructureType,
        TrackStructureType newStructureType,
        TrackLayoutSamplePoint? pendingSeamSample,
        TrackLayoutSamplePoint currentSample)
    {
        currentChunkSamples = new List<TrackLayoutSamplePoint>();
        currentStructureType = newStructureType;

        if (pendingSeamSample.HasValue)
        {
            TrackLayoutSamplePoint seamSample = pendingSeamSample.Value;
            chunkStartDistance = seamSample.Distance;

            AddSampleIfNeeded(
                currentChunkSamples,
                seamSample,
                forceSeamSample: true);
        }
        else
        {
            chunkStartDistance = currentSample.Distance;
        }

        AddSampleIfNeeded(
            currentChunkSamples,
            currentSample,
            forceSeamSample: false);
    }

    /// <summary>
    /// Indica si el chunk actual debe partirse por longitud máxima.
    /// </summary>
    private static bool ShouldSplitCurrentChunk(
        IReadOnlyList<TrackLayoutSamplePoint> currentChunkSamples,
        float chunkStartDistance)
    {
        if (currentChunkSamples == null || currentChunkSamples.Count < 2)
        {
            return false;
        }

        float currentEndDistance = currentChunkSamples[currentChunkSamples.Count - 1].Distance;
        float currentLength = currentEndDistance - chunkStartDistance;

        return currentLength >= MaxSurfaceChunkLength;
    }

    /// <summary>
    /// Finaliza el chunk actual si contiene suficientes samples válidos.
    /// </summary>
    private static void FinalizeCurrentChunkIfValid(
        List<TrackSurfaceChunkDefinition> chunks,
        ref List<TrackLayoutSamplePoint> currentChunkSamples,
        ref int chunkIndex,
        float chunkStartDistance,
        TrackStructureType structureType)
    {
        if (currentChunkSamples == null)
        {
            return;
        }

        if (currentChunkSamples.Count >= 2)
        {
            float endDistance = currentChunkSamples[currentChunkSamples.Count - 1].Distance;

            if (endDistance > chunkStartDistance)
            {
                chunks.Add(new TrackSurfaceChunkDefinition(
                    chunkIndex,
                    chunkStartDistance,
                    endDistance,
                    structureType,
                    currentChunkSamples));

                chunkIndex++;
            }
        }

        currentChunkSamples = null;
    }

    #endregion

    #region Sample Conversion

    /// <summary>
    /// Convierte un punto continuo a un sample final de layout.
    /// </summary>
    private static TrackLayoutSamplePoint ConvertToLayoutSample(TrackSplinePoint point)
    {
        return new TrackLayoutSamplePoint(
            point.Position,
            point.Forward,
            point.Right,
            point.Width,
            point.Distance,
            point.StructureType,
            point.RailSeparation,
            point.RailWidth);
    }

    /// <summary>
    /// Añade un sample evitando duplicados y micro-segmentos.
    /// Las costuras forzadas permiten compartir muestras entre chunks sin abrir huecos visuales.
    /// </summary>
    private static void AddSampleIfNeeded(
        List<TrackLayoutSamplePoint> target,
        TrackLayoutSamplePoint sample,
        bool forceSeamSample)
    {
        if (target == null)
        {
            return;
        }

        if (sample.Forward.sqrMagnitude < 0.0001f || sample.Right.sqrMagnitude < 0.0001f)
        {
            return;
        }

        if (target.Count == 0)
        {
            target.Add(sample);
            return;
        }

        TrackLayoutSamplePoint last = target[target.Count - 1];

        float positionDistance = Vector3.Distance(last.Position, sample.Position);
        float distanceDelta = Mathf.Abs(last.Distance - sample.Distance);

        if (positionDistance <= 0.0005f && distanceDelta <= 0.0005f)
        {
            return;
        }

        float requiredSpacing = forceSeamSample
            ? MinimumForcedSeamSpacing
            : MinimumChunkPointSpacing;

        if (positionDistance < requiredSpacing)
        {
            return;
        }

        target.Add(sample);
    }

    #endregion
}