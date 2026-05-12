using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resuelve una sección lógica del track en una curva continua.
/// 
/// Responsabilidades:
/// - Convertir secciones rectas, pendientes y giros en puntos continuos.
/// - Suavizar variaciones verticales reales para evitar quiebres físicos.
/// - Usar una transición vertical por zonas para entrada y salida de pendientes.
/// - Interpolar ancho y metadatos de estructura de forma estable.
/// </summary>
public static class TrackSectionCurveResolver
{
    #region Public API

    /// <summary>
    /// Convierte una sección lógica en una lista de puntos continuos.
    /// </summary>
    public static List<TrackSplinePoint> ResolveSection(
        TrackSectionDefinition section,
        TrackGenerationProfile generationProfile,
        int sectionIndex)
    {
        List<Vector3> positions = BuildSectionPositions(section, generationProfile);
        List<TrackSplinePoint> points = new List<TrackSplinePoint>(positions.Count);

        if (positions.Count == 0)
        {
            return points;
        }

        for (int i = 0; i < positions.Count; i++)
        {
            float t = positions.Count == 1 ? 0f : i / (float)(positions.Count - 1);
            float widthT = RequiresSmoothWidth(section)
                ? EvaluateSmootherStep(t)
                : t;

            Vector3 forward = ResolveForward(positions, i);
            Vector3 right = ResolveRight(forward);

            float distance = Mathf.Lerp(section.StartDistance, section.EndDistance, t);
            float width = Mathf.Lerp(section.StartWidth, section.EndWidth, widthT);

            points.Add(new TrackSplinePoint(
                positions[i],
                forward,
                right,
                distance,
                width,
                section.StructureType,
                section.HasSurface,
                section.RailSeparation,
                section.RailWidth,
                sectionIndex));
        }

        return points;
    }

    #endregion

    #region Position Build

    /// <summary>
    /// Construye las posiciones continuas de una sección.
    /// </summary>
    private static List<Vector3> BuildSectionPositions(
        TrackSectionDefinition section,
        TrackGenerationProfile generationProfile)
    {
        if (Mathf.Abs(section.TurnAngleDegrees) > 0.001f && section.TurnRadius > 0.001f)
        {
            return BuildTurnPositions(section, generationProfile);
        }

        return BuildLinearPositions(section, generationProfile);
    }

    /// <summary>
    /// Construye posiciones para una sección lineal con variación vertical suavizada.
    /// </summary>
    private static List<Vector3> BuildLinearPositions(
        TrackSectionDefinition section,
        TrackGenerationProfile generationProfile)
    {
        int sampleCount = ResolveSampleCount(section, generationProfile);
        List<Vector3> positions = new List<Vector3>(sampleCount);

        Vector3 start = section.StartPosition;
        Vector3 end = section.EndPosition;

        Vector3 planarStart = new Vector3(start.x, 0f, start.z);
        Vector3 planarEnd = new Vector3(end.x, 0f, end.z);

        for (int i = 0; i < sampleCount; i++)
        {
            float t = sampleCount == 1 ? 0f : i / (float)(sampleCount - 1);
            Vector3 planarPosition = Vector3.Lerp(planarStart, planarEnd, t);

            float y = EvaluateSectionHeight(section, generationProfile, t);

            positions.Add(new Vector3(
                planarPosition.x,
                y,
                planarPosition.z));
        }

        return positions;
    }

    /// <summary>
    /// Construye posiciones para una sección de giro con posible variación vertical suavizada.
    /// </summary>
    private static List<Vector3> BuildTurnPositions(
        TrackSectionDefinition section,
        TrackGenerationProfile generationProfile)
    {
        int sampleCount = ResolveSampleCount(section, generationProfile);
        List<Vector3> positions = new List<Vector3>(sampleCount);

        Vector3 startForward = section.StartForward;
        startForward.y = 0f;

        if (startForward.sqrMagnitude < 0.0001f)
        {
            startForward = Vector3.forward;
        }
        else
        {
            startForward.Normalize();
        }

        Vector3 startRight = Vector3.Cross(Vector3.up, startForward).normalized;
        float signedAngle = section.TurnAngleDegrees;
        float radius = section.TurnRadius;
        float turnSign = Mathf.Sign(signedAngle);

        Vector3 center = section.StartPosition + (startRight * radius * turnSign);
        Vector3 radialStart = section.StartPosition - center;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = sampleCount == 1 ? 0f : i / (float)(sampleCount - 1);
            float turnT = EvaluateSmootherStep(t);
            float currentAngle = signedAngle * turnT;

            Vector3 rotatedRadial = Quaternion.AngleAxis(currentAngle, Vector3.up) * radialStart;
            Vector3 position = center + rotatedRadial;
            position.y = EvaluateSectionHeight(section, generationProfile, t);

            positions.Add(position);
        }

