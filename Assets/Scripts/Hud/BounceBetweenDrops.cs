using UnityEngine;

/// <summary>
/// Partícula que rebota visualmente entre dos gotas conectadas.
/// </summary>
public class BounceBetweenDrops : MonoBehaviour
{
    [Header("References")]
    private Transform dropA;
    private Transform dropB;

    [Header("Movement")]
    [SerializeField] private float speed = 300f;

    [Header("Visual")]
    [SerializeField] private TrailRenderer trail;
    [SerializeField] private float dimmedAlpha = 0.06f;
    [SerializeField] private float normalAlpha = 1f;

    private Transform target;

    public string fragmentA;
    public string fragmentB;

    /// <summary>
    /// Inicializa la conexión.
    /// </summary>
    public void Initialize(
        Transform a,
        Transform b,
        string fragmentA,
        string fragmentB
    )
    {
        dropA = a;
        dropB = b;

        this.fragmentA = fragmentA;
        this.fragmentB = fragmentB;

        transform.position = dropA.position;
        target = dropB;
    }

    private void Update()
    {
        if (dropA == null || dropB == null)
            return;

        transform.position = Vector3.MoveTowards(
            transform.position,
            target.position,
            speed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, target.position) < 0.1f)
        {
            target = target == dropA ? dropB : dropA;
        }
    }

     #region Visual State

    public void SetDimmed()
    {
        SetTrailAlpha(dimmedAlpha);
    }

    public void SetNormal()
    {
        SetTrailAlpha(normalAlpha);
    }

    private void SetTrailAlpha(float alpha)
    {
        if (trail == null)
            return;

        Gradient gradient = trail.colorGradient;
        GradientAlphaKey[] alphaKeys = gradient.alphaKeys;

        for (int i = 0; i < alphaKeys.Length; i++)
        {
            alphaKeys[i].alpha = alpha;
        }

        gradient.alphaKeys = alphaKeys;
        trail.colorGradient = gradient;
    }

    #endregion
}
