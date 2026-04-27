using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(SpriteRenderer))]
public class RippleDropController : MonoBehaviour
{
    [Header("Comportamiento especial")]
    [Tooltip("Simula una onda que aparece desde nada y se desvanece al contraerse")]
    public bool rippleWater = false;

    [SerializeField] private bool onStart = false;
    [Header("Escalas")]
    public Vector3 initialScale = Vector3.one;
    public Vector3 finalScale = Vector3.zero;

    [Header("Tiempos de transición")]
    public float baseShrinkTime = 1f;
    public float shrinkTimeRandomMargin = 0.5f;

    public float baseDelayTime = 1f;
    public float delayRandomMargin = 0.5f;

    [Header("Delay inicial")]
    [Tooltip("Tiempo en segundos antes de comenzar la primera animación")]
    public float initialStartDelay = 0.5f;

    [Header("Shader")]
    public Material baseMaterial;

    [Tooltip("Valor inicial del parámetro Edge1 al comenzar")]
    [Range(0f, 1f)] public float initialEdgeValue = 0f;

    [Tooltip("Valor final del parámetro Edge1 durante la animación")]
    [Range(0f, 1f)] public float finalEdgeValue = 1f;

    [Header("Fade-In del Shader antes del encogimiento")]
    public float fadeInDuration = 1f;

    private Material instanceMaterial;
    private Sequence pulseSequence;
    private bool isLoopRunning = false;
    private bool isInitialized = false;

    private void Start()
    {
        transform.localScale = initialScale;

        if (baseMaterial != null)
        {
            instanceMaterial = new Material(baseMaterial);
            GetComponent<SpriteRenderer>().material = instanceMaterial;
            instanceMaterial.SetFloat("_Edge1", initialEdgeValue);
        }
        if (onStart)
        {
            StartPulse();
        }
    }

    /// <summary>
    /// Inicia la animación desde otro script (una sola vez).
    /// </summary>
    public void StartPulse()
    {
        if (!isInitialized)
        {
            isInitialized = true;

            if (instanceMaterial != null)
            {
                instanceMaterial.SetFloat("_Edge1", initialEdgeValue); // No final
            }

            transform.localScale = initialScale; // Escala grande
            Invoke(nameof(StartLoop), initialStartDelay);
        }
    }

    private void StartLoop()
    {
        isLoopRunning = true;

        float shrinkDuration = baseShrinkTime + Random.Range(-shrinkTimeRandomMargin, shrinkTimeRandomMargin);
        float delayDuration = baseDelayTime + Random.Range(-delayRandomMargin, delayRandomMargin);

        pulseSequence?.Kill();
        pulseSequence = DOTween.Sequence();

        //Fade in de Edge1: final → inicial
        if (instanceMaterial != null)
        {
            pulseSequence.Append(DOTween.To(
                () => instanceMaterial.GetFloat("_Edge1"),
                x => instanceMaterial.SetFloat("_Edge1", x),
                initialEdgeValue,
                fadeInDuration
            ).SetEase(Ease.InOutSine).OnComplete(() =>
            {
                DOTween.To(
                () => instanceMaterial.GetFloat("_Edge1"),
                x => instanceMaterial.SetFloat("_Edge1", x),
                finalEdgeValue,
                shrinkDuration - fadeInDuration
            ).SetEase(Ease.InOutSine);
            }));
        }

        // Encogimiento
        pulseSequence.Join(transform.DOScale(finalScale, shrinkDuration).SetEase(Ease.Linear));

     

        // Reset y espera
        pulseSequence.AppendInterval(delayDuration);
        pulseSequence.AppendCallback(() =>
        {
            transform.localScale = initialScale;
            if (instanceMaterial != null)
                instanceMaterial.SetFloat("_Edge1", finalEdgeValue);
        });

        // Continuar si sigue activo
        pulseSequence.OnComplete(() =>
        {
            if (isLoopRunning)
                StartLoop();
        });
    }

    /// <summary>
    /// Detiene el loop de manera limpia al final del ciclo actual.
    /// </summary>
    public void StopPulseLoop()
    {
        isLoopRunning = false;
        // No se hace Kill(), se deja terminar el ciclo actual
    }

    /// <summary>
    /// Reanuda la animación si estaba detenida.
    /// </summary>
    public void RestartPulseLoop()
    {
        if (!isLoopRunning)
        {
            StartLoop();
        }
    }
}
