using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Sistema de lluvia basado en partículas que genera efectos de colisión (splashes)
/// utilizando un pool de objetos para evitar instanciaciones en runtime.
/// Soporta transiciones suaves de intensidad.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class RainController : MonoBehaviour
{
    #region Serialized Fields

    [Header("Prefab que se instancia al colisionar (ej. splash)")]

    [SerializeField]
    [Tooltip("Prefab visual que se instancia al colisionar la lluvia.")]
    private GameObject splashPrefab;

    [Header("Configuración de vida")]

    [SerializeField]
    [Tooltip("Tiempo que permanece activo el splash antes de volver al pool.")]
    private float effectLifetime = 1.5f;

    [Header("Configuración de emisión")]

    [SerializeField]
    [Tooltip("Intensidad inicial de partículas por segundo.")]
    [Min(0)]
    private float particlesPerSecondIntensity = 0;

    [Header("Estado inicial")]

    [SerializeField]
    [Tooltip("Si está activo, la lluvia inicia automáticamente.")]
    private bool startWithRain = false;

    #endregion

    #region Private Fields

    private ParticleSystem rainParticleSystem;
    private List<GameObject> splashPool = new List<GameObject>();
    private List<ParticleCollisionEvent> collisionEvents;

    private int poolIndex = 0;
    private float currentRate = 0f;

    private Coroutine emissionTransitionRoutine;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        EnsureInitialized();
        
    }

    private void Start()
    {
        InitializePool(particlesPerSecondIntensity);
        SetEmissionRate(0f);
        
        if (startWithRain)
            SetRainEmissionWithTimeTransition(particlesPerSecondIntensity);
            
    }

    private void OnParticleCollision(GameObject other)
    {
        EnsureInitialized();
        
        int numEvents = rainParticleSystem.GetCollisionEvents(other, collisionEvents);

        for (int i = 0; i < numEvents; i++)
        {
            Vector3 pos = collisionEvents[i].intersection;
            GameObject splash = GetSplashFromPool();

            if (splash != null)
            {
                splash.transform.position = pos;
                splash.SetActive(true);
                StartCoroutine(DisableAfterSeconds(splash, effectLifetime));
            }
        }
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Garantiza que todas las referencias críticas estén inicializadas.
    /// Permite llamadas seguras incluso antes de Start().
    /// </summary>
    private void EnsureInitialized()
    {
        if (rainParticleSystem == null)
            rainParticleSystem = GetComponent<ParticleSystem>();

        if (collisionEvents == null)
            collisionEvents = new List<ParticleCollisionEvent>();
    }

    #endregion

    #region Pool

    private void InitializePool(float targetRate)
    {
        int desiredSize = Mathf.CeilToInt(targetRate * 2f);
        if (desiredSize <= 0) desiredSize = 1;

        if (splashPool.Count < desiredSize)
        {
            for (int i = splashPool.Count; i < desiredSize; i++)
            {
                GameObject obj = Instantiate(splashPrefab);
                obj.SetActive(false);
                splashPool.Add(obj);
            }
        }
    }

    private GameObject GetSplashFromPool()
    {
        int count = splashPool.Count;

        if (count == 0)
        {
            Debug.LogWarning("[Rain] Splash pool vacío.");
            return null;
        }

        for (int i = 0; i < count; i++)
        {
            int index = (poolIndex + i) % count;

            if (splashPool[index] != null && !splashPool[index].activeInHierarchy)
            {
                poolIndex = (index + 1) % count;
                return splashPool[index];
            }
        }

        int fallbackIndex = poolIndex % count;
        poolIndex = (fallbackIndex + 1) % count;
        return splashPool[fallbackIndex];
    }

    #endregion

    #region Emission Control

    /// <summary>
    /// Aplica directamente la tasa de emisión.
    /// </summary>
    private void SetEmissionRate(float value)
    {
        EnsureInitialized();

        if (rainParticleSystem == null)
        {
            Debug.LogError("[Rain] ParticleSystem no encontrado.");
            return;
        }

        var emission = rainParticleSystem.emission;
        emission.rateOverTime = value;
        currentRate = value;
    }

    /// <summary>
    /// Transiciona suavemente la intensidad de la lluvia.
    /// </summary>
    public void SetRainEmissionWithTimeTransition(float newRate, float transitionDuration = 1f)
    {
        EnsureInitialized();
       
        if (emissionTransitionRoutine != null)
            StopCoroutine(emissionTransitionRoutine);

        if (transitionDuration <= 0f)
            transitionDuration = 0.01f;

        emissionTransitionRoutine = StartCoroutine(
            EmissionTransitionCoroutine(newRate, transitionDuration)
        );
    }

    private IEnumerator EmissionTransitionCoroutine(float newRate, float duration)
    {
        #region Audio Control

       float normalized = Mathf.InverseLerp(0f, 300f, newRate);
        float volumeMultiplier = Mathf.Clamp01(normalized);


        if (newRate > 0f)
        {
           AudioManager.PlayLoopWithFade(SoundId.Rain, 1.5f, volumeMultiplier);
        }
        else
        {
            AudioManager.StopLoopWithFade(SoundId.Rain);
        }

        #endregion

        #region Emission Logic

        InitializePool(newRate);

        float startRate = currentRate;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;

            float easedT = 1f - (1f - t) * (1f - t);

            float value = Mathf.Lerp(startRate, newRate, easedT);
            SetEmissionRate(value);

            yield return null;
        }

        SetEmissionRate(newRate);

        #endregion
    }

    /// <summary>
    /// Detiene la lluvia con transición.
    /// </summary>
    public void StopRain(float duration = 1f)
    {
    
        SetRainEmissionWithTimeTransition(0f, duration);

    }

    #endregion

    #region Utilities

    private IEnumerator DisableAfterSeconds(GameObject obj, float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (obj != null)
            obj.SetActive(false);
    }

    #endregion
}