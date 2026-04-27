using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using DG.Tweening;
using System.Collections;

/// <summary>
/// Controla los parámetros de Bloom en el Volume global.
/// Permite modificar color (tint) e intensidad con transiciones suaves.
/// </summary>
public sealed class PostProcessingController : MonoBehaviour
{
    #region Serialized Fields

    [Header("Volume Global")]

    [SerializeField]
    [Tooltip("Volume global que contiene el override de Bloom.")]
    private Volume globalVolume;

    [Header("Transición")]

    [SerializeField]
    [Tooltip("Duración por defecto de las transiciones.")]
    private float transitionDuration = 1f;

    [SerializeField]
    [Tooltip("Tipo de interpolación.")]
    private Ease transitionEase = Ease.InOutSine;

    #endregion

    #region Private Fields

    private Bloom bloom;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        if (globalVolume == null || !globalVolume.profile.TryGet(out bloom))
        {
            Debug.LogError("Bloom no encontrado o Volume no asignado.");
            enabled = false;
        }
    }

    #endregion

    #region Tint Control

    /// <summary>
    /// Cambia el color del Bloom.
    /// </summary>
    /// <param name="color">Color objetivo.</param>
    /// <param name="duration">Duración opcional de la transición.</param>
    public void SetTint(Color color, float duration = -1f)
    {
        if (bloom == null) return;

        float time = duration > 0f ? duration : transitionDuration;

        DOTween.To(
            () => bloom.tint.value,
            v => bloom.tint.value = v,
            color,
            time
        ).SetEase(transitionEase);
    }

    /// <summary>
    /// Cambia el color del Bloom después de un delay.
    /// </summary>
    public void SetTintWithDelay(Color color, float delay, float duration = -1f)
    {
        if (bloom == null) return;

        float time = duration > 0f ? duration : transitionDuration;

        StartCoroutine(DelayedTintChange(color, delay, time));
    }

    private IEnumerator DelayedTintChange(Color target, float delay, float duration)
    {
        yield return new WaitForSeconds(delay);

        DOTween.To(
            () => bloom.tint.value,
            v => bloom.tint.value = v,
            target,
            duration
        ).SetEase(transitionEase);
    }

    #endregion

    #region Intensity Control

    /// <summary>
    /// Cambia la intensidad del Bloom.
    /// </summary>
    public void SetIntensity(float value, float duration = -1f)
    {
        if (bloom == null) return;

        float time = duration > 0f ? duration : transitionDuration;

        DOTween.To(
            () => bloom.intensity.value,
            v => bloom.intensity.value = v,
            value,
            time
        ).SetEase(transitionEase);
    }

    /// <summary>
    /// Cambia la intensidad del Bloom después de un delay.
    /// </summary>
    public void SetIntensityWithDelay(float value, float delay, float duration = -1f)
    {
        if (bloom == null) return;

        float time = duration > 0f ? duration : transitionDuration;

        StartCoroutine(DelayedIntensityChange(value, delay, time));
    }

    private IEnumerator DelayedIntensityChange(float target, float delay, float duration)
    {
        yield return new WaitForSeconds(delay);
        SetIntensity(target, duration);
    }

    #endregion
}