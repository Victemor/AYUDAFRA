using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maneja la respuesta de la pelota ante colisiones con obstáculos del escenario.
///
/// Responsabilidades:
/// - Aplicar retroceso y supresión de drive en colisiones con obstáculos inamovibles.
/// - Aplicar fuerzas a obstáculos empujables.
/// - Mantener el estado de bloqueo frontal (<see cref="HasBlockingContact"/>) para que
///   <see cref="BallMovementMotor"/> pueda proyectar el steering sobre el plano de la pared.
///
/// Diseño de barreras:
/// El rebote es manejado por PhysicMaterials. Este componente solo realiza una corrección
/// mínima al colisionar con una barrera: cancela la componente de velocidad que apunta
/// INTO la pared para que el impulso de rebote del PhysicMaterial no sea contrarrestado
/// por la inercia previa de la bola. El bloqueo frontal sigue registrándose vía
/// <see cref="OnCollisionStay"/> para que el steering se proyecte sobre el plano de la pared.
/// </summary>
[DefaultExecutionOrder(-10)]
[RequireComponent(typeof(Rigidbody))]
public sealed class BallCollisionResponder : MonoBehaviour
{
    #region Inspector

    [Header("Referencias")]
    [SerializeField]
    [Tooltip("Motor de movimiento de la pelota.")]
    private BallMovementMotor movementMotor;

    [SerializeField]
    [Tooltip("Rigidbody de la pelota.")]
    private Rigidbody rb;

    [Header("Layers")]
    [SerializeField]
    [Tooltip("Capas de suelo. No aplican retroceso ni bloqueo.")]
    private LayerMask groundLayers;

    [SerializeField]
    [Tooltip("Capas de barreras laterales del track. " +
             "Solo se usa para cancelar la componente de velocidad INTO la pared al chocar. " +
             "El rebote lo gestiona el PhysicMaterial.")]
    private LayerMask barrierLayers;

    [SerializeField]
    [Tooltip("Capas de obstáculos inamovibles.")]
    private LayerMask immovableLayers;

    [SerializeField]
    [Tooltip("Capas de obstáculos empujables.")]
    private LayerMask pushableLayers;

    [Header("Fallback")]
    [SerializeField]
    [Tooltip("Si activo, las colisiones cuya capa no está en immovableLayers ni pushableLayers " +
             "se tratan como obstáculos con los valores por defecto. " +
             "Desactivado por defecto para no interferir con las barreras del track.")]
    private bool handleUnclassifiedCollisionsAsObstacles = false;

    [SerializeField]
    [Tooltip("Multiplicador de velocidad para colisiones sin perfil de obstáculo asignado.")]
    [Range(0f, 1f)]
    private float defaultSpeedMultiplier = 0.9f;

    [SerializeField]
    [Tooltip("Velocidad mínima de retroceso para colisiones sin perfil de obstáculo asignado.")]
    private float defaultRecoilSpeed = 1.25f;

    [SerializeField]
    [Tooltip("Duración mínima de supresión de drive para colisiones sin perfil de obstáculo asignado.")]
    private float defaultDriveSuppression = 0.08f;

    [Header("Bloqueo")]
    [SerializeField]
    [Tooltip("Dot product mínimo entre la dirección de movimiento y la normal inversa del contacto " +
             "para considerarlo un bloqueo frontal real. Evita falsos positivos en contactos laterales.")]
    [Range(0f, 1f)]
    private float forwardBlockThreshold = 0.35f;

    [SerializeField]
    [Tooltip("Velocidad planar mínima en m/s para usar la velocidad real como dirección de impacto. " +
             "Por debajo de este valor se usa el forward guardado del motor.")]
    private float minimumVelocityDirectionSpeed = 0.15f;

    #endregion

    #region Runtime

    private readonly HashSet<Collider> activeObstacleContacts = new HashSet<Collider>();

    private Vector3 blockingNormal;
    private bool    hasBlockingContact;

    private Vector3 anyContactNormal;
    private bool    hasAnyContact;

    #endregion

    #region Properties

