namespace Game.CursorSystem
{
    /// <summary>
    /// Prioridades recomendadas para resolver conflictos entre solicitudes de cursor.
    /// </summary>
    public static class CursorPriority
    {
        /// <summary>
        /// Prioridad baja para hover de mundo.
        /// </summary>
        public const int WorldHover = 10;

        /// <summary>
        /// Prioridad media para hover de UI.
        /// </summary>
        public const int UiHover = 20;

        /// <summary>
        /// Prioridad alta para acciones de inventario.
        /// </summary>
        public const int Inventory = 30;

        /// <summary>
        /// Prioridad máxima para estados críticos o forzados.
        /// </summary>
        public const int Forced = 100;
    }
}