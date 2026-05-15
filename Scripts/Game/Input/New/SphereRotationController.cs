using UnityEngine;

/// <summary>
/// Gestiona la dirección lógica de la cara de la pelota.
///
/// Sistema de rotación por objetivo:
/// En lugar de recibir un eje horizontal continuo y rotar por velocidad angular,
/// recibe una dirección objetivo en espacio mundo (<see cref="SetTargetForward"/>) y
/// rota <see cref="CurrentForward"/> hacia ella a <see cref="faceRotationSpeed"/> grados/segundo.
/// El resultado se siente "casi instantáneo" con el valor por defecto (~720°/s),
/// pero sigue siendo configurable y visualmente suave.
///
/// La cara siempre es una dirección planar (Y = 0). La componente Y se elimina
/// automáticamente de cualquier dirección recibida.
///
/// Consumers típicos:
/// - <see cref="DirectionalJoystickController"/>: actualiza el objetivo cada frame con la dirección del joystick.
/// - <see cref="SwipeDirectionController"/>: establece el objetivo una vez por swipe.
/// - <see cref="BallMovementMotor"/>: lee <see cref="CurrentForward"/> en cada FixedUpdate para steering.
/// - <see cref="CameraFollowController"/>: lee <see cref="CurrentForward"/> para orientar la cámara.
/// </summary>
[DefaultExecutionOrder(-20)]
[RequireComponent(typeof(Rigidbody))]
public sealed class SphereRotationController : MonoBehaviour
{
    #region Inspector

    [Header("Referencias")]
    [SerializeField]
    [Tooltip("Rigidbody de la pelota. Se usa para alinear la rotación visual y eliminar velocidad angular física.")]
    private Rigidbody rb;

    [Header("Rotación hacia Objetivo")]
    [SerializeField]
    [Tooltip("Velocidad en grados/segundo a la que la cara rota hacia el objetivo.\n" +
             "720°/s = 90° en 0.12s, 180° en 0.25s → sensación de 'casi instantáneo'.\n" +
             "Valores recomendados: 540–900°/s.")]
    private float faceRotationSpeed = 720f;

    [SerializeField]
    [Tooltip("Ángulo en grados por debajo del cual se considera que la cara llegó al objetivo.\n" +
             "Valores entre 2°–5° son recomendados. Muy bajo puede causar micro-oscilaciones.")]
    [Range(0.5f, 15f)]
    private float alignmentThreshold = 3f;

    [Header("Visual")]
    [SerializeField]
    [Tooltip("Si activo, aplica la rotación lógica (yaw) al Rigidbody cada frame para\n" +
             "alinear visualmente el modelo 3D con la dirección de la cara.\n" +
             "Desactivar si el modelo no necesita rotar (p.ej. esfera texturizada).")]
    private bool alignVisualYawToForward = true;

    [Header("Debug")]
    [SerializeField]
    [Tooltip("Muestra logs del forward actual, objetivo y estado de alineación.")]
    private bool debugForward;

    #endregion

    #region Runtime

    private Vector3 currentForward  = Vector3.forward;
    private Vector3 targetForward;
    private bool    hasActiveTarget;
    private bool    isAlignedWithTarget;

    #endregion

    #region Properties

    /// <summary>
    /// Dirección de la cara de la pelota en espacio mundo, normalizada y planar (Y = 0).
    /// Esta es la "cara principal" que define hacia dónde se moverá la bola.
    /// </summary>
    public Vector3 CurrentForward => currentForward;

