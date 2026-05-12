using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maneja la respuesta de la pelota ante colisiones con obstáculos del escenario.
/// Las barreras usan rebote conservativo de velocidad; los obstáculos normales usan recoil o supresión.
/// </summary>
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
    [Tooltip("Capas que deben considerarse suelo. No aplican retroceso, bloqueo ni supresión de movimiento.")]
    private LayerMask groundLayers;

    [SerializeField]
    [Tooltip("Capas de barreras. Usan rebote directo conservando velocidad.")]
    private LayerMask barrierLayers;

    [SerializeField]
    [Tooltip("Capas que deben comportarse como obstáculos inamovibles.")]
    private LayerMask immovableLayers;

    [SerializeField]
    [Tooltip("Capas que deben comportarse como obstáculos empujables.")]
    private LayerMask pushableLayers;

    [Header("Barreras")]

    [SerializeField]
    [Tooltip("Si está activo, las barreras conservan exactamente la velocidad planar de entrada.")]
    private bool preserveBarrierSpeed = true;

    [SerializeField]
    [Tooltip("Velocidad mínima usada para resolver dirección cuando la pelota llega casi detenida a una barrera.")]
    private float minimumBarrierDirectionSpeed = 0.15f;

    [SerializeField]
    [Tooltip("Si está activo, dibuja logs del rebote de barrera.")]
    private bool debugBarrierBounce;

    [SerializeField]
    [Tooltip("Componente frontal mínima del impacto para aplicar rebote. " +
            "Valores bajos = solo rebota en impactos casi directos. " +
            "Impactos rasantes por debajo del umbral se ignoran completamente.")]
    [Range(0f, 0.8f)]
    private float barrierFrontalImpactThreshold = 0.25f;

    [Header("Fallback")]

    [SerializeField]
    [Tooltip("Si está activo, las colisiones que no pertenezcan a ninguna capa configurada se tratarán como obstáculos.")]
    private bool handleUnclassifiedCollisionsAsObstacles = false;

    [SerializeField]
    [Tooltip("Multiplicador de velocidad para colisiones sin perfil específico.")]
    [Range(0f, 1f)]
    private float defaultSpeedMultiplier = 0.9f;

    [SerializeField]
    [Tooltip("Velocidad mínima de retroceso para colisiones sin perfil específico.")]
    private float defaultRecoilSpeed = 1.25f;

    [SerializeField]
    [Tooltip("Duración mínima de supresión de tracción para colisiones sin perfil específico.")]
    private float defaultDriveSuppression = 0.08f;

    [Header("Bloqueo")]

    [SerializeField]
    [Tooltip("Dot mínimo para considerar que existe bloqueo frontal real.")]
    [Range(0f, 1f)]
    private float forwardBlockThreshold = 0.35f;

    [SerializeField]
    [Tooltip("Velocidad mínima para usar la velocidad real como dirección de impacto.")]
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

    /// <summary>
    /// Indica si existe una superficie bloqueando el avance frontal de la pelota.
    /// </summary>
    public bool HasBlockingContact => hasBlockingContact;

    /// <summary>
    /// Normal promedio del bloqueo frontal actual.
    /// </summary>
    public Vector3 BlockingNormal => blockingNormal;

    /// <summary>
    /// Normal promedio de todos los contactos activos no-suelo.
    /// </summary>
    public Vector3 AnyContactNormal => anyContactNormal;

    /// <summary>
    /// Indica si hay algún contacto activo no-suelo.
    /// </summary>
    public bool HasAnyContact => hasAnyContact;

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        rb = GetComponent<Rigidbody>();
        movementMotor = GetComponent<BallMovementMotor>();
    }

    private void Awake()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (movementMotor == null)
        {
            movementMotor = GetComponent<BallMovementMotor>();
        }
    }

    private void FixedUpdate()
    {
        RebuildBlockingState();
    }

    private void OnCollisionEnter(Collision collision)
    {
        Collider otherCollider = collision.collider;

        if (otherCollider == null)
        {
            return;
        }

        int otherLayer = otherCollider.gameObject.layer;

        if (IsGroundLayer(otherLayer))
        {
            return;
        }

        if (IsInLayerMask(otherLayer, barrierLayers))
        {
            HandleBarrierCollision(collision);
            return;
        }

        if (activeObstacleContacts.Contains(otherCollider))
        {
            return;
        }

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
        {
            HandleDefaultCollision(collision);
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        Collider otherCollider = collision.collider;
        if (otherCollider == null) return;

        int otherLayer = otherCollider.gameObject.layer;
        if (IsGroundLayer(otherLayer)) return;

        // Barreras: sin rebote en contacto sostenido.
        // Solo OnCollisionEnter dispara rebotes — esto evita rebotes espurios
        // por juntas entre cápsulas y el loop de rebotes al avanzar pegado.
        RegisterBlockingFromCollision(collision);
    }

    private void OnCollisionExit(Collision collision)
    {
        Collider otherCollider = collision.collider;

        if (otherCollider == null)
        {
            return;
        }

        activeObstacleContacts.Remove(otherCollider);
    }

    #endregion

    #region Collision Handling

    private void HandleBarrierCollision(Collision collision)
    {
        if (movementMotor == null || !movementMotor.CanProcessBarrierBounce)
            return;

        Vector3 contactNormal = AverageContactNormal(collision);
        contactNormal.y = 0f;

        if (contactNormal.sqrMagnitude < 0.0001f)
            return;

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
            // Velocidad insuficiente — separar directamente por la normal
            incomingDirection = -contactNormal;
            incomingSpeed     = minimumBarrierDirectionSpeed;
        }

        // Solo rebotar si el impacto tiene componente frontal significativa.
        // Impactos rasantes (avanzar pegado a la barrera, juntas entre cápsulas)
        // quedan por debajo del umbral y se ignoran completamente.
        float frontalDot = Vector3.Dot(incomingDirection, -contactNormal);

        if (frontalDot < barrierFrontalImpactThreshold)
            return;

        Vector3 reflectedDirection = Vector3.Reflect(incomingDirection, contactNormal);
        reflectedDirection.y = 0f;

        if (reflectedDirection.sqrMagnitude < 0.0001f)
            reflectedDirection = contactNormal;

        reflectedDirection.Normalize();

        if (debugBarrierBounce)
        {
            Debug.Log(
                $"[BallCollisionResponder] Barrier bounce | " +
                $"Speed: {incomingSpeed:F2} | FrontalDot: {frontalDot:F2} | " +
                $"Reflected: {reflectedDirection}", this);
        }

        movementMotor.ApplyBarrierBounce(reflectedDirection, incomingSpeed);
    }

    private void HandleImmovableCollision(Collision collision, Collider otherCollider)
    {
        ImmovableObstacle obstacle = otherCollider.GetComponent<ImmovableObstacle>();

        float speedMultiplier = obstacle != null
            ? obstacle.SpeedMultiplier
            : defaultSpeedMultiplier;

        float recoilSpeed = obstacle != null
            ? obstacle.RecoilSpeed
            : defaultRecoilSpeed;

        movementMotor.MultiplySpeed(speedMultiplier);

        Vector3 recoilDirection = CalculateRecoilDirection(collision);
        float resolvedRecoilSpeed = CalculateResolvedRecoilSpeed(recoilSpeed);

        movementMotor.ApplyImpactRecoil(recoilDirection * resolvedRecoilSpeed);
    }

    private void HandlePushableCollision(Collision collision, Collider otherCollider)
    {
        PushableObstacle obstacle = otherCollider.GetComponent<PushableObstacle>();

        float speedMultiplier = obstacle != null ? obstacle.SpeedMultiplier : 0.9f;
        float driveSuppressionDuration = obstacle != null ? obstacle.DriveSuppressionDuration : 0.04f;
        float pushImpulse = obstacle != null ? obstacle.PushImpulse : 2f;
        float lateralPushFactor = obstacle != null ? obstacle.LateralPushFactor : 0.35f;
        float upwardImpulse = obstacle != null ? obstacle.UpwardImpulse : 0.15f;
        float torqueImpulse = obstacle != null ? obstacle.TorqueImpulse : 1.25f;

        movementMotor.MultiplySpeed(speedMultiplier);

        if (driveSuppressionDuration > 0f)
        {
            movementMotor.SuppressDrive(driveSuppressionDuration);
        }

        Rigidbody otherRigidbody = collision.rigidbody;

        if (otherRigidbody == null || otherRigidbody.isKinematic)
        {
            return;
        }

        ApplyPushableForces(
            collision,
            otherRigidbody,
            pushImpulse,
            lateralPushFactor,
            upwardImpulse,
            torqueImpulse);
    }

    private void HandleDefaultCollision(Collision collision)
    {
        movementMotor.MultiplySpeed(defaultSpeedMultiplier);

        Vector3 recoilDirection = CalculateRecoilDirection(collision);
        float recoilSpeed = CalculateResolvedRecoilSpeed(defaultRecoilSpeed);

        movementMotor.ApplyImpactRecoil(recoilDirection * recoilSpeed);
        movementMotor.SuppressDrive(defaultDriveSuppression);
    }

    #endregion

    #region Blocking

    private void RebuildBlockingState()
    {
        hasBlockingContact = false;
        blockingNormal = Vector3.zero;

        hasAnyContact = false;
        anyContactNormal = Vector3.zero;
    }

    private void RegisterBlockingFromCollision(Collision collision)
    {
        int contactCount = collision.contactCount;

        if (contactCount <= 0)
        {
            return;
        }

        Vector3 planarMotionDirection = GetPlanarMotionDirection();
        Vector3 accumulatedBlockingNormal = Vector3.zero;
        Vector3 accumulatedAnyNormal = Vector3.zero;

        int blockingCount = 0;
        int anyCount = 0;

        for (int i = 0; i < contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            Vector3 normal = contact.normal;
            normal.y = 0f;

            if (normal.sqrMagnitude < 0.0001f)
            {
                continue;
            }

            normal.Normalize();

            accumulatedAnyNormal += normal;
            anyCount++;

            float frontalDot = Vector3.Dot(planarMotionDirection, -normal);

            if (frontalDot >= forwardBlockThreshold)
            {
                accumulatedBlockingNormal += normal;
                blockingCount++;
            }
        }

        if (anyCount > 0)
        {
            Vector3 resolvedAnyNormal = accumulatedAnyNormal / anyCount;

            if (resolvedAnyNormal.sqrMagnitude > 0.0001f)
            {
                resolvedAnyNormal.Normalize();

                hasAnyContact = true;
                anyContactNormal = anyContactNormal.sqrMagnitude > 0.0001f
                    ? (anyContactNormal + resolvedAnyNormal).normalized
                    : resolvedAnyNormal;
            }
        }

        if (blockingCount <= 0)
        {
            return;
        }

        Vector3 resolvedBlockingNormal = accumulatedBlockingNormal / blockingCount;

        if (resolvedBlockingNormal.sqrMagnitude < 0.0001f)
        {
            return;
        }

        resolvedBlockingNormal.Normalize();

        if (!hasBlockingContact)
        {
            hasBlockingContact = true;
            blockingNormal = resolvedBlockingNormal;
            return;
        }

        blockingNormal = (blockingNormal + resolvedBlockingNormal).normalized;
    }

    #endregion

    #region Recoil

    private Vector3 CalculateRecoilDirection(Collision collision)
    {
        Vector3 contactNormal = AverageContactNormal(collision);
        contactNormal.y = 0f;

        Vector3 motionDirection = GetPlanarMotionDirection();

        if (contactNormal.sqrMagnitude < 0.0001f)
        {
            return -motionDirection;
        }

        contactNormal.Normalize();

        Vector3 reflected = Vector3.Reflect(motionDirection, contactNormal);
        reflected.y = 0f;

        return reflected.sqrMagnitude < 0.0001f
            ? -motionDirection
            : reflected.normalized;
    }

    private static Vector3 AverageContactNormal(Collision collision)
    {
        int count = collision.contactCount;

        if (count == 0)
        {
            return Vector3.zero;
        }

        if (count == 1)
        {
            return collision.GetContact(0).normal;
        }

        Vector3 sum = Vector3.zero;

        for (int i = 0; i < count; i++)
        {
            sum += collision.GetContact(i).normal;
        }

        return sum / count;
    }

    private float CalculateResolvedRecoilSpeed(float minimumRecoilSpeed)
    {
        Vector3 currentPlanarVelocity = rb.linearVelocity;
        currentPlanarVelocity.y = 0f;

        float incomingSpeed = currentPlanarVelocity.magnitude;
        float recoilSpeed = Mathf.Max(incomingSpeed * 0.35f, minimumRecoilSpeed);

        if (movementMotor != null)
        {
            recoilSpeed = Mathf.Min(recoilSpeed, movementMotor.MaxSpeed);
        }

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
        Vector3 right = Vector3.Cross(Vector3.up, mainDirection).normalized;

        ContactPoint contact = collision.GetContact(0);
        Vector3 toContact = contact.point - transform.position;
        toContact.y = 0f;

        float sideSign = 0f;

        if (toContact.sqrMagnitude > 0.0001f)
        {
            sideSign = Mathf.Sign(Vector3.Dot(toContact.normalized, right));
        }

        Vector3 lateralDirection = right * sideSign;
        Vector3 finalDirection = mainDirection + lateralDirection * lateralPushFactor;
        finalDirection.y = 0f;

        finalDirection = finalDirection.sqrMagnitude < 0.0001f
            ? mainDirection
            : finalDirection.normalized;

        Vector3 impulse = finalDirection * pushImpulse;
        impulse.y += upwardImpulse;

        targetRigidbody.AddForceAtPosition(impulse, contact.point, ForceMode.Impulse);

        if (torqueImpulse <= 0f)
        {
            return;
        }

        Vector3 torqueAxis = Vector3.Cross(Vector3.up, finalDirection).normalized;
        Vector3 torque = torqueAxis * torqueImpulse * (sideSign == 0f ? 1f : sideSign);

        targetRigidbody.AddTorque(torque, ForceMode.Impulse);
    }

    #endregion

    #region Helpers

    private bool IsGroundLayer(int layer)
    {
        return IsInLayerMask(layer, groundLayers);
    }

    private static bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }

    private Vector3 GetPlanarMotionDirection()
    {
        Vector3 planarVelocity = rb != null ? rb.linearVelocity : Vector3.zero;
        planarVelocity.y = 0f;

        if (planarVelocity.magnitude >= minimumVelocityDirectionSpeed)
        {
            return planarVelocity.normalized;
        }

        if (movementMotor != null && movementMotor.LastValidMoveDirection.sqrMagnitude > 0.0001f)
        {
            Vector3 lastDirection = movementMotor.LastValidMoveDirection;
            lastDirection.y = 0f;

            if (lastDirection.sqrMagnitude > 0.0001f)
            {
                return lastDirection.normalized;
            }
        }

        Vector3 planarForward = transform.forward;
        planarForward.y = 0f;

        return planarForward.sqrMagnitude < 0.0001f
            ? Vector3.forward
            : planarForward.normalized;
    }

    #endregion
}