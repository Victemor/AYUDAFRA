using UnityEngine;
using DG.Tweening;

/// <summary>
/// Controlador de viento basado en partículas.
/// Permite controlar la intensidad de forma continua (rateOverTime).
/// </summary>
public sealed class WindController : MonoBehaviour
{
    #region Serialized Fields

    [Header("Sistema de partículas")]

    [SerializeField]
    [Tooltip("Sistema de partículas del viento.")]
    private ParticleSystem windParticleSystem;

    [Header("Transición")]

    [SerializeField]
    [Tooltip("Duración de transición por defecto.")]
    private float transitionTime = 0.5f;

    [Header("Límites")]

    [SerializeField]
    [Tooltip("Máximo permitido de emisión (seguridad).")]
    private float maxRate = 300f;

    #endregion

    #region Private Fields

    private float currentRate;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        if (windParticleSystem == null)
        {
            windParticleSystem = GetComponent<ParticleSystem>();
        }

        SetEmissionRate(0f);
        currentRate = 0f;
    }

    #endregion

    #region Core Logic

    /// <summary>
    /// Aplica directamente la intensidad del viento.
    /// </summary>
    /// <param name="targetRate">Cantidad de partículas por segundo.</param>
    /// <param name="transitionTimeDuration">Duración de la transición.</param>
    /// 
    /// 
    public void SetWindEmissionWithTransition(float targetRate, float transitionTimeDuration = -1f)
    {
        if (windParticleSystem == null)
            return;

        float time = transitionTimeDuration > 0f ? transitionTimeDuration : transitionTime;

        targetRate = Mathf.Clamp(targetRate, 0f, maxRate);

        float volumeMultiplier = Mathf.InverseLerp(0f, maxRate, targetRate);

        Debug.Log("TargetRate: " + targetRate);
        Debug.Log("VolumeMultiplier: " + volumeMultiplier);

        if (targetRate > 0f)
        {
            AudioManager.PlayLoopWithFade(SoundId.Wind, 1.5f, volumeMultiplier);
        }
        else
        {
            AudioManager.StopLoopWithFade(SoundId.Wind);
        }

        DOTween.Kill(this);

        DOTween.To(
                () => currentRate,
                SetEmissionRate,
                targetRate,
                time)
            .SetEase(Ease.OutQuad)
            .SetId(this);
    }

    /// <summary>
    /// Establece directamente la emisión sin interpolación.
    /// </summary>
    public void SetWindEmissionAtInstant(float value)
    {
        value = Mathf.Clamp(value, 0f, maxRate);
        SetEmissionRate(value);
    }

    /// <summary>
    /// Apaga completamente el viento.
    /// </summary>
    public void StopWindWithTransitionTime(float transitionTimeDuration = -1f)
    {
        SetWindEmissionWithTransition(0f, transitionTimeDuration);
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Aplica el valor al sistema de partículas.
    /// </summary>
    private void SetEmissionRate(float value)
    {   
        currentRate = value;

        var emission = windParticleSystem.emission;
        emission.rateOverTime = value;
    }

    #endregion
}