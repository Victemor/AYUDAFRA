using UnityEngine;

/// <summary>
/// Perfil base de generación procedural del track.
/// </summary>
[CreateAssetMenu(fileName = "TrackGenerationProfile", menuName = "Game/Track/Generation Profile")]
public sealed class TrackGenerationProfile : ScriptableObject
{
    #region Geometry

    [Header("Geometry")]
    [SerializeField]
    [Tooltip("Ancho normal del track sólido.")]
    private float normalTrackWidth = 6f;

    [SerializeField]
    [Tooltip("Grosor vertical del track sólido.")]
    private float trackThickness = 0.5f;

    [SerializeField]
    [Tooltip("Longitud mínima de una sección lógica.")]
    private float minimumSectionLength = 6f;

    [SerializeField]
    [Tooltip("Longitud máxima de una sección lógica.")]
    private float maximumSectionLength = 12f;

    [SerializeField]
    [Tooltip("Radio base usado para giros laterales suavizados.")]
    private float curveRadius = 6f;

    [SerializeField]
    [Tooltip("Cantidad de subdivisiones usadas para suavizar giros.")]
    private int curveSubdivisionCount = 8;

    #endregion

    #region Turn Weights

    [Header("Turn Weights")]
    [SerializeField]
    [Tooltip("Peso relativo de giros laterales de 45 grados.")]
    private float turn45Weight = 1.25f;

    [SerializeField]
    [Tooltip("Peso relativo de giros laterales de 90 grados.")]
    private float turn90Weight = 0.8f;

    #endregion

    #region Slope Range

    [Header("Slope Range")]
    [SerializeField]
    [Tooltip("Delta mínimo de altura para una pendiente normal.")]
    private float slopeHeightStepMin = 1f;

    [SerializeField]
    [Tooltip("Delta máximo de altura para una pendiente normal. El techo real también queda limitado por maxSlopeAngleDegrees.")]
    private float slopeHeightStepMax = 3f;

    [SerializeField]
    [Tooltip("Ángulo máximo permitido en la sección más empinada de la pendiente.")]
    [Range(5f, 60f)]
    private float maxSlopeAngleDegrees = 30f;

    [SerializeField]
    [Tooltip("Longitud de transición suave al entrar y salir de una pendiente.")]
    private float slopeTransitionLength = 2f;

    #endregion

    #region Narrow Range

    [Header("Narrow Range")]
    [SerializeField]
    [Tooltip("Relación mínima de ancho permitida al estrechar.")]
    private float narrowWidthRatioMin = 0.4f;

    [SerializeField]
    [Tooltip("Relación máxima de ancho permitida al estrechar.")]
    private float narrowWidthRatioMax = 0.8f;

    [SerializeField]
    [Tooltip("Longitud de transición suave al entrar y salir de un estrechamiento.")]
    private float narrowTransitionLength = 2f;

    #endregion

    #region Gap

    [Header("Gap")]
    [SerializeField]
    [Tooltip("Longitud mínima del gap.")]
    private float gapLengthMin = 2f;

    [SerializeField]
    [Tooltip("Longitud máxima del gap.")]
    private float gapLengthMax = 4f;

    [SerializeField]
    [Tooltip("Longitud mínima de la mini rampa previa al gap.")]
    private float preGapRampLengthMin = 0.75f;

    [SerializeField]
    [Tooltip("Longitud máxima de la mini rampa previa al gap.")]
    private float preGapRampLengthMax = 1.5f;

    [SerializeField]
    [Tooltip("Altura mínima de la mini rampa previa al gap.")]
    private float preGapRampHeightMin = 0.2f;

    [SerializeField]
    [Tooltip("Altura máxima de la mini rampa previa al gap.")]
    private float preGapRampHeightMax = 0.5f;

    #endregion

    #region Rail

    [Header("Rail")]
    [SerializeField]
    [Tooltip("Probabilidad base de intentar generar una estructura de rieles.")]
    [Range(0f, 1f)]
    private float railGenerationChance = 0.06f;

    [SerializeField]
    [Tooltip("Longitud mínima de un tramo individual dentro de la estructura rail.")]
    private float railSectionLengthMin = 6f;

    [SerializeField]
    [Tooltip("Longitud máxima de un tramo individual dentro de la estructura rail.")]
    private float railSectionLengthMax = 10f;

    [SerializeField]
    [Tooltip("Cantidad máxima de secciones consecutivas permitidas dentro de una secuencia rail.")]
    private int railMaxConsecutiveSections = 4;

    [SerializeField]
    [Tooltip("Separación entre los dos rieles.")]
    private float railSeparation = 0.9f;

    [SerializeField]
    [Tooltip("Ancho individual de cada riel.")]
    private float railWidth = 0.35f;

