using UnityEngine;

/// <summary>
/// Representa un rango de valores basado en inicio y fin.
/// </summary>
[System.Serializable]
public struct FloatRange
{
    [SerializeField] private float startValue;
    [SerializeField] private float endValue;

    public float StartValue => startValue;
    public float EndValue => endValue;

    /// <summary>
    /// Evalúa el valor según una interpolación (0 → 1).
    /// </summary>
    public float Evaluate(float t)
    {
        return Mathf.Lerp(startValue, endValue, t);
    }
}