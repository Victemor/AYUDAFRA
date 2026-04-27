using System;
using Game.Data;

namespace Game.Runtime
{
    /// <summary>
    /// Mensaje emitido cuando una entidad narrativa cambia a un estado relevante.
    /// Diseñado para desacoplar runtime, UI y feedback narrativo.
    /// </summary>
    public readonly struct UnlockNotificationMessage
    {
        public UnlockEntityType EntityType { get; }
        public NarrativeNotificationType NotificationType { get; }
        public string MemoryId { get; }
        public string EntityId { get; }
        public string PreviousState { get; }
        public string CurrentState { get; }
        public string Reason { get; }
        public string DisplayMessage { get; }
        public DateTime TimestampUtc { get; }

        public UnlockNotificationMessage(
            UnlockEntityType entityType,
            NarrativeNotificationType notificationType,
            string memoryId,
            string entityId,
            string previousState,
            string currentState,
            string reason,
            string displayMessage)
        {
            EntityType = entityType;
            NotificationType = notificationType;
            MemoryId = memoryId;
            EntityId = entityId;
            PreviousState = previousState;
            CurrentState = currentState;
            Reason = reason;
            DisplayMessage = displayMessage;
            TimestampUtc = DateTime.UtcNow;
        }
    }
}