    [SerializeField]
    [Tooltip("Altura individual de cada riel.")]
    private float railHeight = 0.25f;

    [SerializeField]
    [Tooltip("Multiplicador aplicado a la probabilidad de giros laterales dentro de una secuencia rail.")]
    private float railTurnChanceMultiplier = 1.15f;

    [SerializeField]
    [Tooltip("Multiplicador aplicado a la probabilidad de cambios verticales dentro de una secuencia rail.")]
    private float railVerticalChanceMultiplier = 0.5f;

    [SerializeField]
    [Tooltip("Valor legacy. Se conserva para no romper referencias existentes, pero ya no deforma el radio visual de los rieles.")]
    private float railBlendLength = 2.5f;

    [SerializeField]
    [Tooltip("Offset vertical aplicado al inicio del rail. Un valor negativo ayuda a que la bola entre sin golpear el borde.")]
    private float railEntryVerticalOffset = -0.08f;

    [SerializeField]
    [Tooltip("Offset vertical aplicado al final del rail. Un valor positivo ayuda a entregar la bola hacia el siguiente tramo sólido.")]
    private float railExitVerticalOffset = 0.06f;

    [SerializeField]
    [Tooltip("Cantidad de segmentos radiales usados para construir cada riel cilíndrico.")]
    private int railRadialSegments = 10;

    #endregion

    #region Generation Length

    [Header("Generation Length")]
    [SerializeField]
    [Tooltip("Longitud objetivo total del track.")]
    private float targetTrackLength = 250f;

    [SerializeField]
    [Tooltip("Longitud segura inicial del track.")]
    private float safeStartLength = 20f;

    [SerializeField]
    [Tooltip("Longitud segura final del track.")]
    private float safeEndLength = 18f;

    #endregion

    #region Recovery Distances

    [Header("Recovery Distances")]
    [SerializeField]
    [Tooltip("Longitud recta mínima después de un cambio lateral.")]
    private float minStraightAfterLateralChange = 10f;

    [SerializeField]
    [Tooltip("Longitud recta mínima después de un cambio vertical.")]
    private float minStraightAfterVerticalChange = 10f;

    [SerializeField]
    [Tooltip("Longitud recta mínima después de un cambio de ancho.")]
    private float minStraightAfterWidthChange = 8f;

    [SerializeField]
    [Tooltip("Longitud recta mínima después de un gap.")]
    private float minStraightAfterGap = 10f;

    [SerializeField]
    [Tooltip("Longitud recta mínima después de una secuencia rail.")]
    private float minStraightAfterRail = 10f;

    #endregion

    #region Height Limits

    [Header("Height Limits")]
    [SerializeField]
    [Tooltip("Altura mínima permitida del track.")]
    private float minTrackHeight = -4f;

    [SerializeField]
    [Tooltip("Altura máxima permitida del track.")]
    private float maxTrackHeight = 8f;

    #endregion

    #region Probabilities

    [Header("Base Probabilities")]
    [SerializeField]
    [Tooltip("Probabilidad base de intentar un cambio lateral.")]
    [Range(0f, 1f)]
    private float lateralChangeChance = 0.45f;

    [SerializeField]
    [Tooltip("Probabilidad base de intentar un cambio vertical.")]
    [Range(0f, 1f)]
    private float verticalChangeChance = 0.2f;

    [SerializeField]
    [Tooltip("Probabilidad base de intentar un estrechamiento.")]
    [Range(0f, 1f)]
    private float narrowChance = 0.08f;

    [SerializeField]
    [Tooltip("Probabilidad base de intentar un gap.")]
    [Range(0f, 1f)]
    private float gapChance = 0.015f;

    #endregion

    #region Bias

    [Header("Bias")]
    [SerializeField]
    [Tooltip("Peso relativo para giros hacia izquierda.")]
    private float leftTurnWeight = 1f;

    [SerializeField]
    [Tooltip("Peso relativo para giros hacia derecha.")]
    private float rightTurnWeight = 1f;

    [SerializeField]
    [Tooltip("Peso relativo para pendientes ascendentes.")]
    private float slopeUpWeight = 1f;

    [SerializeField]
    [Tooltip("Peso relativo para pendientes descendentes.")]
    private float slopeDownWeight = 1f;

    #endregion

    #region Forbidden Zones

    [Header("Forbidden Zones")]
    [SerializeField]
    [Tooltip("Distancia mínima desde el inicio del track para permitir un gap.")]
    private float forbidGapNearStartDistance = 30f;

    [SerializeField]
    [Tooltip("Distancia mínima hasta el final del track para permitir un gap.")]
    private float forbidGapNearEndDistance = 30f;

    [SerializeField]
    [Tooltip("Distancia mínima desde un gap para permitir un estrechamiento.")]
    private float forbidNarrowNearGapDistance = 15f;

