using UnityEngine;

/// <summary>
/// Trigger de checkpoint individual.
///
/// Reglas:
/// - No depende del tag Player.
/// - Detecta al jugador por BallMovementMotor en el collider o en el Rigidbody padre.
/// - Solo se activa una vez por ciclo de nivel.
/// - No permite que checkpoints anteriores sobrescriban checkpoints más avanzados.
/// - Si el jugador tiene menos vidas que el máximo, otorga 1 vida.
/// - El visual opcional solo se muestra mientras el checkpoint no ha sido activado y el jugador tiene menos vidas que el máximo.
/// </summary>
public sealed class SpawnPointTrigger : MonoBehaviour
{
    #region Inspector

    [Header("Visual Opcional")]
    [SerializeField]
    [Tooltip("Visual hijo opcional que representa la vida disponible. Se muestra solo si el jugador tiene menos vidas que el máximo y este checkpoint no ha sido activado.")]
    private GameObject availableLifeVisual;

    #endregion

    #region Runtime

    private SpawnPointManager ownerManager;
    private BallRespawnController respawnController;
    private LivesController livesController;

    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private float checkpointDistance;
    private bool hasBeenActivated;

    #endregion

    #region Properties

    public bool HasBeenActivated => hasBeenActivated;
    public float CheckpointDistance => checkpointDistance;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        GameEvents.OnLivesChanged += HandleLivesChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnLivesChanged -= HandleLivesChanged;
    }

    #endregion

    #region Public API

    public void Initialize(
        SpawnPointManager manager,
        BallRespawnController respawnCtrl,
        LivesController livesCtrl,
        Vector3 respawnPos,
        Quaternion respawnRot,
        float distance)
    {
        ownerManager = manager;
        respawnController = respawnCtrl;
        livesController = livesCtrl;
        spawnPosition = respawnPos;
        spawnRotation = respawnRot;
        checkpointDistance = distance;
        hasBeenActivated = false;

        RefreshAvailableLifeVisual();
    }

    public void ResetTrigger()
    {
        hasBeenActivated = false;
        RefreshAvailableLifeVisual();
    }

    #endregion

    #region Physics

    private void OnTriggerEnter(Collider other)
    {
        if (hasBeenActivated)
        {
            return;
        }

        if (!TryGetPlayerMotor(other, out _))
        {
            return;
        }

        if (ownerManager != null && !ownerManager.CanActivateCheckpoint(checkpointDistance))
        {
            return;
        }

        hasBeenActivated = true;

        if (ownerManager != null)
        {
            ownerManager.RegisterCheckpointActivation(checkpointDistance);
        }

        if (respawnController != null)
        {
            respawnController.SetRespawnPosition(spawnPosition, spawnRotation);
        }

        if (livesController != null && livesController.CurrentLives < livesController.MaxLives)
        {
            livesController.AddLife();
        }

        RefreshAvailableLifeVisual();
    }

    #endregion

    #region Visual

    private void HandleLivesChanged(int currentLives)
    {
        RefreshAvailableLifeVisual();
    }

    private void RefreshAvailableLifeVisual()
    {
        if (availableLifeVisual == null)
        {
            return;
        }

        bool shouldShow =
            !hasBeenActivated
            && livesController != null
            && livesController.CurrentLives < livesController.MaxLives;

        availableLifeVisual.SetActive(shouldShow);
    }

    #endregion

    #region Helpers

    private static bool TryGetPlayerMotor(Collider other, out BallMovementMotor motor)
    {
        motor = null;

        if (other == null)
        {
            return false;
        }

        motor = other.GetComponent<BallMovementMotor>();

        if (motor != null)
        {
            return true;
        }

        motor = other.GetComponentInParent<BallMovementMotor>();

        if (motor != null)
        {
            return true;
        }

        if (other.attachedRigidbody != null)
        {
            motor = other.attachedRigidbody.GetComponent<BallMovementMotor>();

            if (motor != null)
            {
                return true;
            }

            motor = other.attachedRigidbody.GetComponentInParent<BallMovementMotor>();

            if (motor != null)
            {
                return true;
            }
        }

        return false;
    }

    #endregion
}