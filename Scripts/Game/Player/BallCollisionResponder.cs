using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maneja la respuesta de la pelota ante colisiones con obstáculos y barreras del escenario.
///
/// Responsabilidades:
/// - Calcular y aplicar rebote de barrera 100% por código (sin PhysicMaterials).
/// - Aplicar retroceso y supresión de drive en colisiones con obstáculos inamovibles.
/// - Aplicar fuerzas a obstáculos empujables.
/// - Mantener el estado de bloqueo frontal para que <see cref="BallMovementMotor"/>
///   proyecte steering e impulsos sobre el plano de la pared.
///
/// Diseño de barreras (física 100% por código):
/// Todos los colliders de barrera deben tener un PhysicMaterial con
/// <c>bounciness = 0</c> y <c>friction = 0</c> (o sin PhysicMaterial asignado y
/// el Default Physics Material del proyecto configurado en cero).
/// Unity resuelve la depenetración pero no añade rebote. Este componente captura
/// la velocidad pre-física en FixedUpdate y calcula el rebote en OnCollisionEnter,
/// eliminando el conflicto entre el solver de Unity y el código del motor que
/// causaba comportamiento inconsistente al deslizar contra paredes.
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
    [Tooltip("Capas de suelo. No aplican retroceso ni bloqueo frontal.")]
    private LayerMask groundLayers;

    [SerializeField]
    [Tooltip("Capas de barreras laterales del track.\n" +
             "El rebote se calcula 100% por código. Los colliders de esta capa deben\n" +
             "tener PhysicMaterial con bounciness=0 y friction=0.")]
    private LayerMask barrierLayers;

    [SerializeField]
    [Tooltip("Capas de obstáculos inamovibles (cajas fijas, muros).")]
    private LayerMask immovableLayers;

    [SerializeField]
    [Tooltip("Capas de obstáculos empujables (cajas dinámicas, pelotas).")]
    private LayerMask pushableLayers;

    [Header("Barreras — Rebote por Código")]
    [SerializeField]
    [Tooltip("Coeficiente de rebote al chocar contra barreras laterales.\n" +
             "0 = completamente absorbente (sin rebote).\n" +
             "0.45 = rebote moderado con sensación de impacto.\n" +
             "1 = rebote perfectamente elástico.\n" +
             "Requiere que el PhysicMaterial de las barreras tenga bounciness=0.")]
    [Range(0f, 1f)]
    private float wallBounceCoefficient = 0.45f;

    [SerializeField]
    [Tooltip("Velocidad perpendicular mínima en m/s para aplicar rebote.\n" +
             "Con 0.4 la bola se pegaba a las paredes si llegaba a baja velocidad:\n" +
             "Unity quitaba la velocidad perpendicular y el código no la reponía.\n" +
             "0.05 es prácticamente 0 — solo filtra contactos de rozamiento mínimo.")]
    private float minimumBounceImpactSpeed = 0.05f;

    [Header("Fallback — Colisiones No Clasificadas")]
    [SerializeField]
    [Tooltip("Si está activo, las colisiones cuya capa no está en ninguna máscara\n" +
             "se tratan como obstáculos con los valores por defecto.\n" +
             "Desactivado por defecto para no interferir con elementos del escenario.")]
    private bool handleUnclassifiedCollisionsAsObstacles = false;

    [SerializeField]
    [Tooltip("Multiplicador de velocidad para colisiones sin perfil de obstáculo asignado.")]
    [Range(0f, 1f)]
    private float defaultSpeedMultiplier = 0.9f;

    [SerializeField]
    [Tooltip("Velocidad mínima de retroceso para colisiones sin perfil de obstáculo asignado.")]
    private float defaultRecoilSpeed = 1.25f;

    [SerializeField]
    [Tooltip("Duración de supresión de drive para colisiones sin perfil de obstáculo asignado.")]
    private float defaultDriveSuppression = 0.08f;

    [Header("Detección de Bloqueo Frontal")]
    [SerializeField]
    [Tooltip("Dot product mínimo entre la dirección de movimiento y la normal inversa del contacto\n" +
             "para considerarlo un bloqueo frontal. Evita falsos positivos en contactos laterales.\n" +
             "Rango recomendado: 0.25–0.45.")]
    [Range(0f, 1f)]
    private float forwardBlockThreshold = 0.35f;

    [SerializeField]
    [Tooltip("Velocidad planar mínima en m/s para usar la velocidad real como dirección de movimiento.\n" +
             "Por debajo de este valor se usa el forward guardado del motor como fallback.")]
    private float minimumVelocityDirectionSpeed = 0.15f;

    #endregion

    #region Runtime

    private readonly HashSet<Collider> activeObstacleContacts = new HashSet<Collider>();

    private Vector3 blockingNormal;
    private bool    hasBlockingContact;

    private Vector3 anyContactNormal;
    private bool    hasAnyContact;

    /// <summary>
    /// Velocidad del Rigidbody capturada al final del FixedUpdate de este componente
    /// (orden -10), después de que BallMovementMotor (orden -15) aplicó sus cambios
    /// y antes de que Unity ejecute su paso de simulación física.
    ///
    /// Se usa en <see cref="OnCollisionEnter"/> para calcular la velocidad perpendicular
    /// real al impacto, ya que con bounciness=0 Unity ya modificó rb.linearVelocity
    /// cuando el callback se invoca.
    /// </summary>
    private Vector3 prePhysicsVelocity;

    #endregion

    #region Properties

    /// <summary>
    /// <c>true</c> si hay un contacto frontal activo que bloquea la dirección de movimiento.
    /// Calculado cada FixedUpdate a partir de <see cref="OnCollisionStay"/>.
    /// Consumido por <see cref="BallMovementMotor"/> para proyectar steering e impulsos
    /// sobre el plano de la barrera, convirtiendo la presión en deslizamiento.
    /// </summary>
    public bool    HasBlockingContact => hasBlockingContact;

    /// <summary>Normal promediada del contacto frontal bloqueante actual.</summary>
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

        // Asignar por código un PhysicsMaterial de fricción cero y sin rebote al Collider
        // de la pelota. Sin esto, Unity aplica el material por defecto (dynamicFriction = 0.6)
        // que roba velocidad durante el deslizamiento lateral contra las paredes.
        // Al eliminar todos los PhysicsMaterials del proyecto, el default de Unity aún actúa.
        // Esta asignación en Awake garantiza que la física de contacto de la bola
        // sea 100% controlada por código, sin interferencia del solver de Unity.
        Collider ownCollider = GetComponent<Collider>();
        if (ownCollider != null)
        {
            ownCollider.material = new PhysicsMaterial("BallZeroFriction")
            {
                dynamicFriction = 0f,
                staticFriction  = 0f,
                bounciness      = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine   = PhysicsMaterialCombine.Minimum,
            };
        }
    }

    private void FixedUpdate()
    {
        // Limpiar estado de bloqueo cada frame; OnCollisionStay lo reconstruye si hay contacto.
        RebuildBlockingState();

        // Capturar velocidad post-motor, pre-física.
        // BallMovementMotor (orden -15) ya corrió antes que este componente (orden -10),
        // por lo que rb.linearVelocity aquí refleja la velocidad final que tendrá la bola
        // justo antes del paso de simulación de Unity donde ocurre la detección de colisión.
        prePhysicsVelocity = rb.linearVelocity;
    }

    private void OnCollisionEnter(Collision collision)
    {
        Collider otherCollider = collision.collider;
        if (otherCollider == null) return;

        int otherLayer = otherCollider.gameObject.layer;

        // El suelo nunca genera retroceso ni bloqueo.
        if (IsGroundLayer(otherLayer)) return;

        // Barrera: rebote 100% por código usando la velocidad pre-física capturada.
        if (IsInLayerMask(otherLayer, barrierLayers))
        {
            ApplyCodedWallBounce(collision);
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

        if (handleUnclassifiedCollisionsAsObstacles)
        {
            HandleDefaultCollision(collision);
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        Collider otherCollider = collision.collider;
        if (otherCollider == null) return;

        // El suelo no genera bloqueo frontal.
        if (IsGroundLayer(otherCollider.gameObject.layer)) return;

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

    #region Collision Handling — Barreras

    /// <summary>
    /// Calcula y aplica el rebote de barrera 100% por código.
    ///
    /// Mecánica:
    /// Con <c>bounciness=0</c> en el PhysicMaterial de la barrera, Unity resuelve la
    /// depenetración y elimina la componente de velocidad perpendicular a la pared,
    /// dejando solo el deslizamiento lateral. Este método usa la velocidad pre-física
    /// (<see cref="prePhysicsVelocity"/>) para recuperar cuánta velocidad tenía la bola
    /// en la dirección de impacto y añade de vuelta el rebote configurado.
    ///
    /// Resultado: deslizamiento lateral conservado + rebote controlado por código.
    /// </summary>
    private void ApplyCodedWallBounce(Collision collision)
    {
        Vector3 contactNormal = AverageContactNormal(collision);
        contactNormal.y = 0f;
        if (contactNormal.sqrMagnitude < 0.0001f) return;
        contactNormal.Normalize();

        // Velocidad planar justo antes del paso de física de Unity.
        Vector3 planarPreVelocity = new Vector3(prePhysicsVelocity.x, 0f, prePhysicsVelocity.z);

        // Componente de velocidad que apuntaba INTO la pared (positivo = impacto real).
        // contactNormal apunta DESDE la pared HACIA la bola (outward normal de Unity).
        // La proyección negativa da la magnitud "hacia adentro".
        float perpendicularSpeed = -Vector3.Dot(planarPreVelocity, contactNormal);

        if (perpendicularSpeed < minimumBounceImpactSpeed) return;

        // Añadir impulso de rebote en dirección de la normal (alejándose de la pared).
        // Unity con bounciness=0 ya removió ese componente de rb.linearVelocity;
        // solo sumamos el rebote configurado sin duplicar nada.
        float bounceSpeed = perpendicularSpeed * wallBounceCoefficient;

        rb.linearVelocity = new Vector3(
            rb.linearVelocity.x + contactNormal.x * bounceSpeed,
            rb.linearVelocity.y,
            rb.linearVelocity.z + contactNormal.z * bounceSpeed);
    }

    #endregion

    #region Collision Handling — Obstáculos

    private void HandleImmovableCollision(Collision collision, Collider otherCollider)
    {
        ImmovableObstacle obstacle = otherCollider.GetComponent<ImmovableObstacle>();

        float speedMultiplier = obstacle != null ? obstacle.SpeedMultiplier : defaultSpeedMultiplier;
        float recoilSpeed     = obstacle != null ? obstacle.RecoilSpeed     : defaultRecoilSpeed;

        movementMotor.MultiplySpeed(speedMultiplier);

        Vector3 recoilDirection     = CalculateRecoilDirection(collision);
        float   resolvedRecoilSpeed = CalculateResolvedRecoilSpeed(recoilSpeed);

        movementMotor.ApplyImpactRecoil(recoilDirection * resolvedRecoilSpeed);
    }

    private void HandlePushableCollision(Collision collision, Collider otherCollider)
    {
        PushableObstacle obstacle = otherCollider.GetComponent<PushableObstacle>();

        float pushImpulse       = obstacle != null ? obstacle.PushImpulse       : 2f;
        float lateralPushFactor = obstacle != null ? obstacle.LateralPushFactor : 0.35f;
        float upwardImpulse     = obstacle != null ? obstacle.UpwardImpulse     : 0.15f;
        float torqueImpulse     = obstacle != null ? obstacle.TorqueImpulse     : 1.25f;

        Rigidbody otherRigidbody = collision.rigidbody;
        if (otherRigidbody == null || otherRigidbody.isKinematic) return;

        ApplyPushableForces(
            collision,
            otherRigidbody,
            pushImpulse,
            lateralPushFactor,
            upwardImpulse,
            torqueImpulse);

        // La pelota no pierde velocidad, no cambia de dirección ni recibe ningún efecto
        // al chocar con un objeto empujable: solo el objeto sale expulsado.
        // Restaurar prePhysicsVelocity anula cualquier modificación que el solver de Unity
        // haya aplicado al Rigidbody de la pelota durante la resolución de esta colisión.
        rb.linearVelocity = prePhysicsVelocity;
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
    /// Acumula normals de contacto del frame actual y determina si el contacto es bloqueante.
    /// Un contacto es frontal bloqueante si la bola se mueve hacia él con un dot product
    /// mayor que <see cref="forwardBlockThreshold"/>.
    /// </summary>
    private void RegisterBlockingFromCollision(Collision collision)
    {
        Vector3 contactNormal = AverageContactNormal(collision);
        contactNormal.y = 0f;
        if (contactNormal.sqrMagnitude < 0.0001f) return;
        contactNormal.Normalize();

        // Registrar cualquier contacto no-suelo activo.
        anyContactNormal = (anyContactNormal + contactNormal).normalized;
        hasAnyContact    = true;

        // Resolver dirección de movimiento: velocidad real o forward del motor como fallback.
        Vector3 planarVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 moveDir;

        if (planarVelocity.magnitude > minimumVelocityDirectionSpeed)
        {
            moveDir = planarVelocity.normalized;
        }
        else
        {
            moveDir = movementMotor != null
                ? movementMotor.LastValidMoveDirection
                : Vector3.forward;
            moveDir.y = 0f;
            if (moveDir.sqrMagnitude < 0.0001f) return;
            moveDir.Normalize();
        }

        // Un contacto es bloqueante si la bola se mueve hacia la pared que lo generó.
        // contactNormal apunta hacia la bola → (-contactNormal) apunta hacia la pared.
        // dot > threshold → la bola se mueve suficientemente "hacia" la pared.
        float dot = Vector3.Dot(moveDir, -contactNormal);
        if (dot > forwardBlockThreshold)
        {
            blockingNormal     = (blockingNormal + contactNormal).normalized;
            hasBlockingContact = true;
        }
    }

    #endregion

    #region Helpers

    private Vector3 CalculateRecoilDirection(Collision collision)
    {
        Vector3 normal = AverageContactNormal(collision);
        normal.y = 0f;

        if (normal.sqrMagnitude > 0.0001f)
        {
            return normal.normalized;
        }

        // Fallback: usar el forward inverso del motor si la normal es inválida.
        return movementMotor != null ? -movementMotor.LastValidMoveDirection : Vector3.back;
    }

    private float CalculateResolvedRecoilSpeed(float baseRecoilSpeed)
    {
        // El retroceso es al menos baseRecoilSpeed, pero escala con la velocidad actual
        // para que impactos a mayor velocidad produzcan un retroceso proporcional.
        float currentSpeed = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude;
        return Mathf.Max(baseRecoilSpeed, currentSpeed * 0.4f);
    }

    private void ApplyPushableForces(
        Collision collision,
        Rigidbody otherRigidbody,
        float pushImpulse,
        float lateralPushFactor,
        float upwardImpulse,
        float torqueImpulse)
    {
        Vector3 contactNormal = AverageContactNormal(collision);
        Vector3 pushDir       = -contactNormal;
        pushDir.y = 0f;

        if (pushDir.sqrMagnitude < 0.0001f)
        {
            pushDir = movementMotor != null
                ? movementMotor.LastValidMoveDirection
                : Vector3.forward;
        }

        pushDir.Normalize();

        Vector3 lateralDir       = Vector3.Cross(pushDir, Vector3.up).normalized;
        Vector3 moveDir          = movementMotor != null ? movementMotor.LastValidMoveDirection : pushDir;
        float   lateralComponent = Vector3.Dot(moveDir, lateralDir);

        Vector3 force =
            pushDir    * pushImpulse +
            lateralDir * (lateralComponent * lateralPushFactor * pushImpulse) +
            Vector3.up * (upwardImpulse * pushImpulse);

        otherRigidbody.AddForce(force, ForceMode.Impulse);

        Vector3 torqueDir = Vector3.Cross(Vector3.up, pushDir);
        otherRigidbody.AddTorque(torqueDir * torqueImpulse, ForceMode.Impulse);
    }

    private static Vector3 AverageContactNormal(Collision collision)
    {
        Vector3 sum = Vector3.zero;

        for (int i = 0; i < collision.contactCount; i++)
        {
            sum += collision.GetContact(i).normal;
        }

        return sum.sqrMagnitude > 0.0001f ? sum.normalized : Vector3.up;
    }

    private bool IsGroundLayer(int layer)
    {
        return IsInLayerMask(layer, groundLayers);
    }

    private static bool IsInLayerMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    #endregion
}