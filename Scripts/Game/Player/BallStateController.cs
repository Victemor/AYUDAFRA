using System;
using UnityEngine;

/// <summary>
/// Controla el ciclo de vida del jugador durante el gameplay.
///
/// Mantiene un único <see cref="PlayerState"/> serializable que garantiza
/// que las condiciones <c>Dead</c> y <c>GoalReached</c> son mutuamente excluyentes
/// por construcción, sin depender de guards de doble-boolean.
///
/// Todas las transiciones de estado:
/// <list type="bullet">
///   <item>Notifican su evento local (<see cref="OnPlayerDied"/>, <see cref="OnGoalReached"/>, <see cref="OnStateReset"/>).</item>
///   <item>Propagan el evento global correspondiente en <see cref="GameEvents"/>.</item>
///   <item>Solo se ejecutan desde <see cref="PlayerState.Alive"/>; cualquier otra llamada es silenciosa.</item>
/// </list>
///
/// Compatibilidad: las propiedades <see cref="IsDead"/>, <see cref="HasReachedGoal"/> y
/// <see cref="CanControl"/> se mantienen como computed properties para que los consumidores
/// existentes no requieran cambios.
/// </summary>
public sealed class BallStateController : MonoBehaviour
{
    #region Events

    /// <summary>
    /// Disparado al transicionar a <see cref="PlayerState.Dead"/>.
    /// Solo se emite una vez por ciclo de vida activo.
    /// Subscriptores típicos: <c>BallRespawnController</c>.
    /// </summary>
    public event Action OnPlayerDied;

    /// <summary>
    /// Disparado al transicionar a <see cref="PlayerState.GoalReached"/>.
    /// Solo se emite una vez por ciclo de vida activo.
    /// Subscriptores típicos: <c>BallRespawnController</c>.
    /// </summary>
    public event Action OnGoalReached;

    /// <summary>
    /// Disparado al volver a <see cref="PlayerState.Alive"/> por un reset.
    /// Subscriptores típicos: sistemas que deben rehabilitar input o lógica de gameplay tras un respawn.
    /// </summary>
    public event Action OnStateReset;

    #endregion

    #region Inspector

    [Header("Estado — Solo Lectura en Runtime")]
    [SerializeField]
    [Tooltip("Estado actual del ciclo de vida. Gestionado exclusivamente por código. " +
             "No modificar desde el Inspector durante Play Mode.")]
    private PlayerState currentState = PlayerState.Alive;

    #endregion

    #region Properties

    /// <summary>Estado de ciclo de vida actual.</summary>
    public PlayerState CurrentState => currentState;

    /// <summary>
    /// <c>true</c> si el jugador está en <see cref="PlayerState.Alive"/>.
    /// Equivale a <see cref="CanControl"/>.
    /// </summary>
    public bool IsAlive => currentState == PlayerState.Alive;

    /// <summary>
    /// <c>true</c> si el jugador está en <see cref="PlayerState.Dead"/>.
    /// Propiedad de compatibilidad con consumidores previos al refactor.
    /// </summary>
    public bool IsDead => currentState == PlayerState.Dead;

    /// <summary>
    /// <c>true</c> si el jugador está en <see cref="PlayerState.GoalReached"/>.
    /// Propiedad de compatibilidad con consumidores previos al refactor.
    /// </summary>
    public bool HasReachedGoal => currentState == PlayerState.GoalReached;

    /// <summary>
    /// <c>true</c> mientras el jugador puede recibir input y ejecutar acciones.
    /// Equivale a <see cref="IsAlive"/>.
    /// </summary>
    public bool CanControl => currentState == PlayerState.Alive;

    #endregion

    #region Public API

    /// <summary>
    /// Transiciona a <see cref="PlayerState.Dead"/> y notifica todos los listeners.
    /// No tiene efecto si el estado actual no es <see cref="PlayerState.Alive"/>.
    /// </summary>
    public void Die()
    {
        if (currentState != PlayerState.Alive)
        {
            return;
        }

        currentState = PlayerState.Dead;
        OnPlayerDied?.Invoke();
        GameEvents.RaisePlayerDied();
    }

    /// <summary>
    /// Transiciona a <see cref="PlayerState.GoalReached"/> y notifica todos los listeners.
    /// No tiene efecto si el estado actual no es <see cref="PlayerState.Alive"/>.
    /// </summary>
    public void ReachGoal()
    {
        if (currentState != PlayerState.Alive)
        {
            return;
        }

        currentState = PlayerState.GoalReached;
        OnGoalReached?.Invoke();
        GameEvents.RaiseGoalReached();
    }

    /// <summary>
    /// Devuelve el estado a <see cref="PlayerState.Alive"/> y notifica a los listeners.
    /// Llamado por <c>BallRespawnController</c> al completar la secuencia de respawn.
    /// No tiene guardas intencionales: siempre es válido resetear.
    /// </summary>
    public void ResetState()
    {
        currentState = PlayerState.Alive;
        OnStateReset?.Invoke();
    }

    #endregion

    #region Validation

#if UNITY_EDITOR
    private void OnValidate()
    {
        /// Previene que un diseñador modifique el estado desde el Inspector
        /// durante Play Mode y genere transiciones fuera del flujo oficial.
        if (Application.isPlaying)
        {
            return;
        }

        currentState = PlayerState.Alive;
    }
#endif

    #endregion
}