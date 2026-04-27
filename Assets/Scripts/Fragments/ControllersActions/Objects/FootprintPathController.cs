using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Controlador encargado de generar y animar una secuencia de huellas
/// sobre una trayectoria definida por una lista de puntos.
/// </summary>
public class FootprintPathController : MonoBehaviour
{
    #region Inspector

    [Header("Iniciar animación al iniciar")]
    [Tooltip("Indica si la animación de huellas debe comenzar automáticamente al iniciar el componente.")]
    public bool startOnAwake = false;

    [Header("Ruta de puntos (Transform)")]
    [Tooltip("Lista ordenada de puntos que define la trayectoria base para generar las huellas.")]
    public List<Transform> pathPoints;

    [Header("Prefab de huella")]
    [Tooltip("Prefab visual que representa una huella individual.")]
    public GameObject footprintPrefab;

    [Header("Parámetros de animación")]
    [Tooltip("Duración del fade de entrada de cada huella en modo normal.")]
    public float fadeInTime = 0.3f;

    [Tooltip("Duración del fade de salida de cada huella.")]
    public float fadeOutTime = 0.5f;

    [Tooltip("Tiempo de espera entre una huella y la siguiente.")]
    public float delayBetweenSteps = 0.2f;

    [Tooltip("Tiempo que cada huella permanece visible antes de comenzar su fade out en modo normal.")]
    public float footprintLifetime = 2f;

    [Tooltip("Retraso inicial antes de comenzar la animación.")]
    public float startDelay = 0.5f;

    [Header("Modo de media animación")]
    [Tooltip("Si está activo, primero aparecen todas las huellas y luego desaparecen secuencialmente.")]
    public bool halfAnimationMode = false;

    [Header("Separación de huellas")]
    [Tooltip("Distancia mínima recorrida sobre la ruta para generar una nueva huella.")]
    public float stepSpacing = 1.0f;

    [Tooltip("Separación lateral entre huellas izquierdas y derechas.")]
    public float footSeparation = 0.4f;

    [Header("Aleatoriedad de la pisada")]
    [Tooltip("Desplazamiento aleatorio máximo aplicado a cada huella para romper la uniformidad visual.")]
    public float maxRandomOffset = 0.1f;

    [Header("Parámetros internos")]
    [Tooltip("Resolución usada para suavizar la ruta mediante interpolación. Valores más bajos generan más puntos.")]
    [Range(0.01f, 1f)]
    public float smoothingResolution = 0.05f;

    [Tooltip("Duración del fade de entrada usada específicamente en el modo de media animación.")]
    public float fadeInTimeHalf = 1f;

    [Header("Audio")]
    [Tooltip("Tiempo mínimo entre reproducciones del sonido de pasos.")]
    public float footstepSoundCooldown = 0.3f;

    #endregion

    #region Private Fields

    /// <summary>
    /// Referencia a la corrutina activa de animación para evitar ejecuciones solapadas.
    /// </summary>
    private Coroutine animationCoroutine;

    /// <summary>
    /// Tiempo en el que se reprodujo el último sonido de pasos.
    /// Se utiliza para evitar la superposición de audio en secuencias densas.
    /// </summary>
    private float lastFootstepSoundTime = float.NegativeInfinity;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Inicia automáticamente la animación si así fue configurado desde el inspector.
    /// </summary>
    private void Start()
    {
        if (startOnAwake)
            StartFootprintAnimation();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Reemplaza la ruta actual por una nueva lista de puntos y reinicia la animación.
    /// </summary>
    /// <param name="newPath">Nueva trayectoria base para la animación de huellas.</param>
    public void SetPathPoints(List<Transform> newPath)
    {
        pathPoints = newPath;
        StartFootprintAnimation();
    }

    /// <summary>
    /// Configura el modo de reproducción y ajusta la velocidad de la animación
    /// antes de iniciar la secuencia de huellas.
    /// </summary>
    /// <param name="useHalfAnimationMode">Indica si debe usarse el modo de media animación.</param>
    /// <param name="speedMultiplier">Multiplicador de velocidad aplicado a los tiempos de animación.</param>
    public void PlayFootprints(bool useHalfAnimationMode, float speedMultiplier = 1f)
    {
        halfAnimationMode = useHalfAnimationMode;
        ApplySpeedMultiplier(speedMultiplier);
        StartFootprintAnimation();
    }

    /// <summary>
    /// Valida la configuración actual, genera una trayectoria suavizada
    /// y lanza la corrutina principal de animación.
    /// </summary>
    public void StartFootprintAnimation()
    {
        if (pathPoints == null || pathPoints.Count < 2 || footprintPrefab == null)
        {
            Debug.LogWarning("Faltan puntos o el prefab no está asignado.");
            return;
        }

        List<Vector3> smoothPath = GenerateSmoothPath(pathPoints, smoothingResolution);

        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }

        animationCoroutine = StartCoroutine(AnimateFootprints(smoothPath));
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Ajusta los tiempos principales de la animación en función de un multiplicador de velocidad.
    /// </summary>
    private void ApplySpeedMultiplier(float speedMultiplier)
    {
        speedMultiplier = Mathf.Max(0.01f, speedMultiplier);

        delayBetweenSteps = 0.2f / speedMultiplier;
        footprintLifetime = 2f / speedMultiplier;
        fadeInTime = 0.3f / speedMultiplier;
        fadeOutTime = 0.5f / speedMultiplier;
    }

    /// <summary>
    /// Genera una trayectoria suavizada a partir de los puntos base usando interpolación Catmull-Rom.
    /// </summary>
    private List<Vector3> GenerateSmoothPath(List<Transform> points, float resolution)
    {
        List<Vector3> result = new List<Vector3>();

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 p0 = points[Mathf.Max(i - 1, 0)].position;
            Vector3 p1 = points[i].position;
            Vector3 p2 = points[i + 1].position;
            Vector3 p3 = points[Mathf.Min(i + 2, points.Count - 1)].position;

            for (float t = 0; t < 1f; t += resolution)
            {
                Vector3 pos = CatmullRom(p0, p1, p2, p3, t);
                result.Add(pos);
            }
        }

        return result;
    }

