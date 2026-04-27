using UnityEngine;

/// <summary>
/// Representa un rango entero basado en inicio y fin.
/// </summary>
[System.Serializable]
public struct IntRange
{
    [SerializeField] private int startValue;
    [SerializeField] private int endValue;

    public int StartValue => startValue;
    public int EndValue => endValue;

    public int Evaluate(float t)
    {
        return Mathf.RoundToInt(Mathf.Lerp(startValue, endValue, t));
    }
}