        return positions;
    }

    #endregion

    #region Height Evaluation

    /// <summary>
    /// Evalúa la altura de una sección en un t normalizado.
    /// </summary>
    private static float EvaluateSectionHeight(
        TrackSectionDefinition section,
        TrackGenerationProfile generationProfile,
        float t)
    {
        if (!RequiresSmoothVertical(section))
        {
            return Mathf.Lerp(section.StartHeight, section.EndHeight, t);
        }

        float normalizedHeight = EvaluateVerticalProfile(
            t,
            section.Length,
            generationProfile.SlopeTransitionLength);

        return Mathf.Lerp(section.StartHeight, section.EndHeight, normalizedHeight);
    }

    /// <summary>
    /// Evalúa un perfil vertical por zonas:
    /// entrada suave, tramo medio y salida suave.
    /// 
    /// Este perfil garantiza derivada horizontal al inicio y al final,
    /// lo que reduce el salto al conectar pendiente con plano.
    /// </summary>
    private static float EvaluateVerticalProfile(
        float t,
        float sectionLength,
        float transitionLength)
    {
        t = Mathf.Clamp01(t);

        if (sectionLength <= 0.0001f)
        {
            return t;
        }

        float normalizedTransition = Mathf.Clamp01(transitionLength / sectionLength);

        if (normalizedTransition <= 0.0001f)
        {
            return EvaluateSmootherStep(t);
        }

        normalizedTransition = Mathf.Min(normalizedTransition, 0.499f);

        if (t <= normalizedTransition)
        {
            float localT = t / normalizedTransition;

            return (normalizedTransition * 0.5f * localT * localT)
                   / (1f - normalizedTransition);
        }

        float middleEnd = 1f - normalizedTransition;

        if (t < middleEnd)
        {
            return (t - (normalizedTransition * 0.5f))
                   / (1f - normalizedTransition);
        }

        float endLocalT = (t - middleEnd) / normalizedTransition;
        float baseValue = (1f - (normalizedTransition * 1.5f))
                          / (1f - normalizedTransition);

        float endContribution = normalizedTransition
                                * (endLocalT - (0.5f * endLocalT * endLocalT))
                                / (1f - normalizedTransition);

        return baseValue + endContribution;
    }

    #endregion

    #region Forward and Right

    /// <summary>
    /// Resuelve la tangente de avance en un índice dado.
    /// </summary>
    private static Vector3 ResolveForward(List<Vector3> positions, int index)
    {
        if (positions.Count == 1)
        {
            return Vector3.forward;
        }

        Vector3 tangent;

        if (index == 0)
        {
            tangent = positions[1] - positions[0];
        }
        else if (index == positions.Count - 1)
        {
            tangent = positions[index] - positions[index - 1];
        }
        else
        {
            tangent = positions[index + 1] - positions[index - 1];
        }

        if (tangent.sqrMagnitude < 0.0001f)
        {
            return Vector3.forward;
        }

        return tangent.normalized;
    }

    /// <summary>
    /// Resuelve el vector lateral horizontal del track.
    /// </summary>
    private static Vector3 ResolveRight(Vector3 forward)
    {
        Vector3 horizontalForward = new Vector3(forward.x, 0f, forward.z);

        if (horizontalForward.sqrMagnitude < 0.0001f)
        {
            return Vector3.right;
        }

        horizontalForward.Normalize();
        return Vector3.Cross(Vector3.up, horizontalForward).normalized;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Determina si la sección requiere suavizado vertical real.
    /// </summary>
    private static bool RequiresSmoothVertical(TrackSectionDefinition section)
    {
        return !Mathf.Approximately(section.StartHeight, section.EndHeight)
               || !Mathf.Approximately(section.SlopeHeightDelta, 0f)
               || !Mathf.Approximately(section.RampHeightDelta, 0f);
    }

    /// <summary>
    /// Determina si la sección requiere suavizado fuerte de ancho.
    /// </summary>
    private static bool RequiresSmoothWidth(TrackSectionDefinition section)
    {
        return !Mathf.Approximately(section.StartWidth, section.EndWidth);
    }

    /// <summary>
    /// Determina la cantidad de samples adecuados para una sección.
    /// 
    /// Las secciones con pendiente reciben más densidad para suavizar
    /// mejor el MeshCollider y reducir quiebres de física.
    /// </summary>
    private static int ResolveSampleCount(
        TrackSectionDefinition section,
        TrackGenerationProfile generationProfile)
    {
        float safeLength = Mathf.Max(0f, section.Length);

        int baseLinearCount = Mathf.Max(2, Mathf.CeilToInt(safeLength * 1.0f) + 1);
        int baseCurveCount = Mathf.Max(4, generationProfile.CurveSubdivisionCount + 1);

        if (Mathf.Abs(section.TurnAngleDegrees) > 0.001f)
        {
            return Mathf.Max(baseCurveCount, baseLinearCount);
        }

        if (RequiresSmoothVertical(section))
        {
            int verticalCurveCount = Mathf.Max(
                baseCurveCount * 2,
                Mathf.CeilToInt(safeLength * 1.5f) + 1);

            return Mathf.Max(8, verticalCurveCount);
        }

        if (RequiresSmoothWidth(section))
        {
            return Mathf.Max(baseCurveCount, baseLinearCount);
        }

        return baseLinearCount;
    }

    /// <summary>
    /// Curva smoother step 0..1.
    /// </summary>
    private static float EvaluateSmootherStep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t * (t * ((6f * t) - 15f) + 10f);
    }

    #endregion
}