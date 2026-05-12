using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Permite samplear la trayectoria final del track por distancia acumulada.
/// 
/// Responsabilidades:
/// - Construir una representación consultable del path.
/// - Interpolar posición, forward y right por distancia.
/// - Exponer una API runtime para futuros sistemas de spawn.
/// </summary>
public sealed class TrackPathSampler
{
    #region Private Types

    /// <summary>
    /// Nodo interno de sampleo.
    /// </summary>
    private readonly struct PathNode
    {
        public TrackLayoutSamplePoint Sample { get; }

        public PathNode(TrackLayoutSamplePoint sample)
        {
            Sample = sample;
        }
    }

    #endregion

    #region Private Fields

    private readonly List<PathNode> nodes = new List<PathNode>();

    #endregion

    #region Properties

    /// <summary>
    /// Distancia total disponible en el sampler.
    /// </summary>
    public float TotalDistance
    {
        get
        {
            if (nodes.Count == 0)
            {
                return 0f;
            }

            return nodes[nodes.Count - 1].Sample.Distance;
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Reconstruye el sampler a partir de los chunks de superficie.
    /// </summary>
    /// <param name="chunks">Chunks sólidos del nivel.</param>
    public void Rebuild(IReadOnlyList<TrackSurfaceChunkDefinition> chunks)
    {
        nodes.Clear();

        if (chunks == null)
        {
            return;
        }

        for (int i = 0; i < chunks.Count; i++)
        {
            IReadOnlyList<TrackLayoutSamplePoint> samples = chunks[i].Samples;

            for (int j = 0; j < samples.Count; j++)
            {
                AddNodeIfNeeded(samples[j]);
            }
        }
    }

    /// <summary>
    /// Devuelve un sample interpolado de trayectoria para una distancia dada.
    /// </summary>
    /// <param name="distance">Distancia acumulada solicitada.</param>
    /// <returns>Sample interpolado.</returns>
    public TrackSample SampleAtDistance(float distance)
    {
        if (nodes.Count == 0)
        {
            return new TrackSample(Vector3.zero, Vector3.forward, Vector3.right, 0f);
        }

        if (nodes.Count == 1)
        {
            TrackLayoutSamplePoint single = nodes[0].Sample;
            return new TrackSample(single.Position, single.Forward, single.Right, single.Distance);
        }

        float clampedDistance = Mathf.Clamp(distance, 0f, TotalDistance);

        for (int i = 0; i < nodes.Count - 1; i++)
        {
            TrackLayoutSamplePoint a = nodes[i].Sample;
            TrackLayoutSamplePoint b = nodes[i + 1].Sample;

            if (clampedDistance < a.Distance || clampedDistance > b.Distance)
            {
                continue;
            }

            float range = Mathf.Max(0.0001f, b.Distance - a.Distance);
            float t = Mathf.Clamp01((clampedDistance - a.Distance) / range);

            Vector3 position = Vector3.Lerp(a.Position, b.Position, t);
            Vector3 forward = Vector3.Slerp(a.Forward, b.Forward, t).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            return new TrackSample(position, forward, right, clampedDistance);
        }

        TrackLayoutSamplePoint last = nodes[nodes.Count - 1].Sample;
        return new TrackSample(last.Position, last.Forward, last.Right, last.Distance);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Añade un nodo si no está duplicando el anterior.
    /// </summary>
    private void AddNodeIfNeeded(TrackLayoutSamplePoint sample)
    {
        if (nodes.Count == 0)
        {
            nodes.Add(new PathNode(sample));
            return;
        }

        TrackLayoutSamplePoint last = nodes[nodes.Count - 1].Sample;

        if (Vector3.Distance(last.Position, sample.Position) <= 0.001f)
        {
            return;
        }

        nodes.Add(new PathNode(sample));
    }

    #endregion
}