    /// <summary>
    /// <c>true</c> si hay un contacto frontal activo que bloquea la dirección de movimiento.
    /// Calculado cada FixedUpdate a partir de <see cref="OnCollisionStay"/>.
    /// Consumido por <see cref="BallMovementMotor"/> para proyectar steering e impulsos sobre el plano de la pared.
    /// </summary>
    public bool    HasBlockingContact => hasBlockingContact;

    /// <summary>Normal promediada del contacto de bloqueo actual.</summary>
    public Vector3 BlockingNormal     => blockingNormal;

    /// <summary>Normal promediada de cualquier contacto no-suelo activo.</summary>
    public Vector3 AnyContactNormal   => anyContactNormal;

    /// <summary><c>true</c> si hay algún contacto no-suelo activo, independientemente de si bloquea.</summary>
    public bool    HasAnyContact      => hasAnyContact;

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        rb            = GetComponent<Rigidbody>();
        movementMotor = GetComponent<BallMovementMotor>();
    }

    private void Awake()
    {
        if (rb == null)            rb            = GetComponent<Rigidbody>();
        if (movementMotor == null) movementMotor = GetComponent<BallMovementMotor>();
    }

    private void FixedUpdate()
    {
        // Limpiar estado de bloqueo cada frame; OnCollisionStay lo reconstruye si el contacto persiste.
        RebuildBlockingState();
    }

    private void OnCollisionEnter(Collision collision)
    {
        Collider otherCollider = collision.collider;
        if (otherCollider == null) return;

        int otherLayer = otherCollider.gameObject.layer;

        // El suelo nunca genera retroceso ni bloqueo.
        if (IsGroundLayer(otherLayer)) return;

        // Barrera: cancelar la componente de velocidad INTO la pared para que
        // el rebote del PhysicMaterial no sea anulado por la inercia de la bola.
        if (IsInLayerMask(otherLayer, barrierLayers))
        {
            CancelIntoWallVelocity(collision);
            return;
        }

        // Evitar procesar el mismo obstáculo varias veces si permanece en contacto.
        if (activeObstacleContacts.Contains(otherCollider)) return;

        activeObstacleContacts.Add(otherCollider);

        if (IsInLayerMask(otherLayer, immovableLayers))
        {
            HandleImmovableCollision(collision, otherCollider);
            return;
        }

        if (IsInLayerMask(otherLayer, pushableLayers))
        {
            HandlePushableCollision(collision, otherCollider);
            return;
        }

        // Las barreras del track caen aquí como colisiones no clasificadas.
        // Como handleUnclassifiedCollisionsAsObstacles = false por defecto,
        // no se aplica ningún código de retroceso — el PhysicMaterial lo gestiona.
        if (handleUnclassifiedCollisionsAsObstacles)
        {
            HandleDefaultCollision(collision);
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        Collider otherCollider = collision.collider;
        if (otherCollider == null) return;

        // El suelo no genera bloqueo.
        if (IsGroundLayer(otherCollider.gameObject.layer)) return;

        // Registrar bloqueo para barreras, obstáculos o cualquier otro contacto persistente.
        RegisterBlockingFromCollision(collision);
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.collider != null)
        {
            activeObstacleContacts.Remove(collision.collider);
        }
    }

    #endregion

    #region Collision Handling

    /// <summary>
    /// Cancela la componente de velocidad planar que apunta INTO la barrera.
    ///
    /// El PhysicMaterial ya aplicó su impulso de rebote antes de que este método se ejecute.
    /// Sin esta corrección, la inercia previa de la bola lucha contra ese impulso y el rebote
    /// se siente amortiguado o la bola queda pegada. Cancelar solo la componente perpendicular
    /// preserva la velocidad lateral (deslizamiento) y deja al PhysicMaterial dictar el rebote.
    /// </summary>
    private void CancelIntoWallVelocity(Collision collision)
    {
        Vector3 normal = AverageContactNormal(collision);
        normal.y = 0f;
        if (normal.sqrMagnitude < 0.0001f) return;
        normal.Normalize();

        Vector3 v        = rb.linearVelocity;
        float   intoWall = Vector3.Dot(new Vector3(v.x, 0f, v.z), -normal);

        if (intoWall > 0f)
        {
            // Eliminar únicamente la componente INTO la pared. Lateral y Y se conservan.
            rb.linearVelocity = new Vector3(
                v.x + normal.x * intoWall,
                v.y,
                v.z + normal.z * intoWall);
        }

        // Bloquear steering e impulsos hasta que el jugador dé input explícito.
        // La fricción pasiva decelerará el rebote del PhysicMaterial de forma natural.
        movementMotor?.NotifyBarrierHit();
    }

    private void HandleImmovableCollision(Collision collision, Collider otherCollider)
    {
        ImmovableObstacle obstacle = otherCollider.GetComponent<ImmovableObstacle>();

        float speedMultiplier = obstacle != null ? obstacle.SpeedMultiplier : defaultSpeedMultiplier;
        float recoilSpeed     = obstacle != null ? obstacle.RecoilSpeed     : defaultRecoilSpeed;

        movementMotor.MultiplySpeed(speedMultiplier);

        Vector3 recoilDirection   = CalculateRecoilDirection(collision);
        float   resolvedRecoilSpeed = CalculateResolvedRecoilSpeed(recoilSpeed);

        movementMotor.ApplyImpactRecoil(recoilDirection * resolvedRecoilSpeed);
    }

    private void HandlePushableCollision(Collision collision, Collider otherCollider)
    {
        PushableObstacle obstacle = otherCollider.GetComponent<PushableObstacle>();

        float speedMultiplier          = obstacle != null ? obstacle.SpeedMultiplier          : 0.9f;
        float driveSuppressionDuration = obstacle != null ? obstacle.DriveSuppressionDuration : 0.04f;
        float pushImpulse              = obstacle != null ? obstacle.PushImpulse              : 2f;
        float lateralPushFactor        = obstacle != null ? obstacle.LateralPushFactor        : 0.35f;
        float upwardImpulse            = obstacle != null ? obstacle.UpwardImpulse            : 0.15f;
        float torqueImpulse            = obstacle != null ? obstacle.TorqueImpulse            : 1.25f;

        movementMotor.MultiplySpeed(speedMultiplier);

        if (driveSuppressionDuration > 0f)
        {
            movementMotor.SuppressDrive(driveSuppressionDuration);
        }

        Rigidbody otherRigidbody = collision.rigidbody;
        if (otherRigidbody == null || otherRigidbody.isKinematic) return;

        ApplyPushableForces(collision, otherRigidbody, pushImpulse, lateralPushFactor, upwardImpulse, torqueImpulse);
    }

    private void HandleDefaultCollision(Collision collision)
    {
        movementMotor.MultiplySpeed(defaultSpeedMultiplier);
        movementMotor.ApplyImpactRecoil(
            CalculateRecoilDirection(collision) * CalculateResolvedRecoilSpeed(defaultRecoilSpeed));
        movementMotor.SuppressDrive(defaultDriveSuppression);
    }

    #endregion

    #region Blocking State

    private void RebuildBlockingState()
    {
        hasBlockingContact = false;
        blockingNormal     = Vector3.zero;
        hasAnyContact      = false;
        anyContactNormal   = Vector3.zero;
    }

    /// <summary>
    /// Acumula normals de contacto del frame actual.
    /// Distingue entre contactos frontales bloqueantes y cualquier contacto lateral.
    /// </summary>
    private void RegisterBlockingFromCollision(Collision collision)
    {
        int contactCount = collision.contactCount;
        if (contactCount <= 0) return;

        Vector3 planarMotionDirection     = GetPlanarMotionDirection();
        Vector3 accumulatedBlockingNormal = Vector3.zero;
        Vector3 accumulatedAnyNormal      = Vector3.zero;
        int     blockingCount             = 0;
        int     anyCount                  = 0;

        for (int i = 0; i < contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;
            normal.y = 0f;
            if (normal.sqrMagnitude < 0.0001f) continue;
            normal.Normalize();

            accumulatedAnyNormal += normal;
            anyCount++;

            if (Vector3.Dot(planarMotionDirection, -normal) >= forwardBlockThreshold)
            {
                accumulatedBlockingNormal += normal;
                blockingCount++;
            }
        }

        if (anyCount > 0)
        {
            Vector3 resolved = accumulatedAnyNormal / anyCount;
            if (resolved.sqrMagnitude > 0.0001f)
            {
                resolved.Normalize();
                hasAnyContact    = true;
                anyContactNormal = anyContactNormal.sqrMagnitude > 0.0001f
                    ? (anyContactNormal + resolved).normalized
                    : resolved;
            }
        }

        if (blockingCount <= 0) return;

        Vector3 resolvedBlocking = accumulatedBlockingNormal / blockingCount;
        if (resolvedBlocking.sqrMagnitude < 0.0001f) return;
        resolvedBlocking.Normalize();

        if (!hasBlockingContact)
        {
            hasBlockingContact = true;
            blockingNormal     = resolvedBlocking;
            return;
        }

        blockingNormal = (blockingNormal + resolvedBlocking).normalized;
    }

    #endregion

    #region Recoil

    private Vector3 CalculateRecoilDirection(Collision collision)
    {
        Vector3 contactNormal   = AverageContactNormal(collision);
        contactNormal.y         = 0f;
        Vector3 motionDirection = GetPlanarMotionDirection();

        if (contactNormal.sqrMagnitude < 0.0001f) return -motionDirection;

        contactNormal.Normalize();
        Vector3 reflected = Vector3.Reflect(motionDirection, contactNormal);
        reflected.y = 0f;

        return reflected.sqrMagnitude < 0.0001f ? -motionDirection : reflected.normalized;
    }

    private static Vector3 AverageContactNormal(Collision collision)
    {
        int count = collision.contactCount;
        if (count == 0) return Vector3.zero;
        if (count == 1) return collision.GetContact(0).normal;

        Vector3 sum = Vector3.zero;
        for (int i = 0; i < count; i++) sum += collision.GetContact(i).normal;
        return sum / count;
    }

    private float CalculateResolvedRecoilSpeed(float minimumRecoilSpeed)
    {
        Vector3 planar = rb.linearVelocity;
        planar.y = 0f;
        float recoilSpeed = Mathf.Max(planar.magnitude * 0.35f, minimumRecoilSpeed);
        if (movementMotor != null) recoilSpeed = Mathf.Min(recoilSpeed, movementMotor.MaxSpeed);
        return recoilSpeed;
    }

    #endregion

    #region Pushables

    private void ApplyPushableForces(
        Collision collision,
        Rigidbody targetRigidbody,
        float pushImpulse,
        float lateralPushFactor,
        float upwardImpulse,
        float torqueImpulse)
    {
        Vector3 mainDirection = GetPlanarMotionDirection();
        Vector3 right         = Vector3.Cross(Vector3.up, mainDirection).normalized;

        ContactPoint contact = collision.GetContact(0);
        Vector3      toContact = contact.point - transform.position;
        toContact.y = 0f;

        float sideSign = toContact.sqrMagnitude > 0.0001f
            ? Mathf.Sign(Vector3.Dot(toContact.normalized, right))
            : 0f;

        Vector3 finalDirection = mainDirection + right * sideSign * lateralPushFactor;
        finalDirection.y = 0f;
        finalDirection = finalDirection.sqrMagnitude < 0.0001f
            ? mainDirection
            : finalDirection.normalized;

        Vector3 impulse = finalDirection * pushImpulse;
        impulse.y += upwardImpulse;
        targetRigidbody.AddForceAtPosition(impulse, contact.point, ForceMode.Impulse);

        if (torqueImpulse <= 0f) return;

        Vector3 torqueAxis = Vector3.Cross(Vector3.up, finalDirection).normalized;
        targetRigidbody.AddTorque(
            torqueAxis * torqueImpulse * (sideSign == 0f ? 1f : sideSign),
            ForceMode.Impulse);
    }

    #endregion

    #region Helpers

    private bool IsGroundLayer(int layer) => IsInLayerMask(layer, groundLayers);

    private static bool IsInLayerMask(int layer, LayerMask mask) => (mask.value & (1 << layer)) != 0;

    private Vector3 GetPlanarMotionDirection()
    {
        Vector3 planar = rb != null ? rb.linearVelocity : Vector3.zero;
        planar.y = 0f;

        if (planar.magnitude >= minimumVelocityDirectionSpeed) return planar.normalized;

        if (movementMotor != null)
        {
            Vector3 last = movementMotor.LastValidMoveDirection;
            last.y = 0f;
            if (last.sqrMagnitude > 0.0001f) return last.normalized;
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;
        return forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
    }

    #endregion
}