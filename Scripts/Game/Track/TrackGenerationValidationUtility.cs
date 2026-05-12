using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Utilidades de validación del sistema de generación de pista.
/// 
/// Responsabilidades:
/// - Detectar inconsistencias configurables sin autocorregir assets.
/// - Exponer mensajes reutilizables para runtime y editor.
/// </summary>
public static class TrackGenerationValidationUtility
{
    #region Public API

    /// <summary>
    /// Recolecta warnings de validación del perfil base.
    /// </summary>
    public static List<string> CollectProfileWarnings(TrackGenerationProfile profile)
    {
        List<string> warnings = new List<string>();

        if (profile == null)
        {
            warnings.Add("TrackGenerationProfile no está asignado.");
            return warnings;
        }

        ValidateOrderedRange(
            warnings,
            profile.MinimumSectionLength,
            profile.MaximumSectionLength,
            "MaximumSectionLength",
            "MinimumSectionLength");

        ValidateOrderedRange(
            warnings,
            profile.SlopeHeightStepMin,
            profile.SlopeHeightStepMax,
            "SlopeHeightStepMax",
            "SlopeHeightStepMin");

        ValidateOrderedRange(
            warnings,
            profile.NarrowWidthRatioMin,
            profile.NarrowWidthRatioMax,
            "NarrowWidthRatioMax",
            "NarrowWidthRatioMin");

        ValidateOrderedRange(
            warnings,
            profile.GapLengthMin,
            profile.GapLengthMax,
            "GapLengthMax",
            "GapLengthMin");

        ValidateOrderedRange(
            warnings,
            profile.PreGapRampLengthMin,
            profile.PreGapRampLengthMax,
            "PreGapRampLengthMax",
            "PreGapRampLengthMin");

        ValidateOrderedRange(
            warnings,
            profile.PreGapRampHeightMin,
            profile.PreGapRampHeightMax,
            "PreGapRampHeightMax",
            "PreGapRampHeightMin");

        ValidateOrderedRange(
            warnings,
            profile.RailSectionLengthMin,
            profile.RailSectionLengthMax,
            "RailSectionLengthMax",
            "RailSectionLengthMin");

        if (profile.SafeStartLength + profile.SafeEndLength > profile.TargetTrackLength)
        {
            warnings.Add("La suma de SafeStartLength y SafeEndLength supera TargetTrackLength.");
        }

        if (profile.RailRadialSegments < 3)
        {
            warnings.Add("RailRadialSegments debe ser al menos 3.");
        }

        if (profile.RailBlendLength < 0f)
        {
            warnings.Add("RailBlendLength no puede ser negativo.");
        }

        return warnings;
    }

    /// <summary>
    /// Recolecta warnings de validación de settings del nivel.
    /// </summary>
    public static List<string> CollectLevelSettingsWarnings(LevelGenerationSettings settings)
    {
        List<string> warnings = new List<string>();

        if (settings == null)
        {
            warnings.Add("LevelGenerationSettings no está asignado.");
            return warnings;
        }

        if (settings.OverrideMinHeight && settings.OverrideMaxHeight)
        {
            if (settings.MinHeightOverride > settings.MaxHeightOverride)
            {
                warnings.Add("MinHeightOverride no puede ser mayor que MaxHeightOverride.");
            }
        }

        return warnings;
    }

    /// <summary>
    /// Imprime warnings de validación en consola.
    /// </summary>
    public static void LogWarnings(
        Object context,
        IEnumerable<string> warnings)
    {
        if (warnings == null)
        {
            return;
        }

        foreach (string warning in warnings)
        {
            if (string.IsNullOrWhiteSpace(warning))
            {
                continue;
            }

            Debug.LogWarning($"[TRACK VALIDATION] {warning}", context);
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Añade warning si un rango ordenado es inválido.
    /// </summary>
    private static void ValidateOrderedRange(
        List<string> warnings,
        float minValue,
        float maxValue,
        string maxLabel,
        string minLabel)
    {
        if (maxValue < minValue)
        {
            warnings.Add($"{maxLabel} no puede ser menor que {minLabel}.");
        }
    }

    #endregion
}