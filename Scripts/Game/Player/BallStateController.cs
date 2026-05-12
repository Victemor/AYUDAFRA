using System;
using UnityEngine;

/// <summary>
/// Controla el estado del jugador durante el gameplay.
/// </summary>
public sealed class BallStateController : MonoBehaviour
{
    public event Action OnPlayerDied;
    public event Action OnGoalReached;
    public event Action OnStateReset;

    public bool IsDead { get; private set; }
    public bool HasReachedGoal { get; private set; }
    public bool CanControl => !IsDead && !HasReachedGoal;

    /// <summary>
    /// Marca al jugador como muerto y notifica eventos.
    /// </summary>
    public void Die()
    {
        if (IsDead || HasReachedGoal)
        {
            return;
        }

        IsDead = true;
        OnPlayerDied?.Invoke();
        GameEvents.RaisePlayerDied();
    }

    /// <summary>
    /// Marca al jugador como llegado a la meta y notifica eventos.
    /// </summary>
    public void ReachGoal()
    {
        if (IsDead || HasReachedGoal)
        {
            return;
        }

        HasReachedGoal = true;
        OnGoalReached?.Invoke();
        GameEvents.RaiseGoalReached();
    }

    /// <summary>
    /// Reinicia el estado del jugador.
    /// </summary>
    public void ResetState()
    {
        IsDead = false;
        HasReachedGoal = false;
        OnStateReset?.Invoke();
    }
}