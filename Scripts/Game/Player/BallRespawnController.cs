using System.Collections;
using UnityEngine;

/// <summary>
/// Gestiona respawn y resolución de fin de nivel del jugador.
/// 
/// Responsabilidades:
/// - Guardar la transformación de origen o un punto de respawn explícito.
/// - Reposicionar al jugador al caer al vacío.
/// - Forzar una detención controlada al llegar a la meta.
/// - Reiniciar el estado cuando corresponde.
/// - Bloquear el control hasta que la cámara termine de acomodarse en un respawn.
/// </summary>
[RequireComponent(typeof(BallStateController))]
[RequireComponent(typeof(BallMovementMotor))]
public sealed class BallRespawnController : MonoBehaviour
{
    #region Inspector

    [Header("Referencias")]

    [Tooltip("Estado del jugador.")]
    [SerializeField] private BallStateController stateController;

    [Tooltip("Motor de movimiento del jugador.")]
    [SerializeField] private BallMovementMotor movementMotor;

    [Tooltip("Cámara de seguimiento que debe transicionar suavemente al respawn.")]
    [SerializeField] private CameraFollowController cameraFollowController;

    [Tooltip("Punto de respawn opcional. Si no se asigna, se usa el respawn guardado por código.")]
    [SerializeField] private Transform respawnPoint;

    [Header("Respawn")]

    [Tooltip("Tiempo de espera antes de reaparecer tras caer al vacío.")]
    [SerializeField] private float respawnDelay = 0.2f;

    [Tooltip("Tiempo máximo de espera por la transición de cámara antes de devolver el control igualmente.")]
    [SerializeField] private float maxCameraWaitDuration = 1.5f;

    [Header("Meta")]

    [Tooltip("Desaceleración aplicada cuando el jugador entra en la meta.")]
    [SerializeField] private float goalStopDeceleration = 60f;

    #endregion

    #region Runtime

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Coroutine respawnRoutine;

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        stateController = GetComponent<BallStateController>();
        movementMotor = GetComponent<BallMovementMotor>();
    }

    private void Awake()
    {
        if (stateController == null)
        {
            stateController = GetComponent<BallStateController>();
        }

        if (movementMotor == null)
        {
            movementMotor = GetComponent<BallMovementMotor>();
        }

        CaptureCurrentTransformAsRespawn();
    }

    private void OnEnable()
    {
        if (stateController == null)
        {
            return;
        }

        stateController.OnPlayerDied += HandlePlayerDied;
        stateController.OnGoalReached += HandleGoalReached;
    }

    private void OnDisable()
    {
        if (stateController == null)
        {
            return;
        }

        stateController.OnPlayerDied -= HandlePlayerDied;
        stateController.OnGoalReached -= HandleGoalReached;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Actualiza el respawn general usando la transformación actual del jugador.
    /// </summary>
    public void CaptureCurrentTransformAsRespawn()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
    }

    /// <summary>
    /// Sobreescribe el punto de respawn con una posición y rotación explícitas.
    /// Usado por el manager del nivel, checkpoints y sistema de vidas.
    /// </summary>
    public void SetRespawnPosition(Vector3 position, Quaternion rotation)
    {
        initialPosition = position;
        initialRotation = rotation.normalized;
        respawnPoint = null;
    }

    /// <summary>
    /// Reposiciona inmediatamente al jugador en el punto de respawn y reinicia su estado.
    /// </summary>
    public void RespawnImmediate()
    {
        if (respawnRoutine != null)
        {
            StopCoroutine(respawnRoutine);
        }

        respawnRoutine = StartCoroutine(RespawnSequenceRoutine(0f));
        stateController.ResetState();
        GameEvents.RaisePlayerRespawned();
    }

    #endregion

    #region Event Handlers

    private void HandlePlayerDied()
    {
        if (respawnRoutine != null)
        {
            StopCoroutine(respawnRoutine);
        }

        respawnRoutine = StartCoroutine(RespawnSequenceRoutine(respawnDelay));
    }

    private void HandleGoalReached()
    {
        movementMotor.BeginForcedStop(goalStopDeceleration);
    }

    #endregion

    #region Respawn Flow

    private IEnumerator RespawnSequenceRoutine(float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        ResolveRespawnTransform(out Vector3 targetPosition, out Quaternion targetRotation);

        movementMotor.TeleportTo(targetPosition, targetRotation);

        if (cameraFollowController != null)
        {
            cameraFollowController.BeginRespawnTransition();

            float waitTimer = 0f;

            while (cameraFollowController.IsRespawnTransitionActive && waitTimer < maxCameraWaitDuration)
            {
                waitTimer += Time.deltaTime;
                yield return null;
            }
        }

        stateController.ResetState();
        respawnRoutine = null;
    }

    /// <summary>
    /// Resuelve la transformación final de respawn.
    /// </summary>
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