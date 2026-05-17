using UnityEngine;

/// <summary>
/// Trigger de checkpoint individual.
///
/// Estructura esperada del prefab:
/// <list type="bullet">
///   <item><b>Visual base</b>: visual principal del objeto, siempre visible.</item>
///   <item>
///     <b>Visual de vida</b> (<see cref="availableLifeVisual"/>): GameObject hijo opcional
///     que representa una vida disponible. Se gestiona automáticamente:
///     <list type="bullet">
///       <item>Oculto cuando las vidas están al máximo (no hay nada que dar).</item>
///       <item>Visible cuando las vidas están por debajo del máximo y este checkpoint no fue visitado.</item>
///       <item>Se oculta permanentemente al activarse este checkpoint (hasta un reinicio de nivel).</item>
///     </list>
///     El diseñador lo asigna manualmente en el Inspector; este componente no lo busca de forma automática.
///   </item>
/// </list>
///
/// Reglas de activación:
/// <list type="bullet">
///   <item>Solo se activa una vez por ciclo de nivel.</item>
///   <item>No permite que checkpoints anteriores sobrescriban checkpoints más avanzados.</item>
///   <item>Detección por componente (<see cref="BallMovementMotor"/>), nunca por tag.</item>
/// </list>
/// </summary>
public sealed class SpawnPointTrigger : MonoBehaviour
{
    #region Inspector

    [Header("Visuales")]
    [SerializeField]
    [Tooltip("Visual hijo que representa la vida disponible en este checkpoint.\n" +
             "Asignar manualmente desde el Inspector del prefab.\n" +
             "Se oculta cuando las vidas están al máximo o cuando el checkpoint ya fue visitado.")]
    private GameObject availableLifeVisual;

    #endregion

    #region Runtime

    private SpawnPointManager     ownerManager;
    private BallRespawnController respawnController;
    private LivesController       livesController;

    private Vector3    spawnPosition;
    private Quaternion spawnRotation;
    private float      checkpointDistance;
    private bool       hasBeenActivated;

    #endregion

    #region Properties

    /// <summary><c>true</c> si la bola ya ha pasado por este checkpoint en el nivel actual.</summary>
    public bool HasBeenActivated  => hasBeenActivated;

    /// <summary>Distancia sobre el track donde está ubicado este checkpoint.</summary>
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

    /// <summary>
    /// Inicializa el checkpoint con sus referencias y datos de posición.
    /// Llamado por <see cref="SpawnPointManager"/> al instanciar el prefab.
    /// </summary>
    public void Initialize(
        SpawnPointManager     manager,
        BallRespawnController respawnCtrl,
        LivesController       livesCtrl,
        Vector3               respawnPos,
        Quaternion            respawnRot,
        float                 distance)
    {
        ownerManager       = manager;
        respawnController  = respawnCtrl;
        livesController    = livesCtrl;
        spawnPosition      = respawnPos;
        spawnRotation      = respawnRot;
        checkpointDistance = distance;
        hasBeenActivated   = false;

        RefreshLifeVisual();
    }

    /// <summary>
    /// Resetea el checkpoint a no visitado y actualiza el visual de vida.
    /// Llamado por <see cref="SpawnPointManager.ResetAllSpawnPoints"/> en reinicio de nivel.
    /// </summary>
    public void ResetTrigger()
    {
        hasBeenActivated = false;
        RefreshLifeVisual();
    }

    #endregion

    #region Physics

    private void OnTriggerEnter(Collider other)
    {
        if (hasBeenActivated) return;
        if (!TryGetPlayerMotor(other, out _)) return;
        if (ownerManager != null && !ownerManager.CanActivateCheckpoint(checkpointDistance)) return;

        hasBeenActivated = true;

        ownerManager?.RegisterCheckpointActivation(checkpointDistance);
        respawnController?.SetRespawnPosition(spawnPosition, spawnRotation);

        if (livesController != null && livesController.CurrentLives < livesController.MaxLives)
            livesController.AddLife();

        // Ocultar el visual de vida: este checkpoint ya fue visitado, no puede dar más vidas.
        RefreshLifeVisual();
    }

    #endregion

    #region Visual

    private void HandleLivesChanged(int _) => RefreshLifeVisual();

    /// <summary>
    /// Actualiza la visibilidad del visual de vida según el estado actual.
    /// Mostrar solo si: no visitado Y vidas actuales inferiores al máximo.
    /// </summary>
    private void RefreshLifeVisual()
    {
        if (availableLifeVisual == null) return;

        bool shouldShow = !hasBeenActivated
            && livesController != null
            && livesController.CurrentLives < livesController.MaxLives;

        availableLifeVisual.SetActive(shouldShow);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Busca <see cref="BallMovementMotor"/> en el collider y su jerarquía de Rigidbody.
    /// Detección por componente, nunca por tag.
    /// </summary>
    private static bool TryGetPlayerMotor(Collider other, out BallMovementMotor motor)
    {
        motor = null;
        if (other == null) return false;

        motor = other.GetComponent<BallMovementMotor>()
             ?? other.GetComponentInParent<BallMovementMotor>();
        if (motor != null) return true;

        if (other.attachedRigidbody != null)
        {
            motor = other.attachedRigidbody.GetComponent<BallMovementMotor>()
                 ?? other.attachedRigidbody.GetComponentInParent<BallMovementMotor>();
        }

        return motor != null;
    }

    #endregion
}