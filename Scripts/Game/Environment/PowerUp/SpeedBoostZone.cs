using UnityEngine;

/// <summary>
/// Zona de aceleración sobre el track.
///
/// Mientras la bola esté dentro del trigger, su velocidad máxima se incrementa
/// en <see cref="speedBoostAmount"/> m/s adicionales por encima del máximo base
/// configurado en <see cref="BallMovementMotor"/>. Al salir, el máximo vuelve al valor normal.
///
/// Ejemplo: si la velocidad máxima base es 12 m/s y <see cref="speedBoostAmount"/> es 5,
/// la bola puede alcanzar hasta 17 m/s dentro de la zona.
///
/// Diseño del boost como suma aditiva (no multiplicador):
/// La suma da al diseñador un control directo e intuitivo sobre cuántos m/s extra
/// aporta la zona, independientemente de la velocidad base configurada en el motor.
/// Un multiplicador hace que el delta de velocidad real cambie cada vez que se ajusta
/// la velocidad base, lo que genera iteraciones lentas de tuning.
///
/// El prefab de esta zona debe incluir:
/// - Una malla visual del camino elevado.
/// - Un Collider físico (no trigger) que sirva de suelo para la bola.
/// - Este trigger volumétrico que abarca toda la zona elevada.
/// </summary>
public sealed class SpeedBoostZone : MonoBehaviour
{
    #region Inspector

    [Header("Configuración")]
    [SerializeField]
    [Tooltip("Velocidad extra en m/s que se suma al máximo base del motor mientras la bola está en la zona.\n" +
             "Ejemplo: máximo base = 12 m/s, speedBoostAmount = 5 → máximo en zona = 17 m/s.\n" +
             "Rango recomendado: 3 – 8 m/s.")]
    [Min(0f)]
    private float speedBoostAmount = 5f;

    [Header("Debug")]
    [SerializeField]
    [Tooltip("Si está activo, imprime logs de entrada y salida de la zona en la consola.\n" +
             "Desactivar en builds de producción.")]
    private bool enableDebugLogs;

    #endregion

    #region Physics

    private void OnTriggerEnter(Collider other)
    {
        // Detectamos al jugador por componente para ser consistentes con el resto
        // del proyecto. CompareTag introduce una dependencia en un string "Player"
        // que puede desincronizarse silenciosamente si se renombra el tag.
        BallMovementMotor motor =
            other.GetComponent<BallMovementMotor>()
            ?? other.GetComponentInParent<BallMovementMotor>();

        if (motor == null) return;

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[SPEED BOOST] Bola entrando — boost: +{speedBoostAmount:F1} m/s — " +
                $"velocidad actual: {motor.CurrentPlanarVelocity:F1} m/s — " +
                $"nuevo máximo efectivo: {motor.MaxSpeed + speedBoostAmount:F1} m/s.",
                this);
        }

        motor.SetSpeedBoost(speedBoostAmount);
    }

    private void OnTriggerExit(Collider other)
    {
        BallMovementMotor motor =
            other.GetComponent<BallMovementMotor>()
            ?? other.GetComponentInParent<BallMovementMotor>();

        if (motor == null) return;

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[SPEED BOOST] Bola saliendo — velocidad al salir: {motor.CurrentPlanarVelocity:F1} m/s.",
                this);
        }

        motor.ClearSpeedBoost();
    }

    #endregion
}