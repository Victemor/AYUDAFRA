/// <summary>
/// Identifica el tipo de efecto de un power-up coleccionable.
///
/// Cada valor mapea a una rama de lógica en <see cref="BallPowerUpController"/>
/// y a un conjunto de parámetros en <see cref="CollectiblePowerUpData"/>.
/// Usar este enum en lugar de strings elimina magic-values en todo el sistema.
/// </summary>
public enum CollectiblePowerUpType
{
    /// <summary>
    /// Reduce la fricción pasiva y la adhesión al suelo durante un período.
    /// La bola mantiene velocidad por más tiempo y puede "flotar" levemente sobre rampas.
    /// </summary>
    ReducedInertia = 0,

    /// <summary>
    /// Aumenta el radio de recolección de monedas y las atrae hacia la bola.
    /// </summary>
    Magnet = 1,
}