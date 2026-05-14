using UnityEngine;

/// <summary>
/// Configuración de un power-up coleccionable.
///
/// Un asset de este tipo define completamente un power-up: qué hace, cuánto dura,
/// desde qué nivel aparece y cómo escala su probabilidad de spawn con la progresión.
/// El <see cref="PowerUpGenerator"/> y el <see cref="BallPowerUpController"/> leen este asset;
/// ninguno de los dos tiene lógica hardcoded sobre tipos específicos.
/// </summary>
[CreateAssetMenu(fileName = "DA_PowerUp_", menuName = "Game/Power-Ups/Collectible Power-Up Data")]
public sealed class CollectiblePowerUpData : ScriptableObject
{
    #region Identity

    [Header("Identidad")]
    [SerializeField]
    [Tooltip("Tipo de efecto que aplica este power-up al ser recolectado.")]
    private CollectiblePowerUpType type;

    [SerializeField]
    [Tooltip("Duración del efecto en segundos.")]
    [Min(0.1f)]
    private float effectDuration = 5f;

    #endregion

    #region Progression

    [Header("Progresión")]
    [SerializeField]
    [Tooltip("Nivel mínimo a partir del cual este power-up puede aparecer en el track. " +
             "0 = disponible desde el primer nivel.")]
    [Min(0)]
    private int minimumLevelToSpawn = 0;

    [SerializeField]
    [Tooltip("Peso relativo de este power-up respecto a otros elegibles en el mismo nivel. " +
             "Valores mayores aumentan la probabilidad de ser elegido frente a otros tipos.")]
    [Min(0.01f)]
    private float spawnWeight = 1f;

    [SerializeField]
    [Tooltip("Curva de probabilidad de spawn según la progresión del jugador.\n" +
             "Eje X = progressionT normalizado [0 = nivel 1, 1 = dificultad máxima].\n" +
             "Eje Y = probabilidad de spawn [0 = nunca, 1 = siempre si es elegido].\n" +
             "Ejemplo típico: curva ascendente → aparece poco al principio, más al avanzar.")]
    private AnimationCurve spawnChanceCurve = AnimationCurve.Linear(0f, 0.2f, 1f, 0.9f);

    #endregion

    #region Reduced Inertia Effect

    [Header("Efecto: Reduced Inertia")]
    [SerializeField]
    [Tooltip("[Solo para tipo ReducedInertia]\n" +
             "Multiplicador sobre la fricción pasiva del motor. " +
             "0.3 = la bola retiene el 70% más de velocidad de lo normal. " +
             "Rango recomendado: 0.1 – 0.5.")]
    [Range(0f, 1f)]
    private float frictionMultiplier = 0.3f;

    [SerializeField]
    [Tooltip("[Solo para tipo ReducedInertia]\n" +
             "Multiplicador sobre la fuerza de adhesión al suelo. " +
             "0.4 = la bola se adhiere al 40% de la fuerza normal, permitiendo pequeños 'floats'. " +
             "Rango recomendado: 0.1 – 0.6.")]
    [Range(0f, 1f)]
    private float groundStickMultiplier = 0.4f;

    #endregion

    #region Magnet Effect

    [Header("Efecto: Magnet")]
    [SerializeField]
    [Tooltip("[Solo para tipo Magnet]\n" +
             "Radio en metros dentro del cual las monedas son atraídas hacia la bola.")]
    [Min(1f)]
    private float magnetRadius = 8f;

    [SerializeField]
    [Tooltip("[Solo para tipo Magnet]\n" +
             "Velocidad en m/s a la que las monedas se desplazan hacia la bola.")]
    [Min(1f)]
    private float magnetAttractionSpeed = 15f;

    #endregion

    #region Spawn Reservation

    [Header("Reserva de Spawn")]
    [SerializeField]
    [Tooltip("Longitud en metros del área reservada al colocar este power-up. " +
             "Evita que monedas u obstáculos se solapen con él.")]
    [Min(0.5f)]
    private float reservationLength = 2f;

    [SerializeField]
    [Tooltip("Ancho en metros del área reservada. 0 = sin restricción lateral (se coloca en el centro).")]
    [Min(0f)]
    private float reservationWidth = 1f;

    [SerializeField]
    [Tooltip("Offset vertical sobre la superficie de la pista al instanciar el pickup.")]
    [Min(0f)]
    private float surfaceOffset = 0.15f;

    #endregion

    #region Properties

    public CollectiblePowerUpType Type              => type;
    public float                  EffectDuration    => effectDuration;
    public int                    MinimumLevelToSpawn => minimumLevelToSpawn;
    public float                  SpawnWeight       => spawnWeight;

    public float FrictionMultiplier   => frictionMultiplier;
    public float GroundStickMultiplier => groundStickMultiplier;

    public float MagnetRadius          => magnetRadius;
    public float MagnetAttractionSpeed => magnetAttractionSpeed;

    public float ReservationLength => reservationLength;
    public float ReservationWidth  => reservationWidth;
    public float SurfaceOffset     => surfaceOffset;

    #endregion

    #region Public API

    /// <summary>
    /// Evalúa la probabilidad de spawn del power-up para el valor de progresión dado.
    /// </summary>
    /// <param name="progressionT">Factor de progresión [0–1].</param>
    public float EvaluateSpawnChance(float progressionT)
    {
        return Mathf.Clamp01(spawnChanceCurve.Evaluate(progressionT));
    }

    /// <summary>
    /// Indica si este power-up puede aparecer en el nivel indicado.
    /// </summary>
    public bool IsEligibleForLevel(int levelIndex)
    {
        return levelIndex >= minimumLevelToSpawn;
    }

    #endregion

    #region Validation

    private void OnValidate()
    {
        effectDuration    = Mathf.Max(0.1f, effectDuration);
        spawnWeight       = Mathf.Max(0.01f, spawnWeight);
        magnetRadius      = Mathf.Max(1f, magnetRadius);
        magnetAttractionSpeed = Mathf.Max(1f, magnetAttractionSpeed);
        reservationLength = Mathf.Max(0.5f, reservationLength);
        surfaceOffset     = Mathf.Max(0f, surfaceOffset);
    }

    #endregion
}