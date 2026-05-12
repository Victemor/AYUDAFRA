/// <summary>
/// Grupo continuo de chunks donde se debe generar un borde sin cortes intermedios.
/// </summary>
public readonly struct TrackBarrierRun
{
    /// <summary>
    /// Índice inicial del run dentro de la lista de chunks seleccionados.
    /// </summary>
    public int StartIndex { get; }

    /// <summary>
    /// Índice final del run dentro de la lista de chunks seleccionados.
    /// </summary>
    public int EndIndex { get; }

    /// <summary>
    /// Crea un nuevo run continuo de bordes.
    /// </summary>
    public TrackBarrierRun(int startIndex, int endIndex)
    {
        StartIndex = startIndex;
        EndIndex = endIndex;
    }
}