    [SerializeField]
    [Tooltip("Distancia mínima tras un cambio difícil reciente para permitir un gap.")]
    private float forbidGapAfterRecentHardChangeDistance = 20f;

    [SerializeField]
    [Tooltip("Distancia desde el inicio del track antes de la cual no se generan rieles.")]
    private float forbidRailNearStartDistance = 30f;

    [SerializeField]
    [Tooltip("Distancia hasta el final del track en la que se prohíbe iniciar una secuencia rail.")]
    private float forbidRailNearEndDistance = 40f;

    #endregion

    #region Materials

    [Header("Materials")]
    [SerializeField]
    [Tooltip("Material del centro superior del track.")]
    private Material topCenterMaterial;

    [SerializeField]
    [Tooltip("Material de los bordes superiores del track.")]
    private Material topBorderMaterial;

    [SerializeField]
    [Tooltip("Material de la parte inferior del track.")]
    private Material bottomMaterial;

    [SerializeField]
    [Tooltip("Material de los laterales del track.")]
    private Material sideMaterial;

    [SerializeField]
    [Tooltip("Material específico de los railes.")]
    private Material railMaterial;

    #endregion

    #region Properties

    public float NormalTrackWidth => normalTrackWidth;
    public float TrackThickness => trackThickness;
    public float MinimumSectionLength => minimumSectionLength;
    public float MaximumSectionLength => maximumSectionLength;
    public float CurveRadius => curveRadius;
    public int CurveSubdivisionCount => curveSubdivisionCount;

    public float Turn45Weight => turn45Weight;
    public float Turn90Weight => turn90Weight;

    public float SlopeHeightStepMin => slopeHeightStepMin;
    public float SlopeHeightStepMax => slopeHeightStepMax;
    public float MaxSlopeAngleDegrees => maxSlopeAngleDegrees;
    public float SlopeTransitionLength => slopeTransitionLength;

    public float NarrowWidthRatioMin => narrowWidthRatioMin;
    public float NarrowWidthRatioMax => narrowWidthRatioMax;
    public float NarrowTransitionLength => narrowTransitionLength;

    public float GapLengthMin => gapLengthMin;
    public float GapLengthMax => gapLengthMax;
    public float PreGapRampLengthMin => preGapRampLengthMin;
    public float PreGapRampLengthMax => preGapRampLengthMax;
    public float PreGapRampHeightMin => preGapRampHeightMin;
    public float PreGapRampHeightMax => preGapRampHeightMax;

    public float RailGenerationChance => railGenerationChance;
    public float RailSectionLengthMin => railSectionLengthMin;
    public float RailSectionLengthMax => railSectionLengthMax;
    public int RailMaxConsecutiveSections => railMaxConsecutiveSections;
    public float RailSeparation => railSeparation;
    public float RailWidth => railWidth;
    public float RailHeight => railHeight;
    public float RailTurnChanceMultiplier => railTurnChanceMultiplier;
    public float RailVerticalChanceMultiplier => railVerticalChanceMultiplier;
    public float RailBlendLength => railBlendLength;
    public float RailEntryVerticalOffset => railEntryVerticalOffset;
    public float RailExitVerticalOffset => railExitVerticalOffset;
    public int RailRadialSegments => railRadialSegments;

    public float TargetTrackLength => targetTrackLength;
    public float SafeStartLength => safeStartLength;
    public float SafeEndLength => safeEndLength;

    public float MinStraightAfterLateralChange => minStraightAfterLateralChange;
    public float MinStraightAfterVerticalChange => minStraightAfterVerticalChange;
    public float MinStraightAfterWidthChange => minStraightAfterWidthChange;
    public float MinStraightAfterGap => minStraightAfterGap;
    public float MinStraightAfterRail => minStraightAfterRail;

    public float MinTrackHeight => minTrackHeight;
    public float MaxTrackHeight => maxTrackHeight;

    public float LateralChangeChance => lateralChangeChance;
    public float VerticalChangeChance => verticalChangeChance;
    public float NarrowChance => narrowChance;
    public float GapChance => gapChance;

    public float LeftTurnWeight => leftTurnWeight;
    public float RightTurnWeight => rightTurnWeight;
    public float SlopeUpWeight => slopeUpWeight;
    public float SlopeDownWeight => slopeDownWeight;

    public float ForbidGapNearStartDistance => forbidGapNearStartDistance;
    public float ForbidGapNearEndDistance => forbidGapNearEndDistance;
    public float ForbidNarrowNearGapDistance => forbidNarrowNearGapDistance;
    public float ForbidGapAfterRecentHardChangeDistance => forbidGapAfterRecentHardChangeDistance;
    public float ForbidRailNearStartDistance => forbidRailNearStartDistance;
    public float ForbidRailNearEndDistance => forbidRailNearEndDistance;

