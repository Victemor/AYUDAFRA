using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Construye la trayectoria continua global del track a partir de secciones lógicas.
/// 
/// Responsabilidades:
/// - Resolver cada sección en una curva continua local.
/// - Unificar todas las secciones en una trayectoria continua global.
/// - Reconstruir un frame transversal estable a lo largo de toda la trayectoria.
/// - Evitar flips de right entre puntos consecutivos.
/// </summary>
public static class TrackSplineBuilder
{
    #region Constants

    /// <summary>
    /// Distancia mínima entre puntos consecutivos para conservarlos.
    /// </summary>
    private const float MinimumPointSpacing = 0.005f;

    #endregion

    #region Public API

    /// <summary>
    /// Construye la lista global de puntos continuos del track.
    /// </summary>
    public static List<TrackSplinePoint> BuildSplinePoints(
        IReadOnlyList<TrackSectionDefinition> sections,
        TrackGenerationProfile generationProfile)
    {
        List<TrackSplinePoint> rawPoints = new List<TrackSplinePoint>();

        if (sections == null || sections.Count == 0 || generationProfile == null)
        {
            return rawPoints;
        }

        for (int i = 0; i < sections.Count; i++)
        {
            List<TrackSplinePoint> resolvedPoints =
                TrackSectionCurveResolver.ResolveSection(sections[i], generationProfile, i);

            AppendSectionPoints(rawPoints, resolvedPoints);
        }

        return BuildStableFramePoints(rawPoints);
    }

    #endregion

    #region Raw Append

    /// <summary>
    /// Añade los puntos de una sección evitando duplicados innecesarios.
    /// </summary>
    private static void AppendSectionPoints(
        List<TrackSplinePoint> target,
        List<TrackSplinePoint> sectionPoints)
    {
        if (sectionPoints == null || sectionPoints.Count == 0)
        {
            return;
        }

        if (target.Count == 0)
        {
            target.AddRange(sectionPoints);
            return;
        }

        TrackSplinePoint last = target[target.Count - 1];

        for (int i = 0; i < sectionPoints.Count; i++)
        {
            TrackSplinePoint current = sectionPoints[i];

            bool samePosition = Vector3.Distance(last.Position, current.Position) <= 0.0005f;
            bool sameStructure = last.StructureType == current.StructureType;
            bool sameSurfaceState = last.HasSurface == current.HasSurface;

            if (samePosition && sameStructure && sameSurfaceState)
            {
                continue;
            }

            target.Add(current);
            last = current;
        }
    }

    #endregion

    #region Stable Frame

    /// <summary>
    /// Reconstruye la trayectoria con un frame transversal estable.
    /// </summary>
    private static List<TrackSplinePoint> BuildStableFramePoints(
        List<TrackSplinePoint> rawPoints)
    {
        List<TrackSplinePoint> stablePoints = new List<TrackSplinePoint>();

        if (rawPoints == null || rawPoints.Count == 0)
        {
            return stablePoints;
        }

        Vector3 previousForward = ResolveValidForward(rawPoints, 0);
        Vector3 previousRight = ResolveInitialRight(previousForward);

        stablePoints.Add(CreateStablePoint(
            rawPoints[0],
            previousForward,
            previousRight));

        for (int i = 1; i < rawPoints.Count; i++)
        {
            TrackSplinePoint rawPoint = rawPoints[i];
            TrackSplinePoint previousStablePoint = stablePoints[stablePoints.Count - 1];

            float distance = Vector3.Distance(previousStablePoint.Position, rawPoint.Position);
            if (distance < MinimumPointSpacing)
            {
                continue;
            }

            Vector3 forward = ResolveForwardFromNeighborhood(rawPoints, i, previousForward);
            Vector3 right = TransportRight(previousRight, previousForward, forward);

            if (Vector3.Dot(right, previousRight) < 0f)
            {
                right = -right;
            }

            TrackSplinePoint stablePoint = CreateStablePoint(rawPoint, forward, right);
            stablePoints.Add(stablePoint);

            previousForward = forward;
            previousRight = right;
        }

        return stablePoints;
    }

