using UnityEngine;

/// <summary>
/// Datos de un gesto de swipe clasificado.
/// </summary>
public readonly struct SwipeData
{
    /// <summary>Dirección normalizada del swipe en espacio de pantalla.</summary>
    public Vector2 ScreenDirection { get; }

    /// <summary>Longitud del gesto en píxeles.</summary>
    public float Length { get; }

    /// <summary>Velocidad del gesto en píxeles por segundo.</summary>
    public float Speed { get; }

    /// <summary>Intent clasificado del gesto.</summary>
    public SwipeIntent Intent { get; }

    /// <summary>
    /// Factor de intensidad normalizado [0,1] combinando longitud y velocidad.
    /// </summary>
    public float Intensity { get; }

    public SwipeData(
        Vector2 screenDirection,
        float length,
        float speed,
        SwipeIntent intent,
        float intensity)
    {
        ScreenDirection = screenDirection;
        Length = length;
        Speed = speed;
        Intent = intent;
        Intensity = intensity;
    }
}

/// <summary>
/// Intent clasificado de un gesto de swipe.
/// </summary>
public enum SwipeIntent
{
    /// <summary>Swipe hacia arriba — acelerar.</summary>
    Forward = 0,

    /// <summary>Swipe hacia abajo — frenar o rotar atrás.</summary>
    Backward = 1,

    /// <summary>Swipe hacia la izquierda — rotar izquierda.</summary>
    Left = 2,

    /// <summary>Swipe hacia la derecha — rotar derecha.</summary>
    Right = 3,

    /// <summary>Swipe diagonal adelante-izquierda — rotar y acelerar.</summary>
    DiagonalForwardLeft = 4,

    /// <summary>Swipe diagonal adelante-derecha — rotar y acelerar.</summary>
    DiagonalForwardRight = 5,

    /// <summary>Swipe diagonal atrás-izquierda — frenar con sesgo de rotación.</summary>
    DiagonalBackwardLeft = 6,

    /// <summary>Swipe diagonal atrás-derecha — frenar con sesgo de rotación.</summary>
    DiagonalBackwardRight = 7
}