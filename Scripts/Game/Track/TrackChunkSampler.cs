using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Utilidad estática para samplear un <see cref="TrackSurfaceChunkDefinition"/> por distancia.
///
/// Centraliza la lógica de interpolación lineal entre samples que estaba duplicada en:
/// <list type="bullet">
///   <item><c>TrackBarrierGenerator.SampleChunkAtDistance</c></item>
///   <item><c>InfiniteLevelManager.TrySampleChunkAtDistance</c></item>
/// </list>
///
/// Devuelve un <see cref="TrackLayoutSamplePoint"/> completo con todas las propiedades
/// interpoladas: posición, forward, right, ancho, separación de raíles, etc.
/// Los consumidores que solo necesiten posición y forward extraen esas propiedades.
/// </summary>
public static class TrackChunkSampler
{
    private const float MinSegmentLength = 0.0001f;

    /// <summary>
    /// Samplea el chunk a la distancia indicada, interpolando linealmente entre los dos
    /// samples adyacentes más cercanos.
    ///
    /// Si la distancia está fuera del rango del chunk, devuelve el sample extremo más próximo.
    /// Si el chunk no tiene samples válidos, devuelve <c>default</c>.
    /// </summary>
    /// <param name="chunk">Chunk a samplear.</param>
    /// <param name="distance">Distancia global acumulada.</param>
    public static TrackLayoutSamplePoint SampleAtDistance(
        TrackSurfaceChunkDefinition chunk,
        float                       distance)
    {
        if (chunk?.Samples == null || chunk.Samples.Count == 0)
            return default;

        IReadOnlyList<TrackLayoutSamplePoint> samples = chunk.Samples;

        if (samples.Count == 1)
            return samples[0];

        if (distance <= samples[0].Distance)
            return samples[0];

        int lastIndex = samples.Count - 1;

        if (distance >= samples[lastIndex].Distance)
            return samples[lastIndex];

        for (int i = 0; i < samples.Count - 1; i++)
        {
            TrackLayoutSamplePoint a = samples[i];
            TrackLayoutSamplePoint b = samples[i + 1];

            if (distance < a.Distance || distance > b.Distance) continue;

            float length = Mathf.Max(MinSegmentLength, b.Distance - a.Distance);
            float t      = Mathf.Clamp01((distance - a.Distance) / length);

            Vector3 position       = Vector3.Lerp(a.Position, b.Position, t);
            Vector3 forward        = Vector3.Slerp(a.Forward, b.Forward, t).normalized;
            Vector3 right          = Vector3.Slerp(a.Right,   b.Right,   t).normalized;
            float   width          = Mathf.Lerp(a.Width,          b.Width,          t);
            float   railSeparation = Mathf.Lerp(a.RailSeparation, b.RailSeparation, t);
            float   railWidth      = Mathf.Lerp(a.RailWidth,      b.RailWidth,      t);

            return new TrackLayoutSamplePoint(
                position,
                forward,
                right,
                width,
                distance,
                a.StructureType,
                railSeparation,
                railWidth);
        }

        return samples[lastIndex];
    }
}