namespace Game.Runtime
{
    /// <summary>
    /// Genera mensajes fallback para desbloqueos y estados narrativos.
    /// Estos mensajes solo se usan cuando no existe un mensaje específico,
    /// genérico o directo configurado.
    /// </summary>
    public static class UnlockMessageBuilder
    {
        /// <summary>
        /// Construye el mensaje fallback para una memoria desbloqueada.
        /// </summary>
        public static string BuildMemoryUnlocked(string memoryId)
        {
            return "Seeing this seems to have unlocked a new fragment of my memory.";
        }

        /// <summary>
        /// Construye el mensaje fallback para un objeto desbloqueado dentro de una memoria.
        /// </summary>
        public static string BuildObjectUnlocked(string memoryId, string objectId)
        {
            return "Connecting these memory fragments made something clearer. Something inside a fragment now feels more defined.";
        }

        /// <summary>
        /// Construye el mensaje fallback para un nuevo estado disponible en un objeto.
        /// </summary>
        public static string BuildStateAvailable(string memoryId, string objectId)
        {
            return "I feel that something inside a fragment may now be understood differently.";
        }
    }
}