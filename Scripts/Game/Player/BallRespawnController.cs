using System.Collections;
using UnityEngine;

/// <summary>
/// Gestiona respawn y resolución de fin de nivel del jugador.
///
/// Responsabilidades:
/// - Guardar la transformación de origen o un punto de respawn explícito.
/// - Reposicionar al jugador al caer al vacío o al iniciar un nuevo nivel.
/// - Forzar una detención controlada al llegar a la meta.
/// - Orquestar la secuencia Teleport → Spawning → (cámara alineada) → Alive.
///
/// Contrato de estado garantizado:
/// El jugador NUNCA pasa a <see cref="PlayerState.Alive"/> antes de que la cámara
/// haya completado su transición hacia la nueva posición. Esto elimina la ventana
/// de input en posición incorrecta, tanto en respawns mid-level como en
/// transiciones entre niveles.
///
/// Flujo para muerte:
///   Dead → [respawnDelay] → Teleport → Spawning → [cámara alineada] → Alive
///
/// Flujo para inicio de nivel / RespawnImmediate:
///   Spawning [inmediato] → Teleport → [cámara alineada] → Alive
/// </summary>
[RequireComponent(typeof(BallStateController))]
[RequireComponent(typeof(BallMovementMotor))]
public sealed class BallRespawnController : MonoBehaviour
{
    #region Inspector

    [Header("Referencias")]
    [SerializeField]
    [Tooltip("Estado del jugador.")]
    private BallStateController stateController;

    [SerializeField]
    [Tooltip("Motor de movimiento del jugador.")]
    private BallMovementMotor movementMotor;

    [SerializeField]
    [Tooltip("Cámara de seguimiento. Su transición de posición define cuándo se habilita el input.\n" +
             "Si no se asigna, el input se habilita un step de física después del teleport.")]
    private CameraFollowController cameraFollowController;

    [SerializeField]
    [Tooltip("Punto de respawn opcional. Si no se asigna, se usa el respawn guardado por código.")]
    private Transform respawnPoint;

    [Header("Respawn")]
    [SerializeField]
    [Tooltip("Tiempo de espera antes de reaparecer tras caer al vacío.")]
    private float respawnDelay = 0.2f;

    [SerializeField]
    [Tooltip("Tiempo máximo de espera por la transición de cámara antes de habilitar el control igualmente.\n" +
             "Previene que la bola quede permanentemente bloqueada si la cámara nunca llega.")]
    private float maxCameraWaitDuration = 1.5f;

    [Header("Meta")]
    [SerializeField]
    [Tooltip("Desaceleración aplicada cuando el jugador entra en la meta.")]
    private float goalStopDeceleration = 60f;

    #endregion

    #region Runtime

