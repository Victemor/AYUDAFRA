using UnityEngine;

/// <summary>
/// Power-up coleccionable de tipo Imán.
///
/// Al ser recolectado, activa el modo imán en <see cref="BallPowerUpController"/>:
/// las monedas dentro del radio configurado son atraídas hacia la bola durante
/// la duración del efecto.
///
/// Todos los parámetros del efecto están serializados en este componente.
/// No requiere ScriptableObject externo.
/// </summary>
public sealed class PowerUpMagnet : CollectiblePowerUpBase
{
    #region Inspector

    [Header("Efecto — Imán")]
    [SerializeField]
    [Tooltip("Duración del efecto de imán en segundos.")]
    [Min(0.1f)]
    private float effectDuration = 6f;

    [SerializeField]
    [Tooltip("Radio en metros dentro del cual las monedas son atraídas hacia la bola.\n" +
             "Rango recomendado: 5 – 12 m.")]
    [Min(0.5f)]
    private float magnetRadius = 8f;

    [SerializeField]
    [Tooltip("Velocidad en m/s a la que las monedas se mueven hacia la bola.\n" +
             "Rango recomendado: 8 – 15 m/s.")]
    [Min(0.1f)]
    private float attractionSpeed = 12f;

    #endregion

    #region CollectiblePowerUpBase

    /// <inheritdoc/>
    public override CollectiblePowerUpType PowerUpType => CollectiblePowerUpType.Magnet;

    /// <inheritdoc/>
    public override void ApplyEffect(BallPowerUpController controller)
    {
        controller.ActivateMagnet(effectDuration, magnetRadius, attractionSpeed);
    }

    #endregion
}