using System;
using UnityEngine;

/// <summary>
/// Controla el ciclo de vida del jugador durante el gameplay.
///
/// Mantiene un único <see cref="PlayerState"/> serializable que garantiza
/// que las condiciones <c>Dead</c>, <c>GoalReached</c> y <c>Spawning</c> son mutuamente
/// excluyentes por construcción, sin depender de guards de doble-boolean.
///
/// Todas las transiciones de estado notifican su evento local y propagan el evento global
/// correspondiente en <see cref="GameEvents"/>.
///
/// Flujo canónico de respawn:
/// <c>Alive → Dead (Die) → Spawning (BeginSpawning, tras teleport) → Alive (ResetState, tras cámara)</c>
///
/// Flujo canónico de transición de nivel:
/// <c>Alive → GoalReached (ReachGoal) → Spawning (BeginSpawning, tras teleport) → Alive (ResetState, tras cámara)</c>
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
    /// Disparado al volver a <see cref="PlayerState.Alive"/> mediante <see cref="ResetState"/>.
    /// Subscriptores típicos: sistemas que deben rehabilitar lógica de gameplay tras un respawn.
    /// </summary>
    public event Action OnStateReset;

    #endregion

    #region Inspector

    [Header("Estado — Solo Lectura en Runtime")]
    [SerializeField]
    [Tooltip("Estado actual del ciclo de vida. Gestionado exclusivamente por código.\n" +
             "No modificar desde el Inspector durante Play Mode.")]
    private PlayerState currentState = PlayerState.Alive;

    #endregion

    #region Properties

    /// <summary>Estado de ciclo de vida actual.</summary>
    public PlayerState CurrentState => currentState;

    /// <summary>
    /// <c>true</c> únicamente cuando el jugador está en <see cref="PlayerState.Alive"/>
    /// y puede recibir input. Equivale a <see cref="CanControl"/>.
    ///
    /// Retorna <c>false</c> para <see cref="PlayerState.Dead"/>, <see cref="PlayerState.GoalReached"/>
    /// y <see cref="PlayerState.Spawning"/>. El motor bloquea toda lógica de input en estos estados.
    /// </summary>
    public bool IsAlive => currentState == PlayerState.Alive;

    /// <summary><c>true</c> si el jugador está en <see cref="PlayerState.Dead"/>.</summary>
    public bool IsDead => currentState == PlayerState.Dead;

    /// <summary><c>true</c> si el jugador está en <see cref="PlayerState.GoalReached"/>.</summary>
    public bool HasReachedGoal => currentState == PlayerState.GoalReached;

    /// <summary>
    /// <c>true</c> si la bola está posicionada pero la cámara aún no confirmó su alineación.
    /// El input está bloqueado en este estado.
    /// </summary>
    public bool IsSpawning => currentState == PlayerState.Spawning;

    /// <summary>
    /// <c>true</c> mientras el jugador puede recibir input y ejecutar acciones.
    /// Equivale a <see cref="IsAlive"/>. Mantenido por compatibilidad con consumidores existentes.
    /// </summary>
    public bool CanControl => currentState == PlayerState.Alive;

    #endregion

    #region Public API

    /// <summary>
    /// Transiciona a <see cref="PlayerState.Dead"/> y notifica todos los listeners.
    /// No tiene efecto si el estado actual no es <see cref="PlayerState.Alive"/>.
    ///
    /// No se puede morir durante <see cref="PlayerState.Spawning"/> intencionalmente:
    /// la ventana de invulnerabilidad mientras la cámara transiciona es aceptable
    /// y evita reiniciar la secuencia de respawn si la física mueve la bola levemente.
    /// </summary>
    public void Die()
    {
        if (currentState != PlayerState.Alive) return;

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
        if (currentState != PlayerState.Alive) return;

        currentState = PlayerState.GoalReached;
        OnGoalReached?.Invoke();
        GameEvents.RaiseGoalReached();
    }

    /// <summary>
    /// Transiciona a <see cref="PlayerState.Spawning"/>.
    ///
    /// Llamado por <see cref="BallRespawnController"/> en dos momentos:
    /// <list type="number">
    ///   <item>
    ///     Inmediatamente en <c>RespawnImmediate()</c>, antes de que la corutina ejecute,
    ///     para bloquear el input en el gap entre la llamada y el primer frame de la corutina.
    ///   </item>
    ///   <item>
    ///     En la corutina, justo después del teleport, para estados que llegan bloqueados
    ///     por otra razón (Dead, GoalReached) y necesitan transicionar a Spawning
    ///     antes de esperar la cámara.
    ///   </item>
    /// </list>
    ///
    /// No tiene guardas intencionales: siempre es válido entrar en Spawning.
    /// </summary>
    public void BeginSpawning()
    {
        currentState = PlayerState.Spawning;
    }

    /// <summary>
    /// Devuelve el estado a <see cref="PlayerState.Alive"/> y notifica a los listeners.
    /// Llamado por <see cref="BallRespawnController"/> cuando la cámara completa
    /// su transición hacia la bola (o cuando el timeout de cámara expira).
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
        // Previene que un diseñador modifique el estado desde el Inspector
        // durante Play Mode y genere transiciones fuera del flujo oficial.
        if (Application.isPlaying) return;
        currentState = PlayerState.Alive;
    }
#endif

    #endregion
}