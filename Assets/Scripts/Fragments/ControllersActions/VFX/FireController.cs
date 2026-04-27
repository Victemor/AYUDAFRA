using System.Collections;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Controla la activación, desactivación y transición visual de un fuego
/// mediante un material y un sistema de partículas hijo.
/// </summary>
public sealed class FireController : MonoBehaviour
{
    #region Constants

    /// <summary>
    /// Nombre del parámetro del shader que representa la intensidad del fuego.
    /// </summary>
    private const string MaterialPowerParameter = "_Power";

    #endregion

    #region Inspector

    [Header("Initial State")]
    [SerializeField]
    [Tooltip("Indica si el fuego debe iniciar encendido al cargar la escena.")]
    private bool startOn = false;

    [Header("Transition Settings")]
    [SerializeField]
    [Tooltip("Duración por defecto usada en las transiciones de encendido y apagado.")]
    private float defaultFadeDuration = 1f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Intensidad por defecto usada al encender el fuego.")]
    private float defaultIntensity = 1f;

    [SerializeField]
    [Tooltip("Cantidad base de emisión de partículas cuando el fuego está activo.")]
    private float numberOfParticles = 6f;

    #endregion

    #region Private Fields

    /// <summary>
    /// Instancia del material del renderer asociada al fuego.
    /// </summary>
    private Material fireMaterial;

    /// <summary>
    /// Sistema de partículas hijo que complementa visualmente el fuego.
    /// </summary>
    private ParticleSystem childParticles;

