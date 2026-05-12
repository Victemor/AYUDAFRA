using UnityEngine;

/// <summary>
/// Zona de aceleración. Mientras la bola esté dentro del trigger,
/// puede superar su límite de velocidad normal por el multiplicador configurado.
///
/// El prefab de esta zona debe incluir:
/// - Una malla visual del camino elevado.
/// - Un collider físico (no trigger) que sirva de suelo para la bola.
/// - Este trigger volumétrico que abarca toda la zona.
/// </summary>
public sealed class SpeedBoostZone : MonoBehaviour
{
    #region Inspector

    [Header("Configuración")]
    [SerializeField]
    [Tooltip("Multiplicador de velocidad máxima mientras la bola está en la zona.\n" +
             "Ejemplo: 1.5 → puede llegar a 1.5× la velocidad normal.")]
    [Range(1f, 3f)]
    private float speedBoostMultiplier = 1.5f;

    #endregion

    #region Physics

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        BallMovementMotor motor = other.GetComponentInParent<BallMovementMotor>()
            ?? other.GetComponent<BallMovementMotor>();

        if (motor == null)
        {
            Debug.LogWarning(
                "[SPEED BOOST] ⚠ Entrada con 'Player' detectada PERO BallMovementMotor no encontrado.", this);
            return;
        }

        Debug.Log(
            $"[SPEED BOOST] ✓ Bola ENTRANDO a zona de aceleración — " +
            $"multiplicador: {speedBoostMultiplier:F2}× — " +
            $"velocidad actual: {motor.CurrentPlanarVelocity:F1} m/s.", this);

        motor.SetSpeedBoostMultiplier(speedBoostMultiplier);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        BallMovementMotor motor = other.GetComponentInParent<BallMovementMotor>()
            ?? other.GetComponent<BallMovementMotor>();

        if (motor == null)
        {
            return;
        }

        Debug.Log(
            $"[SPEED BOOST] Bola SALIENDO de zona de aceleración — " +
            $"velocidad al salir: {motor.CurrentPlanarVelocity:F1} m/s.", this);

        motor.ClearSpeedBoost();
    }

    #endregion
}