using UnityEngine;

/// <summary>
/// Configuración de impacto para obstáculos fijos que no deben desplazarse.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class ImmovableObstacle : MonoBehaviour
{
    #region Inspector

    [Header("Impacto")]

    [Tooltip("Multiplicador de velocidad lógica que conserva la pelota tras impactar este obstáculo.")]
    [SerializeField, Range(0f, 1f)] private float speedMultiplier = 0.2f;

    [Tooltip("Velocidad mínima de retroceso aplicada a la pelota al impactar este obstáculo.")]
    [SerializeField, Min(0f)] private float recoilSpeed = 1.75f;

    [Tooltip("Valor mínimo del dot frontal para considerar el impacto como frontal.")]
    [SerializeField, Range(0f, 1f)] private float frontalImpactThreshold = 0.45f;

    #endregion

    #region Properties

    public float SpeedMultiplier => speedMultiplier;
    public float RecoilSpeed => recoilSpeed;
    public float FrontalImpactThreshold => frontalImpactThreshold;

    #endregion

    #region Unity Events

    private void Reset()
    {
        Collider ownCollider = GetComponent<Collider>();
        ownCollider.isTrigger = false;
    }

    private void OnValidate()
    {
        speedMultiplier = Mathf.Clamp01(speedMultiplier);
        recoilSpeed = Mathf.Max(0f, recoilSpeed);
        frontalImpactThreshold = Mathf.Clamp01(frontalImpactThreshold);
    }

    #endregion
}