    /// <summary>
    /// Rutina activa para comportamientos temporales del fuego.
    /// Se reutiliza para evitar múltiples secuencias concurrentes.
    /// </summary>
    private Coroutine timedRoutine;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Inicializa las referencias visuales del fuego y aplica el estado inicial configurado.
    /// </summary>
    private void Awake()
    {
        Renderer rendererComponent = GetComponent<Renderer>();
        if (rendererComponent != null)
        {
            fireMaterial = rendererComponent.material;
        }

        childParticles = GetComponentInChildren<ParticleSystem>(true);

        if (startOn)
        {
            SetPowerInstant(defaultIntensity);
            SetParticleEmission(numberOfParticles);
            childParticles?.Play();
        }
        else
        {
            SetPowerInstant(0f);
            SetParticleEmission(0f);
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Enciende el fuego con una transición suave hasta la intensidad indicada.
    /// </summary>
    /// <param name="intensity">Intensidad objetivo del fuego.</param>
    /// <param name="fadeTime">
    /// Duración de la transición. Si es menor o igual a cero, se usa la duración por defecto.
    /// </param>
   public void StartFire(float intensity = 1f, float fadeTime = -1f)
{
    if (fadeTime <= 0f)
    {
        fadeTime = defaultFadeDuration;
    }

    float clampedIntensity = Mathf.Clamp01(intensity);
    float volumeMultiplier = clampedIntensity * clampedIntensity * clampedIntensity;

     AudioManager.PlayLoopWithFade(SoundId.Fire, 1.5f, volumeMultiplier);

    DOTween.Kill(this);

    float currentValue = fireMaterial != null
        ? fireMaterial.GetFloat(MaterialPowerParameter)
        : 0f;

    DOTween.To(
            () => currentValue,
            value =>
            {
                currentValue = value;

                if (fireMaterial != null)
                {
                    fireMaterial.SetFloat(MaterialPowerParameter, value);
                }

                SetParticleEmission(value);
            },
            intensity,
            fadeTime)
        .SetEase(Ease.OutQuad)
        .SetId(this);
}

    /// <summary>
    /// Apaga el fuego con una transición suave hasta intensidad cero.
    /// </summary>
    /// <param name="fadeTime">
    /// Duración de la transición. Si es menor o igual a cero, se usa la duración por defecto.
    /// </param>
    public void StopFire(float fadeTime = -1f)
    {
        AudioManager.StopLoopWithFade(SoundId.Fire);
        if (fadeTime <= 0f)
        {
            
            fadeTime = defaultFadeDuration;
        }

        DOTween.Kill(this);

        float currentValue = fireMaterial != null
            ? fireMaterial.GetFloat(MaterialPowerParameter)
            : 1f;

        DOTween.To(
                () => currentValue,
                value =>
                {
                    if (fireMaterial != null)
                    {
                        fireMaterial.SetFloat(MaterialPowerParameter, value);
                    }

                    SetParticleEmission(value);
                },
                0f,
                fadeTime)
            .SetEase(Ease.InExpo)
            .SetId(this);
    }

    /// <summary>
    /// Enciende el fuego durante un tiempo determinado y luego lo apaga automáticamente.
    /// </summary>
    /// <param name="intensity">Intensidad con la que se encenderá el fuego.</param>
    /// <param name="seconds">Tiempo que permanecerá encendido antes de apagarse.</param>
    /// <param name="fadeInTime">
    /// Duración del encendido. Si es menor o igual a cero, se usa la duración por defecto.
    /// </param>
    /// <param name="fadeOutTime">
    /// Duración del apagado. Si es menor o igual a cero, se usa la duración por defecto.
    /// </param>
    public void StartFireForSeconds(float intensity, float seconds, float fadeInTime = -1f, float fadeOutTime = -1f)
    {
        if (fadeInTime <= 0f)
        {
            fadeInTime = defaultFadeDuration;
        }

        if (fadeOutTime <= 0f)
        {
            fadeOutTime = defaultFadeDuration;
        }

        if (timedRoutine != null)
        {
            StopCoroutine(timedRoutine);
        }

        timedRoutine = StartCoroutine(FireTemporaryOnRoutine(intensity, seconds, fadeInTime, fadeOutTime));
    }

    /// <summary>
    /// Apaga el fuego temporalmente y luego lo restaura a una intensidad determinada.
    /// </summary>
    /// <param name="seconds">Tiempo que permanecerá apagado.</param>
    /// <param name="returnIntensity">
    /// Intensidad a la que volverá el fuego. Si es menor que cero, se usa la intensidad por defecto.
    /// </param>
    /// <param name="fadeOutTime">
    /// Duración del apagado. Si es menor o igual a cero, se usa la duración por defecto.
    /// </param>
    /// <param name="fadeInTime">
    /// Duración del reencendido. Si es menor o igual a cero, se usa la duración por defecto.
    /// </param>
    public void StopFireForSeconds(float seconds, float returnIntensity = -1f, float fadeOutTime = -1f, float fadeInTime = -1f)
    {
        if (fadeInTime <= 0f)
        {
            fadeInTime = defaultFadeDuration;
        }

        if (fadeOutTime <= 0f)
        {
            fadeOutTime = defaultFadeDuration;
        }

        if (timedRoutine != null)
        {
            StopCoroutine(timedRoutine);
        }

        timedRoutine = StartCoroutine(FireTemporaryOffRoutine(seconds, returnIntensity, fadeOutTime, fadeInTime));
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Aplica instantáneamente la intensidad del material del fuego sin transición.
    /// </summary>
    /// <param name="value">Valor de intensidad a aplicar.</param>
    private void SetPowerInstant(float value)
    {
        fireMaterial?.SetFloat(MaterialPowerParameter, value);
    }

    /// <summary>
    /// Ajusta la emisión del sistema de partículas en función de la intensidad recibida.
    /// Además, inicia o detiene el sistema según corresponda.
    /// </summary>
    /// <param name="value">Valor de emisión deseado.</param>
    private void SetParticleEmission(float value)
    {
        if (childParticles == null)
        {
            return;
        }

        ParticleSystem.EmissionModule emission = childParticles.emission;
        emission.rateOverTime = value;

        if (value > 0f && !childParticles.isPlaying)
        {
            childParticles.Play();
        }
        else if (value <= 0f && childParticles.isPlaying)
        {
            childParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    #endregion

    #region Coroutines

    /// <summary>
    /// Enciende el fuego, espera un tiempo y luego lo apaga.
    /// </summary>
    /// <param name="intensity">Intensidad objetivo al encender.</param>
    /// <param name="seconds">Tiempo que permanecerá encendido.</param>
    /// <param name="fadeInTime">Duración del encendido.</param>
    /// <param name="fadeOutTime">Duración del apagado.</param>
    /// <returns>Rutina de control temporal del fuego.</returns>
    private IEnumerator FireTemporaryOnRoutine(float intensity, float seconds, float fadeInTime, float fadeOutTime)
    {
        StartFire(intensity, fadeInTime);
        yield return new WaitForSeconds(seconds);
        StopFire(fadeOutTime);
    }

    /// <summary>
    /// Apaga el fuego, espera un tiempo y luego lo vuelve a encender.
    /// </summary>
    /// <param name="seconds">Tiempo que permanecerá apagado.</param>
    /// <param name="returnIntensity">Intensidad a restaurar al finalizar la espera.</param>
    /// <param name="fadeOutTime">Duración del apagado.</param>
    /// <param name="fadeInTime">Duración del encendido.</param>
    /// <returns>Rutina de restauración temporal del fuego.</returns>
    private IEnumerator FireTemporaryOffRoutine(float seconds, float returnIntensity, float fadeOutTime, float fadeInTime)
    {
        StopFire(fadeOutTime);
        yield return new WaitForSeconds(seconds);

        float intensity = returnIntensity >= 0f
            ? returnIntensity
            : defaultIntensity;

        StartFire(intensity, fadeInTime);
    }

    #endregion
}