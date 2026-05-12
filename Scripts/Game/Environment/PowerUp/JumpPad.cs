using UnityEngine;

/// <summary>
/// Power-up de salto. Al ser pisado aplica un impulso vertical a la bola.
/// </summary>
public sealed class JumpPad : MonoBehaviour
{
    #region Inspector

    [Header("Configuración")]
    [SerializeField]
    [Tooltip("Velocidad vertical aplicada a la bola (m/s). Recomendado: 8–15.")]
    private float jumpForce = 10f;

    [SerializeField]
    [Tooltip("Tiempo mínimo entre activaciones (segundos).")]
    private float cooldownDuration = 0.5f;

    #endregion

    #region Runtime

    private float lastActivationTime = -999f;

    #endregion

    #region Physics

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        float timeSinceLast = Time.time - lastActivationTime;

        if (timeSinceLast < cooldownDuration)
        {
            Debug.Log(
                $"[JUMP PAD] Colisión detectada en '{other.name}' pero cooldown activo " +
                $"({timeSinceLast:F2}s / {cooldownDuration:F2}s requeridos).", this);
            return;
        }

        BallMovementMotor motor = other.GetComponentInParent<BallMovementMotor>()
            ?? other.GetComponent<BallMovementMotor>();

        if (motor == null)
        {
            Debug.LogWarning(
                "[JUMP PAD] ⚠ Colisión con 'Player' detectada PERO BallMovementMotor no encontrado. " +
                "Verifica que el tag 'Player' esté en el objeto correcto.", this);
            return;
        }

        lastActivationTime = Time.time;
        Debug.Log(
            $"[JUMP PAD] ✓ Colisión detectada — aplicando impulso {jumpForce} m/s a '{other.name}'. " +
            $"Velocidad planar actual: {motor.CurrentPlanarVelocity:F1} m/s.", this);

        motor.ApplyJump(jumpForce);
    }

    #endregion
}