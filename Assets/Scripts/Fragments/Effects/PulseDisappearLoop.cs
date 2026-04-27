using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(SpriteRenderer))]
public class PulseDisappearLoop : MonoBehaviour
{
    [Header("Escalas")]
    public Vector3 initialScale = Vector3.one;
    public Vector3 finalScale = Vector3.zero;

    [Header("Tiempos de transición")]
    public float baseShrinkTime = 1f;
    public float shrinkTimeRandomMargin = 0.5f;

    public float baseDelayTime = 1f;
    public float delayRandomMargin = 0.5f;

    [Header("Shader")]
    public Material targetMaterial;

    [Tooltip("Valor inicial del parámetro Edge1 al comenzar")]
    [Range(0f, 1f)] public float initialEdgeValue = 0f;

    [Tooltip("Valor final del parámetro Edge1 durante la animación")]
    [Range(0f, 1f)] public float finalEdgeValue = 1f;

    [Header("Fade-In del Shader antes del encogimiento")]
    public float fadeInDuration = 1f;

    private void Start()
    {
        transform.localScale = initialScale;

        if (targetMaterial != null)
        {
            targetMaterial.SetFloat("_Edge1", finalEdgeValue);
        }

        StartLoop();
    }

    void StartLoop()
    {
        float shrinkDuration = baseShrinkTime + Random.Range(-shrinkTimeRandomMargin, shrinkTimeRandomMargin);
        float delayDuration = baseDelayTime + Random.Range(-delayRandomMargin, delayRandomMargin);

        Sequence seq = DOTween.Sequence();

        // Primero, fade de Edge1: final → inicial
        if (targetMaterial != null)
        {
            seq.Append(DOTween.To(
                () => targetMaterial.GetFloat("_Edge1"),
                x => targetMaterial.SetFloat("_Edge1", x),
                initialEdgeValue,
                fadeInDuration
            ).SetEase(Ease.InOutSine));
        }

        // Luego, encoge y aplica el fade final Edge1: inicial → final
        seq.Append(transform.DOScale(finalScale, shrinkDuration).SetEase(Ease.Linear));

        if (targetMaterial != null)
        {
            seq.Join(DOTween.To(
                () => targetMaterial.GetFloat("_Edge1"),
                x => targetMaterial.SetFloat("_Edge1", x),
                finalEdgeValue,
                shrinkDuration
            ).SetEase(Ease.InOutSine));
        }

        // Espera aleatoria y reinicio
        seq.AppendInterval(delayDuration);
        seq.AppendCallback(() =>
        {
            transform.localScale = initialScale;

            if (targetMaterial != null)
                targetMaterial.SetFloat("_Edge1", finalEdgeValue); // Reiniciar visible
        });

        seq.OnComplete(() => StartLoop());
    }
}
