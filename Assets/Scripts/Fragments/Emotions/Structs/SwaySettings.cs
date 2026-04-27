using UnityEngine;

/// <summary>
/// Configuración de sway de cámara basada en interpolación.
/// </summary>
[System.Serializable]
public struct SwaySettings
{
    [SerializeField] private FloatRange amountX;
    [SerializeField] private FloatRange amountZ;
    [SerializeField] private FloatRange speed;

    public (float x, float z, float speed) Evaluate(float t)
    {
        return (
            amountX.Evaluate(t),
            amountZ.Evaluate(t),
            speed.Evaluate(t)
        );
    }
}