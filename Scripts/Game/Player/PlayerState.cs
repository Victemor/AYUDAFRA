/// <summary>
/// Estado de ciclo de vida del jugador durante el gameplay.
///
/// Reemplaza los booleans paralelos <c>IsDead</c> y <c>HasReachedGoal</c> que existían
/// antes de este refactor. Un enum garantiza que solo puede existir un estado activo
/// a la vez, eliminando cualquier combinación lógicamente inválida.
///
/// Transiciones válidas:
/// <list type="bullet">
///   <item><see cref="Alive"/> → <see cref="Dead"/> mediante <c>BallStateController.Die()</c></item>
///   <item><see cref="Alive"/> → <see cref="GoalReached"/> mediante <c>BallStateController.ReachGoal()</c></item>
///   <item>Cualquier estado → <see cref="Alive"/> mediante <c>BallStateController.ResetState()</c></item>
/// </list>
/// </summary>
public enum PlayerState
{
    /// <summary>El jugador está vivo y puede recibir input.</summary>
    Alive = 0,

    /// <summary>El jugador ha muerto y espera la secuencia de respawn.</summary>
    Dead = 1,

    /// <summary>El jugador ha alcanzado la meta y el nivel está cerrando.</summary>
    GoalReached = 2,
}