    private Vector3    initialPosition;
    private Quaternion initialRotation;
    private Coroutine  respawnRoutine;

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        stateController = GetComponent<BallStateController>();
        movementMotor   = GetComponent<BallMovementMotor>();
    }

    private void Awake()
    {
        if (stateController == null) stateController = GetComponent<BallStateController>();
        if (movementMotor   == null) movementMotor   = GetComponent<BallMovementMotor>();

        CaptureCurrentTransformAsRespawn();
    }

    private void OnEnable()
    {
        if (stateController == null) return;
        stateController.OnPlayerDied  += HandlePlayerDied;
        stateController.OnGoalReached += HandleGoalReached;
    }

    private void OnDisable()
    {
        if (stateController == null) return;
        stateController.OnPlayerDied  -= HandlePlayerDied;
        stateController.OnGoalReached -= HandleGoalReached;
    }

    #endregion

    #region Public API

    /// <summary>Actualiza el respawn general usando la transformación actual del jugador.</summary>
    public void CaptureCurrentTransformAsRespawn()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
    }

    /// <summary>
    /// Sobreescribe el punto de respawn con una posición y rotación explícitas.
    /// Usado por InfiniteLevelManager, checkpoints y LivesController.
    /// </summary>
    public void SetRespawnPosition(Vector3 position, Quaternion rotation)
    {
        initialPosition = position;
        initialRotation = rotation.normalized;
        respawnPoint    = null;
    }

    /// <summary>
    /// Inicia una secuencia de respawn inmediata sin delay de muerte.
    /// Usado por <c>InfiniteLevelManager.RepositionBallAtLevelStart()</c>.
    ///
    /// Llama a <c>stateController.BeginSpawning()</c> de forma síncrona ANTES de
    /// iniciar la corutina para cubrir el gap de un frame entre esta llamada y la
    /// primera ejecución de la corutina. Sin este bloqueo previo, si la bola estuviera
    /// en <see cref="PlayerState.Alive"/>, recibiría input en ese frame.
    /// </summary>
    public void RespawnImmediate()
    {
        if (respawnRoutine != null)
            StopCoroutine(respawnRoutine);

        // Bloqueo sincrónico: ningún estado previo (Alive, GoalReached, Dead)
        // produce input entre esta llamada y el primer frame de la corutina.
        stateController.BeginSpawning();

        respawnRoutine = StartCoroutine(RespawnSequenceRoutine(0f));
    }

    #endregion

    #region Event Handlers

    private void HandlePlayerDied()
    {
        if (respawnRoutine != null)
            StopCoroutine(respawnRoutine);

        respawnRoutine = StartCoroutine(RespawnSequenceRoutine(respawnDelay));
    }

    private void HandleGoalReached()
    {
        movementMotor.BeginForcedStop(goalStopDeceleration);
    }

    #endregion

    #region Respawn Flow

    /// <summary>
    /// Secuencia canónica de respawn. Orden garantizado:
    ///
    /// <list type="number">
    ///   <item>Delay pre-teleport (solo si <paramref name="delay"/> &gt; 0, ruta de muerte).</item>
    ///   <item>Teleport: la bola está en Dead o Spawning, nunca en Alive.</item>
    ///   <item>
    ///     <see cref="BallStateController.BeginSpawning"/>: la bola está posicionada
    ///     pero el control permanece bloqueado. Para la ruta de muerte transiciona de
    ///     Dead a Spawning; para la ruta RespawnImmediate es idempotente (ya en Spawning).
    ///   </item>
    ///   <item>
    ///     Espera de cámara: si <see cref="cameraFollowController"/> está asignado,
    ///     aguarda a que <c>IsRespawnTransitionActive</c> sea <c>false</c> o a que
    ///     expire <see cref="maxCameraWaitDuration"/>. Sin cámara: <c>WaitForFixedUpdate</c>.
    ///   </item>
    ///   <item>
    ///     <see cref="BallStateController.ResetState"/>: bola <see cref="PlayerState.Alive"/>,
    ///     cámara alineada, input habilitado.
    ///   </item>
    ///   <item><see cref="GameEvents.RaisePlayerRespawned"/>: notifica sistemas externos.</item>
    /// </list>
    /// </summary>
    private IEnumerator RespawnSequenceRoutine(float delay)
    {
        // — 1. Delay pre-teleport (solo muerte) —
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        // — 2. Teleport —
        ResolveRespawnTransform(out Vector3 targetPosition, out Quaternion targetRotation);
        movementMotor.TeleportTo(targetPosition, targetRotation);

        // — 3. Estado Spawning: posicionada, cámara en tránsito, sin input —
        // Para la ruta de muerte: Dead → Spawning (la bola ya está en posición correcta).
        // Para RespawnImmediate: ya en Spawning, llamada idempotente.
        stateController.BeginSpawning();

        // — 4. Esperar a que la cámara se alinee —
        if (cameraFollowController != null)
        {
            // El input solo se habilita cuando el jugador puede ver la bola
            // desde la perspectiva correcta. Si la cámara aparece "de la nada"
            // ya alineada, IsRespawnTransitionActive será false de inmediato.
            cameraFollowController.BeginRespawnTransition();

            float waitTimer = 0f;
            while (cameraFollowController.IsRespawnTransitionActive
                   && waitTimer < maxCameraWaitDuration)
            {
                waitTimer += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            // Sin cámara: mínimo de un step de física para que el Rigidbody
            // registre la nueva posición antes del primer FixedUpdate con input.
            yield return new WaitForFixedUpdate();
        }

        // — 5. Cámara alineada → bola viva → input habilitado —
        stateController.ResetState();
        GameEvents.RaisePlayerRespawned();
        respawnRoutine = null;
    }

    /// <summary>Resuelve la transformación final de respawn.</summary>
    private void ResolveRespawnTransform(out Vector3 position, out Quaternion rotation)
    {
        if (respawnPoint != null)
        {
            position = respawnPoint.position;
            rotation = respawnPoint.rotation;
            return;
        }

        position = initialPosition;
        rotation = initialRotation;
    }

    #endregion
}