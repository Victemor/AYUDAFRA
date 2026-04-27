/// <summary>
/// Define un objeto interactuable en el mundo.
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// Indica si el objeto puede ser interactuado actualmente.
    /// </summary>
    bool CanInteract();
    /// <summary>
    /// Ejecuta la interacción del objeto.
    /// </summary>
    void Interact();
}