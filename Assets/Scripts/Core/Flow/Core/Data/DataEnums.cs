namespace Game.Data
{
    /// <summary>
    /// Estado del fragmento de memoria.
    /// </summary>
    public enum MemoryState
    {
        Locked = 0,
        Visible = 1,
        Seen = 2,
        Completed = 3
    }

    /// <summary>
    /// Tipo de emoción narrativa.
    /// </summary>
    public enum EmotionType
    {
        Guilt = 0,
        Repression = 1,
        Nostalgia = 2,
        Confusion = 3,
        Acceptance = 4,
        Neutral = 5
    }

    /// <summary>
    /// Nivel de intensidad narrativa.
    /// </summary>
    public enum IntensityLevel
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    /// <summary>
    /// Modo de comparación para requisitos de estado de objeto.
    /// </summary>
    public enum ObjectStateComparisonMode
    {
        Exact = 0,
        ThisStateOrLater = 1
    }

    /// <summary>
    /// Clasifica el tipo de entidad que generó la notificación.
    /// </summary>
    public enum UnlockEntityType
    {
        Memory = 0,
        Object = 1,
        ObjectState = 2
    }

    /// <summary>
    /// Clasifica los tipos de mensaje narrativo que pueden resolverse
    /// a partir de eventos del sistema.
    /// </summary>
    public enum NarrativeNotificationType
    {
        FragmentUnlocked = 0,
        ObjectUnlocked = 1,
        StateAvailable = 2
    }
}