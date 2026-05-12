namespace Game.CameraSystem
{
    /// <summary>
    /// Estado vertical actual del encuadre de cámara.
    /// </summary>
    public enum CameraVerticalState
    {
        /// <summary>
        /// Encuadre normal.
        /// </summary>
        Normal = 0,

        /// <summary>
        /// Encuadre para subida.
        /// </summary>
        Ascending = 1,

        /// <summary>
        /// Encuadre para bajada.
        /// </summary>
        Descending = 2,

        /// <summary>
        /// Encuadre temporal de transición por respawn.
        /// </summary>
        RespawnTransition = 3
    }
}