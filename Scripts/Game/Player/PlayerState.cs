/// <summary>
/// Estado de ciclo de vida del jugador durante el gameplay.
///
/// Reemplaza los booleans paralelos <c>IsDead</c> y <c>HasReachedGoal</c> que existían
/// antes de este refactor. Un enum garantiza que solo puede existir un estado activo
/// a la vez, eliminando cualquier combinación lógicamente inválida.
///
/// Transiciones válidas:
/// <list type="bullet">
///   <item><see cref="Alive"/>    → <see cref="Dead"/>        mediante <c>BallStateController.Die()</c></item>
///   <item><see cref="Alive"/>    → <see cref="GoalReached"/> mediante <c>BallStateController.ReachGoal()</c></item>
///   <item>Cualquier estado      → <see cref="Spawning"/>    mediante <c>BallStateController.BeginSpawning()</c></item>
///   <item>Cualquier estado      → <see cref="Alive"/>        mediante <c>BallStateController.ResetState()</c></item>
/// </list>
///
/// El ciclo normal de respawn es:
/// <c>Alive → Dead → Spawning → Alive</c>
/// El ciclo normal de transición de nivel es:
/// <c>Alive → GoalReached → Spawning → Alive</c>
///
/// El estado <see cref="Spawning"/> garantiza que la bola nunca recibe input
/// mientras la cámara está en tránsito hacia su posición correcta.
/// </summary>
public enum PlayerState
{
    /// <summary>El jugador está vivo y puede recibir input.</summary>
    Alive = 0,

    /// <summary>El jugador ha muerto y espera la secuencia de respawn.</summary>
    Dead = 1,

    /// <summary>El jugador ha alcanzado la meta y el nivel está cerrando.</summary>
    GoalReached = 2,

    /// <summary>
    /// La bola ha sido teleportada a su posición de respawn pero la cámara aún
    /// no ha completado su transición hacia ella. El input está bloqueado hasta
    /// que <c>BallStateController.ResetState()</c> transite a <see cref="Alive"/>.
    ///
    /// Semántica: la bola "existe y está posicionada" pero el jugador aún no
    /// tiene control sobre ella.
    /// </summary>
    Spawning = 3,
}