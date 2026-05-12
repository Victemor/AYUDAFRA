using UnityEngine;

/// <summary>
/// Rota un objeto visual constantemente.
/// Útil para monedas, power-ups o hélices visuales.
/// </summary>
public sealed class SimpleVisualRotator : MonoBehaviour
{
    #region Inspector

    [Header("Rotación")]
    [SerializeField]
    [Tooltip("Eje local de rotación.")]
    private Vector3 localAxis = Vector3.up;

    [SerializeField]
    [Tooltip("Velocidad de rotación en grados por segundo.")]
    private float degreesPerSecond = 120f;

    [SerializeField]
    [Tooltip("Si está activo, usa tiempo no escalado.")]
    private bool useUnscaledTime;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        if (localAxis.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        transform.Rotate(localAxis.normalized, degreesPerSecond * deltaTime, Space.Self);
    }

    #endregion

    #region Validation

    private void OnValidate()
    {
        if (localAxis.sqrMagnitude <= 0.0001f)
        {
            localAxis = Vector3.up;
        }
    }

    #endregion
}