    /// <summary>
    /// <c>true</c> si la cara llegó al objetivo (ángulo ≤ <see cref="alignmentThreshold"/>).
    /// Consumido por <see cref="DirectionalJoystickController"/> para saber cuándo
    /// dar el kickstart o activar el mantenimiento de velocidad.
    /// </summary>
    public bool IsAlignedWithTarget => isAlignedWithTarget;

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        // Inicializar currentForward desde la orientación actual del Transform.
        Vector3 initialFwd = transform.forward;
        initialFwd.y = 0f;
        currentForward = initialFwd.sqrMagnitude > 0.0001f
            ? initialFwd.normalized
            : Vector3.forward;
    }

    private void FixedUpdate()
    {
        ApplyTargetRotation();
        ClearPhysicalAngularVelocity();
        ApplyVisualRotation();
    }

    private void OnValidate()
    {
        faceRotationSpeed  = Mathf.Max(1f, faceRotationSpeed);
        alignmentThreshold = Mathf.Clamp(alignmentThreshold, 0.5f, 15f);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Establece la dirección objetivo hacia la que debe rotar la cara.
    /// La cara rotará a <see cref="faceRotationSpeed"/> grados/segundo cada FixedUpdate.
    /// Puede llamarse cada frame para actualizar el objetivo continuamente (joystick).
    /// </summary>
    /// <param name="worldForward">Dirección en espacio mundo. La componente Y se ignora.</param>
    public void SetTargetForward(Vector3 worldForward)
    {
        worldForward.y = 0f;
        if (worldForward.sqrMagnitude < 0.0001f) return;

        targetForward   = worldForward.normalized;
        hasActiveTarget = true;
    }

    /// <summary>
    /// Cancela el objetivo de rotación activo.
    /// La cara se detiene en su posición actual y <see cref="IsAlignedWithTarget"/> pasa a <c>false</c>.
    /// Llamar cuando el joystick se suelta para dejar de rotar.
    /// </summary>
    public void ClearTarget()
    {
        hasActiveTarget      = false;
        isAlignedWithTarget  = false;
    }

    /// <summary>
    /// Fuerza la cara directamente a la dirección dada sin animación de rotación.
    /// Usado por swipes de dirección que ya manejan la transición por otro medio.
    /// </summary>
    public void SetForward(Vector3 worldForward)
    {
        worldForward.y = 0f;
        if (worldForward.sqrMagnitude < 0.0001f) return;

        currentForward       = worldForward.normalized;
        targetForward        = currentForward;
        hasActiveTarget      = false;
        isAlignedWithTarget  = true;

        ApplyVisualRotation();
    }

    /// <summary>
    /// Alinea instantáneamente la cara a la dirección de una rotación.
    /// Usado por <see cref="BallMovementMotor.TeleportTo"/> y respawn.
    /// </summary>
    public void SnapToRotation(Quaternion rotation)
    {
        Vector3 forward = rotation * Vector3.forward;
        SetForward(forward);
    }

    #endregion

    #region Private

    /// <summary>
    /// Rota <see cref="currentForward"/> hacia <see cref="targetForward"/> a la velocidad configurada.
    /// Cuando el ángulo cae por debajo de <see cref="alignmentThreshold"/>, snappea y marca alineación.
    /// </summary>
    private void ApplyTargetRotation()
    {
        if (!hasActiveTarget) return;

        float angle = Vector3.Angle(currentForward, targetForward);

        if (angle <= alignmentThreshold)
        {
            currentForward      = targetForward;
            isAlignedWithTarget = true;

            if (debugForward)
                Debug.Log($"[SphereRotationController] ALIGNED → {currentForward}");

            return;
        }

        float maxDelta = faceRotationSpeed * Mathf.Deg2Rad * Time.fixedDeltaTime;
        currentForward = Vector3.RotateTowards(currentForward, targetForward, maxDelta, 0f);
        currentForward.y = 0f;

        if (currentForward.sqrMagnitude < 0.0001f)
            currentForward = targetForward;
        else
            currentForward.Normalize();

        isAlignedWithTarget = false;

        if (debugForward)
            Debug.Log($"[SphereRotationController] Rotating → {currentForward:F2} (angle: {angle:F1}°)");
    }

    private void ApplyVisualRotation()
    {
        if (!alignVisualYawToForward || rb == null) return;
        if (currentForward.sqrMagnitude < 0.0001f) return;

        rb.rotation = Quaternion.LookRotation(currentForward, Vector3.up);
    }

    private void ClearPhysicalAngularVelocity()
    {
        if (rb == null) return;
        rb.angularVelocity = Vector3.zero;
    }

    #endregion
}