    /// <summary>
    /// Calcula una posición interpolada sobre una curva Catmull-Rom.
    /// </summary>
    private Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        return 0.5f * ((2f * p1) +
                       (-p0 + p2) * t +
                       (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t +
                       (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);
    }

    #endregion

    #region Coroutines

    /// <summary>
    /// Genera huellas a lo largo de la trayectoria suavizada,
    /// controlando orientación, alternancia de pies y reproducción de audio.
    /// </summary>
    private IEnumerator AnimateFootprints(List<Vector3> path)
    {
        yield return new WaitForSeconds(startDelay);

        float distance = 0f;
        int stepIndex = 0;

        List<GameObject> allFootprints = new List<GameObject>();

        for (int i = 1; i < path.Count; i++)
        {
            distance += Vector3.Distance(path[i - 1], path[i]);

            if (distance >= stepSpacing)
            {
                distance = 0f;

                Vector3 dir = (path[i] - path[i - 1]).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, dir);

                bool isLeftFoot = (stepIndex % 2 == 0);

                /// <summary>
                /// Reproduce el sonido de pasos únicamente en un pie y respetando un cooldown,
                /// evitando la saturación cuando la densidad de huellas es alta.
                /// </summary>
                if (isLeftFoot && Time.time - lastFootstepSoundTime >= footstepSoundCooldown)
                {
                    AudioManager.PlaySfx(SoundId.Pisada);
                    lastFootstepSoundTime = Time.time;
                }

                Vector3 offset = right * (isLeftFoot ? -footSeparation / 2f : footSeparation / 2f);

                Vector3 randomOffset = new Vector3(
                    Random.Range(-maxRandomOffset, maxRandomOffset),
                    Random.Range(-maxRandomOffset, maxRandomOffset),
                    0f
                );

                Vector3 spawnPos = path[i] + offset + randomOffset;
                Quaternion rot = Quaternion.LookRotation(Vector3.forward, dir);

                GameObject footprint = Instantiate(footprintPrefab, spawnPos, rot);
                SpriteRenderer sr = footprint.GetComponent<SpriteRenderer>();

                if (sr != null)
                {
                    if (halfAnimationMode)
                    {
                        sr.color = new Color(1, 1, 1, 0);
                        sr.DOFade(1f, fadeInTimeHalf);
                        allFootprints.Add(footprint);
                    }
                    else
                    {
                        sr.color = new Color(1, 1, 1, 0);
                        sr.DOFade(1f, fadeInTime);
                        sr.DOFade(0f, fadeOutTime)
                            .SetDelay(footprintLifetime)
                            .OnComplete(() => Destroy(footprint));
                    }
                }
                else
                {
                    Destroy(footprint, fadeInTime + footprintLifetime + fadeOutTime);
                }

                stepIndex++;

                if (!halfAnimationMode)
                {
                    yield return new WaitForSeconds(delayBetweenSteps);
                }
            }
        }

        if (halfAnimationMode)
        {
            foreach (GameObject fp in allFootprints)
            {
                SpriteRenderer sr = fp.GetComponent<SpriteRenderer>();

                if (sr != null)
                {
                    sr.DOFade(0f, fadeOutTime).OnComplete(() => Destroy(fp));
                }
                else
                {
                    Destroy(fp, fadeOutTime);
                }

                yield return new WaitForSeconds(delayBetweenSteps);
            }
        }
    }

    #endregion
}