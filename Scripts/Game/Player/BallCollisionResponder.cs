using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maneja la respuesta de la pelota ante colisiones con obstáculos del escenario.
/// </summary>
[DefaultExecutionOrder(-10)]
[RequireComponent(typeof(Rigidbody))]
public sealed class BallCollisionResponder : MonoBehaviour
{
    #region Inspector

    [Header("Referencias")]
    [SerializeField][Tooltip("Motor de movimiento de la pelota.")]
    private BallMovementMotor movementMotor;

    [SerializeField][Tooltip("Rigidbody de la pelota.")]
    private Rigidbody rb;

    [Header("Layers")]
    [SerializeField][Tooltip("Capas de suelo. No aplican retroceso ni bloqueo.")]
    private LayerMask groundLayers;

    [SerializeField][Tooltip("Capas de barreras. Usan rebote conservando velocidad reducida.")]
    private LayerMask barrierLayers;

    [SerializeField][Tooltip("Capas de obstáculos inamovibles.")]
    private LayerMask immovableLayers;

    [SerializeField][Tooltip("Capas de obstáculos empujables.")]
    private LayerMask pushableLayers;

    [Header("Barreras")]
    [SerializeField][Tooltip("Si activo, las barreras conservan exactamente la velocidad planar de entrada antes de aplicar la reduccion.")]
    private bool preserveBarrierSpeed = true;

    [SerializeField][Tooltip("Velocidad mínima para resolver dirección cuando la pelota llega casi detenida a una barrera.")]
    private float minimumBarrierDirectionSpeed = 0.15f;

    [SerializeField][Tooltip("Fracción de la velocidad de impacto conservada en el rebote. " +
                             "0.3 = pierde el 70% de velocidad al chocar. Impacto proporcional: lento=rebote pequeño, rápido=rebote mayor.")]
    [Range(0f, 1f)]
    private float barrierBounceSpeedRetention = 0.30f;

    [SerializeField][Tooltip("Componente frontal mínima del impacto para aplicar rebote. " +
                             "0.40 filtra los micro-rebotes en juntas entre CapsuleColliders adyacentes.")]
    [Range(0f, 0.8f)]
    private float barrierFrontalImpactThreshold = 0.40f;

    [SerializeField][Tooltip("Si activo, dibuja logs del rebote de barrera.")]
    private bool debugBarrierBounce;

    [Header("Fallback")]
    [SerializeField][Tooltip("Si activo, las colisiones sin capa configurada se tratan como obstáculos.")]
    private bool handleUnclassifiedCollisionsAsObstacles = false;

    [SerializeField][Tooltip("Multiplicador de velocidad para colisiones sin perfil.")][Range(0f, 1f)]
    private float defaultSpeedMultiplier = 0.9f;

    [SerializeField][Tooltip("Velocidad mínima de retroceso para colisiones sin perfil.")]
    private float defaultRecoilSpeed = 1.25f;

    [SerializeField][Tooltip("Duración mínima de supresión de tracción para colisiones sin perfil.")]
    private float defaultDriveSuppression = 0.08f;

    [Header("Bloqueo")]
    [SerializeField][Tooltip("Dot mínimo para considerar que existe bloqueo frontal real.")][Range(0f, 1f)]
    private float forwardBlockThreshold = 0.35f;

    [SerializeField][Tooltip("Velocidad mínima para usar la velocidad real como dirección de impacto.")]
    private float minimumVelocityDirectionSpeed = 0.15f;

    #endregion

    #region Runtime

    private readonly HashSet<Collider> activeObstacleContacts = new HashSet<Collider>();

    private Vector3 blockingNormal;
    private bool hasBlockingContact;

    private Vector3 anyContactNormal;
    private bool hasAnyContact;

    #endregion

    #region Properties

