using System;
using UnityEngine;

/// <summary>
/// Entrada de configuración de spawn para un power-up coleccionable.
///
/// Desacopla la lógica de cuándo y con qué probabilidad aparece un power-up
/// (responsabilidad del <see cref="PowerUpGenerator"/>) de la lógica de qué hace
/// (responsabilidad del componente <see cref="CollectiblePowerUpBase"/> en el prefab).
///
/// Cada entrada apunta a un prefab que debe tener un componente derivado de
/// <see cref="CollectiblePowerUpBase"/>, donde reside toda la configuración del efecto.
/// </summary>
[Serializable]
public struct CollectiblePowerUpSpawnEntry
{
    #region Fields

    [SerializeField]
    [Tooltip("Prefab del power-up a instanciar. Debe contener un componente derivado de CollectiblePowerUpBase.")]
    private GameObject prefab;

    [SerializeField]
    [Tooltip("Nivel mínimo a partir del cual este power-up puede aparecer en el track.\n" +
             "0 = disponible desde el primer nivel.")]
    [Min(0)]
    private int minimumLevelToSpawn;

    [SerializeField]
    [Tooltip("Peso relativo de este power-up respecto a otros elegibles en el mismo nivel.\n" +
             "Valores mayores aumentan la probabilidad de ser elegido frente a otros tipos.")]
    [Min(0.01f)]
    private float spawnWeight;

    [SerializeField]
    [Tooltip("Curva de probabilidad de spawn según la progresión del jugador.\n" +
             "Eje X = progressionT normalizado [0 = nivel 1, 1 = dificultad máxima].\n" +
             "Eje Y = probabilidad de spawn [0 = nunca, 1 = siempre si es elegido].")]
    private AnimationCurve spawnChanceCurve;

    [SerializeField]
    [Tooltip("Longitud de la reserva en metros para el mapa de colisiones de spawn.\n" +
             "Evita que otros objetos (monedas, obstáculos) aparezcan en la misma zona.\n" +
             "Rango recomendado: 1.5 – 3 m.")]
    [Min(0f)]
    private float reservationLength;

    [SerializeField]
    [Tooltip("Ancho de la reserva en metros para el mapa de colisiones de spawn.\n" +
             "Rango recomendado: 1.5 – 3 m.")]
    [Min(0f)]
    private float reservationWidth;

    #endregion

    #region Properties

    /// <summary>Prefab a instanciar en el track.</summary>
    public GameObject Prefab => prefab;

    /// <summary>Peso relativo para la selección ponderada.</summary>
    public float SpawnWeight => spawnWeight;

    /// <summary>Longitud de reserva en el <c>TrackSpawnReservationMap</c>.</summary>
    public float ReservationLength => reservationLength;

    /// <summary>Ancho de reserva en el <c>TrackSpawnReservationMap</c>.</summary>
    public float ReservationWidth => reservationWidth;

    #endregion

    #region Helpers

    /// <summary>
    /// <c>true</c> si este power-up es elegible para el nivel dado.
    /// </summary>
    public bool IsEligibleForLevel(int levelIndex)
        => levelIndex >= minimumLevelToSpawn;

    /// <summary>
    /// Evalúa la probabilidad de spawn para el valor de progresión dado.
    ///
    /// Un <c>AnimationCurve</c> serializado por Unity nunca es null: cuando el campo está vacío
    /// en el Inspector (sin keyframes), <c>Evaluate()</c> devuelve 0, lo que filtraría esta
    /// entrada como inelegible. Por eso verificamos <c>length == 0</c> como fallback a 1.
    /// </summary>
    /// <param name="progressionT">Valor normalizado [0, 1] de la progresión del jugador.</param>
    public float EvaluateSpawnChance(float progressionT)
    {
        if (spawnChanceCurve == null || spawnChanceCurve.length == 0) return 1f;
        return spawnChanceCurve.Evaluate(progressionT);
    }

    #endregion
}