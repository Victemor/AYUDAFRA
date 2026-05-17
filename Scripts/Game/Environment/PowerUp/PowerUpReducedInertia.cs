using UnityEngine;

/// <summary>
/// Power-up coleccionable de tipo Inercia Reducida.
///
/// Al ser recolectado, modifica temporalmente el motor de movimiento de la bola:
/// - Reduce la fricción pasiva para que la bola no pierda velocidad por inercia.
/// - Mejora la respuesta de steering para que los giros a alta velocidad sean
///   menos restrictivos.
/// - Reduce la adhesión al suelo para una sensación de "flotabilidad" controlada.
///
/// Todos los parámetros del efecto están serializados en este componente.
/// No requiere ScriptableObject externo.
/// </summary>
public sealed class PowerUpReducedInertia : CollectiblePowerUpBase
{
    #region Inspector

    [Header("Efecto — Inercia Reducida")]
    [SerializeField]
    [Tooltip("Duración del efecto en segundos.")]
    [Min(0.1f)]
    private float effectDuration = 5f;

    [SerializeField]
    [Tooltip("Multiplicador sobre la fricción pasiva del motor durante el efecto.\n" +
             "0 = la bola no pierde velocidad por inercia.\n" +
             "0.05 = pérdida de velocidad casi nula (recomendado para un feel controlado).\n" +
             "1 = fricción normal sin cambio.\n" +
             "Rango recomendado: 0 – 0.1.")]
    [Range(0f, 1f)]
    private float frictionMultiplier = 0.05f;

    [SerializeField]
    [Tooltip("Multiplicador sobre la fuerza de adhesión al suelo durante el efecto.\n" +
             "0.4 = la bola se adhiere con el 40% de la fuerza normal.\n" +
             "Permite una ligera sensación de flotabilidad sin perder el contacto.\n" +
             "Rango recomendado: 0.3 – 0.6.")]
    [Range(0f, 1f)]
    private float groundStickMultiplier = 0.4f;

    [SerializeField]
    [Tooltip("Multiplicador sobre la velocidad de steering durante el efecto.\n" +
             "1 = steering normal.\n" +
             "1.5 = 50% más de respuesta en giros, ideal para curvas a alta velocidad.\n" +
             "Rango recomendado: 1.3 – 2.0.")]
    [Range(1f, 3f)]
    private float steeringBoostMultiplier = 1.5f;

    #endregion

    #region CollectiblePowerUpBase

    /// <inheritdoc/>
    public override CollectiblePowerUpType PowerUpType => CollectiblePowerUpType.ReducedInertia;

    /// <inheritdoc/>
    public override void ApplyEffect(BallPowerUpController controller)
    {
        controller.ActivateReducedInertia(
            effectDuration,
            frictionMultiplier,
            groundStickMultiplier,
            steeringBoostMultiplier);
    }

    #endregion
}