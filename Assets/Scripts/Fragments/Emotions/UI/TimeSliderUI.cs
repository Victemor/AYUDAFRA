using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Representa un temporizador visual basado en un Slider.
/// Maneja un conteo regresivo y notifica cuando finaliza.
/// </summary>
public sealed class TimerSliderUI : MonoBehaviour
{
    #region Serialized Fields

    [SerializeField]
    [Tooltip("Slider que representa visualmente el tiempo restante.")]
    private Slider slider;

    [SerializeField]
    [Tooltip("Duración por defecto del temporizador en segundos.")]
    private float defaultDuration = 5f;

    #endregion

    #region Events

    /// <summary>
    /// Se invoca cuando el temporizador finaliza.
    /// </summary>
    public event System.Action OnTimerCompleted;

    #endregion

    #region Private Fields

    private Coroutine timerRoutine;
    private float currentDuration;

    #endregion

    #region Public API

    /// <summary>
    /// Inicia el temporizador con una duración específica.
    /// </summary>
    public void StartTimer(float duration)
    {
        currentDuration = duration;

        if (timerRoutine != null)
        {
            StopCoroutine(timerRoutine);
        }

        timerRoutine = StartCoroutine(TimerRoutine());
    }

    /// <summary>
    /// Inicia el temporizador usando la duración por defecto.
    /// </summary>
    public void StartTimer()
    {
        StartTimer(defaultDuration);
    }

    /// <summary>
    /// Detiene el temporizador actual.
    /// </summary>
    public void StopTimer()
    {
        if (timerRoutine != null)
        {
            StopCoroutine(timerRoutine);
            timerRoutine = null;
        }
    }

    #endregion

    #region Core Logic

    /// <summary>
    /// Rutina interna del countdown.
    /// </summary>
    private IEnumerator TimerRoutine()
    {
        float remainingTime = currentDuration;

        slider.maxValue = currentDuration;
        slider.value = currentDuration;

        while (remainingTime > 0f)
        {
            remainingTime -= Time.deltaTime;
            slider.value = remainingTime;

            yield return null;
        }

        slider.value = 0f;
        timerRoutine = null;

        OnTimerCompleted?.Invoke();
    }

    #endregion
}