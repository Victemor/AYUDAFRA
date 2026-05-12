using UnityEngine;

/// <summary>
/// Resultado de muestreo de la trayectoria final del track.
/// </summary>
public readonly struct TrackSample
{
    /// <summary>
    /// Posición mundial sobre la trayectoria.
    /// </summary>
    public Vector3 Position { get; }

    /// <summary>
    /// Dirección tangente del track en el sample.
    /// </summary>
    public Vector3 Forward { get; }

    /// <summary>
    /// Vector lateral del track en el sample.
    /// </summary>
    public Vector3 Right { get; }

    /// <summary>
    /// Distancia acumulada correspondiente al sample.
    /// </summary>
    public float Distance { get; }

    /// <summary>
    /// Crea un nuevo sample de trayectoria.
    /// </summary>
    public TrackSample(
        Vector3 position,
        Vector3 forward,
        Vector3 right,
        float distance)
    {
        Position = position;
        Forward = forward;
        Right = right;
        Distance = distance;
    }
}