    public bool HasBlockingContact    => hasBlockingContact;
    public Vector3 BlockingNormal     => blockingNormal;
    public Vector3 AnyContactNormal   => anyContactNormal;
    public bool HasAnyContact         => hasAnyContact;

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
        RebuildBlockingState();
    }

    private void OnCollisionEnter(Collision collision)
    {
        Collider otherCollider = collision.collider;
        if (otherCollider == null) return;

        int otherLayer = otherCollider.gameObject.layer;

        if (IsGroundLayer(otherLayer)) return;

        if (IsInLayerMask(otherLayer, barrierLayers))
        {
            HandleBarrierCollision(collision);
            return;
        }

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

        if (handleUnclassifiedCollisionsAsObstacles)
            HandleDefaultCollision(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        Collider otherCollider = collision.collider;
        if (otherCollider == null) return;

        int otherLayer = otherCollider.gameObject.layer;
        if (IsGroundLayer(otherLayer)) return;

        RegisterBlockingFromCollision(collision);
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.collider != null)
            activeObstacleContacts.Remove(collision.collider);
    }

    #endregion

    #region Collision Handling

    /// <summary>
    /// Maneja rebote contra barrera.
    /// La velocidad de rebote se reduce por barrierBounceSpeedRetention antes de pasarse
    /// al motor, garantizando que impactos lentos produzcan rebotes pequeños y rápidos mayores.
    /// </summary>
    private void HandleBarrierCollision(Collision collision)
    {
        if (movementMotor == null || !movementMotor.CanProcessBarrierBounce) return;

        Vector3 contactNormal = AverageContactNormal(collision);
        contactNormal.y = 0f;

        if (contactNormal.sqrMagnitude < 0.0001f) return;
        contactNormal.Normalize();

        Vector3 relativeVelocity = collision.relativeVelocity;
        relativeVelocity.y = 0f;
        float incomingSpeed = relativeVelocity.magnitude;

        Vector3 incomingDirection;

        if (incomingSpeed >= minimumBarrierDirectionSpeed)
        {
            incomingDirection = relativeVelocity.normalized;
        }
        else
        {
            incomingDirection = -contactNormal;
            incomingSpeed     = minimumBarrierDirectionSpeed;
        }

        float frontalDot = Vector3.Dot(incomingDirection, -contactNormal);

        if (frontalDot < barrierFrontalImpactThreshold) return;

        Vector3 reflectedDirection = Vector3.Reflect(incomingDirection, contactNormal);
        reflectedDirection.y = 0f;

        if (reflectedDirection.sqrMagnitude < 0.0001f) reflectedDirection = contactNormal;
        reflectedDirection.Normalize();

        // Reducir la velocidad del rebote según barrierBounceSpeedRetention.
        // El jugador pierde la mayor parte de la velocidad al chocar, proporcional al impacto.
        float bounceSpeed = incomingSpeed * barrierBounceSpeedRetention;

        if (debugBarrierBounce)
        {
            Debug.Log(
                $"[BallCollisionResponder] Barrier bounce | " +
                $"InSpeed: {incomingSpeed:F2} | BounceSpeed: {bounceSpeed:F2} | " +
                $"FrontalDot: {frontalDot:F2} | Reflected: {reflectedDirection}", this);
        }

        movementMotor.ApplyBarrierBounce(reflectedDirection, bounceSpeed, contactNormal);
    }

    private void HandleImmovableCollision(Collision collision, Collider otherCollider)
    {
        ImmovableObstacle obstacle = otherCollider.GetComponent<ImmovableObstacle>();

        float speedMultiplier = obstacle != null ? obstacle.SpeedMultiplier : defaultSpeedMultiplier;
        float recoilSpeed     = obstacle != null ? obstacle.RecoilSpeed     : defaultRecoilSpeed;

        movementMotor.MultiplySpeed(speedMultiplier);

        Vector3 recoilDirection   = CalculateRecoilDirection(collision);
        float resolvedRecoilSpeed = CalculateResolvedRecoilSpeed(recoilSpeed);

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
        if (driveSuppressionDuration > 0f) movementMotor.SuppressDrive(driveSuppressionDuration);

        Rigidbody otherRigidbody = collision.rigidbody;
        if (otherRigidbody == null || otherRigidbody.isKinematic) return;

        ApplyPushableForces(collision, otherRigidbody, pushImpulse, lateralPushFactor, upwardImpulse, torqueImpulse);
    }

    private void HandleDefaultCollision(Collision collision)
    {
        movementMotor.MultiplySpeed(defaultSpeedMultiplier);
        movementMotor.ApplyImpactRecoil(CalculateRecoilDirection(collision) * CalculateResolvedRecoilSpeed(defaultRecoilSpeed));
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

    private void RegisterBlockingFromCollision(Collision collision)
    {
        int contactCount = collision.contactCount;
        if (contactCount <= 0) return;

        Vector3 planarMotionDirection     = GetPlanarMotionDirection();
        Vector3 accumulatedBlockingNormal = Vector3.zero;
        Vector3 accumulatedAnyNormal      = Vector3.zero;
        int blockingCount = 0;
        int anyCount      = 0;

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
        Collision collision, Rigidbody targetRigidbody,
        float pushImpulse, float lateralPushFactor, float upwardImpulse, float torqueImpulse)
    {
        Vector3 mainDirection = GetPlanarMotionDirection();
        Vector3 right         = Vector3.Cross(Vector3.up, mainDirection).normalized;

        ContactPoint contact = collision.GetContact(0);
        Vector3 toContact    = contact.point - transform.position;
        toContact.y          = 0f;

        float sideSign = toContact.sqrMagnitude > 0.0001f
            ? Mathf.Sign(Vector3.Dot(toContact.normalized, right))
            : 0f;

        Vector3 finalDirection = mainDirection + right * sideSign * lateralPushFactor;
        finalDirection.y = 0f;
        finalDirection = finalDirection.sqrMagnitude < 0.0001f ? mainDirection : finalDirection.normalized;

        Vector3 impulse = finalDirection * pushImpulse;
        impulse.y += upwardImpulse;
        targetRigidbody.AddForceAtPosition(impulse, contact.point, ForceMode.Impulse);

        if (torqueImpulse <= 0f) return;

        Vector3 torqueAxis = Vector3.Cross(Vector3.up, finalDirection).normalized;
        targetRigidbody.AddTorque(torqueAxis * torqueImpulse * (sideSign == 0f ? 1f : sideSign), ForceMode.Impulse);
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