    /// <summary>
    /// Crea un nuevo punto spline con forward y right estables.
    /// </summary>
    private static TrackSplinePoint CreateStablePoint(
        TrackSplinePoint source,
        Vector3 forward,
        Vector3 right)
    {
        return new TrackSplinePoint(
            source.Position,
            forward,
            right,
            source.Distance,
            source.Width,
            source.StructureType,
            source.HasSurface,
            source.RailSeparation,
            source.RailWidth,
            source.SectionIndex);
    }

    /// <summary>
    /// Resuelve un forward robusto a partir del entorno del índice.
    /// </summary>
    private static Vector3 ResolveForwardFromNeighborhood(
        List<TrackSplinePoint> rawPoints,
        int index,
        Vector3 fallbackForward)
    {
        Vector3 forward = ResolveValidForward(rawPoints, index);

        if (forward.sqrMagnitude < 0.0001f)
        {
            return fallbackForward;
        }

        if (Vector3.Dot(forward, fallbackForward) < -0.999f)
        {
            return fallbackForward;
        }

        return forward;
    }

    /// <summary>
    /// Obtiene un forward válido para el índice dado usando vecinos.
    /// </summary>
    private static Vector3 ResolveValidForward(
        List<TrackSplinePoint> rawPoints,
        int index)
    {
        if (rawPoints == null || rawPoints.Count == 0)
        {
            return Vector3.forward;
        }

        Vector3 forward = rawPoints[index].Forward;
        if (forward.sqrMagnitude >= 0.0001f)
        {
            return forward.normalized;
        }

        Vector3 tangent = Vector3.zero;

        if (index == 0 && rawPoints.Count > 1)
        {
            tangent = rawPoints[1].Position - rawPoints[0].Position;
        }
        else if (index == rawPoints.Count - 1 && rawPoints.Count > 1)
        {
            tangent = rawPoints[index].Position - rawPoints[index - 1].Position;
        }
        else if (index > 0 && index < rawPoints.Count - 1)
        {
            tangent = rawPoints[index + 1].Position - rawPoints[index - 1].Position;
        }

        if (tangent.sqrMagnitude < 0.0001f)
        {
            return Vector3.forward;
        }

        return tangent.normalized;
    }

    /// <summary>
    /// Resuelve el right inicial del frame.
    /// </summary>
    private static Vector3 ResolveInitialRight(Vector3 forward)
    {
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        if (right.sqrMagnitude < 0.0001f)
        {
            right = Vector3.Cross(Vector3.forward, forward);
        }

        if (right.sqrMagnitude < 0.0001f)
        {
            return Vector3.right;
        }

        right.Normalize();

        Vector3 up = Vector3.Cross(forward, right).normalized;
        right = Vector3.Cross(up, forward).normalized;

        return right;
    }

    /// <summary>
    /// Transporta el right anterior al nuevo forward para evitar torsiones bruscas.
    /// </summary>
    private static Vector3 TransportRight(
        Vector3 previousRight,
        Vector3 previousForward,
        Vector3 currentForward)
    {
        Vector3 projectedRight = Vector3.ProjectOnPlane(previousRight, currentForward);

        if (projectedRight.sqrMagnitude < 0.0001f)
        {
            projectedRight = Vector3.Cross(Vector3.up, currentForward);
        }

        if (projectedRight.sqrMagnitude < 0.0001f)
        {
            projectedRight = Vector3.Cross(previousForward, currentForward);
        }

        if (projectedRight.sqrMagnitude < 0.0001f)
        {
            projectedRight = Vector3.right;
        }

        projectedRight.Normalize();

        Vector3 up = Vector3.Cross(currentForward, projectedRight);
        if (up.sqrMagnitude < 0.0001f)
        {
            return ResolveInitialRight(currentForward);
        }

        up.Normalize();
        Vector3 correctedRight = Vector3.Cross(up, currentForward).normalized;

        if (correctedRight.sqrMagnitude < 0.0001f)
        {
            return ResolveInitialRight(currentForward);
        }

        return correctedRight;
    }

    #endregion
}