    public Material TopCenterMaterial => topCenterMaterial;
    public Material TopBorderMaterial => topBorderMaterial;
    public Material BottomMaterial => bottomMaterial;
    public Material SideMaterial => sideMaterial;
    public Material RailMaterial => railMaterial;

    #endregion

    private void OnValidate()
    {
        normalTrackWidth = Mathf.Max(0.1f, normalTrackWidth);
        trackThickness = Mathf.Max(0.01f, trackThickness);
        minimumSectionLength = Mathf.Max(1f, minimumSectionLength);
        maximumSectionLength = Mathf.Max(minimumSectionLength, maximumSectionLength);
        curveRadius = Mathf.Max(0.5f, curveRadius);
        curveSubdivisionCount = Mathf.Max(2, curveSubdivisionCount);

        slopeHeightStepMin = Mathf.Max(0.01f, slopeHeightStepMin);
        slopeHeightStepMax = Mathf.Max(slopeHeightStepMin, slopeHeightStepMax);
        maxSlopeAngleDegrees = Mathf.Clamp(maxSlopeAngleDegrees, 1f, 89f);
        slopeTransitionLength = Mathf.Max(0f, slopeTransitionLength);

        narrowWidthRatioMin = Mathf.Clamp(narrowWidthRatioMin, 0.1f, 1f);
        narrowWidthRatioMax = Mathf.Clamp(narrowWidthRatioMax, narrowWidthRatioMin, 1f);
        narrowTransitionLength = Mathf.Max(0f, narrowTransitionLength);

        gapLengthMin = Mathf.Max(0.5f, gapLengthMin);
        gapLengthMax = Mathf.Max(gapLengthMin, gapLengthMax);
        preGapRampLengthMin = Mathf.Max(0.1f, preGapRampLengthMin);
        preGapRampLengthMax = Mathf.Max(preGapRampLengthMin, preGapRampLengthMax);
        preGapRampHeightMin = Mathf.Max(0.01f, preGapRampHeightMin);
        preGapRampHeightMax = Mathf.Max(preGapRampHeightMin, preGapRampHeightMax);

        railSectionLengthMin = Mathf.Max(1f, railSectionLengthMin);
        railSectionLengthMax = Mathf.Max(railSectionLengthMin, railSectionLengthMax);
        railMaxConsecutiveSections = Mathf.Max(1, railMaxConsecutiveSections);
        railSeparation = Mathf.Max(0.05f, railSeparation);
        railWidth = Mathf.Max(0.05f, railWidth);
        railHeight = Mathf.Max(0.05f, railHeight);
        railTurnChanceMultiplier = Mathf.Max(0f, railTurnChanceMultiplier);
        railVerticalChanceMultiplier = Mathf.Max(0f, railVerticalChanceMultiplier);
        railBlendLength = Mathf.Max(0f, railBlendLength);
        railEntryVerticalOffset = Mathf.Clamp(railEntryVerticalOffset, -0.5f, 0.5f);
        railExitVerticalOffset = Mathf.Clamp(railExitVerticalOffset, -0.5f, 0.5f);
        railRadialSegments = Mathf.Max(3, railRadialSegments);

        targetTrackLength = Mathf.Max(10f, targetTrackLength);
        safeStartLength = Mathf.Max(0f, safeStartLength);
        safeEndLength = Mathf.Max(0f, safeEndLength);

        minStraightAfterLateralChange = Mathf.Max(0f, minStraightAfterLateralChange);
        minStraightAfterVerticalChange = Mathf.Max(0f, minStraightAfterVerticalChange);
        minStraightAfterWidthChange = Mathf.Max(0f, minStraightAfterWidthChange);
        minStraightAfterGap = Mathf.Max(0f, minStraightAfterGap);
        minStraightAfterRail = Mathf.Max(0f, minStraightAfterRail);

        leftTurnWeight = Mathf.Max(0.01f, leftTurnWeight);
        rightTurnWeight = Mathf.Max(0.01f, rightTurnWeight);
        slopeUpWeight = Mathf.Max(0.01f, slopeUpWeight);
        slopeDownWeight = Mathf.Max(0.01f, slopeDownWeight);

        forbidGapNearStartDistance = Mathf.Max(0f, forbidGapNearStartDistance);
        forbidGapNearEndDistance = Mathf.Max(0f, forbidGapNearEndDistance);
        forbidNarrowNearGapDistance = Mathf.Max(0f, forbidNarrowNearGapDistance);
        forbidGapAfterRecentHardChangeDistance = Mathf.Max(0f, forbidGapAfterRecentHardChangeDistance);
        forbidRailNearStartDistance = Mathf.Max(0f, forbidRailNearStartDistance);
        forbidRailNearEndDistance = Mathf.Max(0f, forbidRailNearEndDistance);
    }
}