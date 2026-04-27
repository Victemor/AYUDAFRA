using System.Collections.Generic;
using UnityEngine;
using Game.Data;
using Game;

namespace Game.Runtime
{
    /// <summary>
    /// Resuelve el texto final de una notificación narrativa.
    /// Prioridad:
    /// 1. Mensaje específico por condición.
    /// 2. Mensaje genérico aleatorio por tipo.
    /// 3. Mensaje recibido directamente en la notificación.
    /// 4. Fallback interno en inglés.
    /// </summary>
    public sealed class NarrativeNotificationResolver : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Biblioteca de mensajes narrativos configurables.")]
        private NarrativeNotificationLibrary notificationLibrary;

        /// <summary>
        /// Resuelve el texto final para una notificación dada.
        /// </summary>
        public string ResolveMessage(
            UnlockNotificationMessage message,
            RuntimeContext runtimeContext)
        {
            if (notificationLibrary != null &&
                notificationLibrary.TryGetSpecificMessage(
                    message,
                    runtimeContext,
                    out string specificMessage) &&
                !string.IsNullOrWhiteSpace(specificMessage))
            {
                return specificMessage;
            }

            string configuredMessage = TryGetRandomConfiguredMessage(message.NotificationType);

            if (!string.IsNullOrWhiteSpace(configuredMessage))
            {
                return configuredMessage;
            }

            if (!string.IsNullOrWhiteSpace(message.DisplayMessage))
            {
                return message.DisplayMessage;
            }

            return BuildFallback(message);
        }

        private string TryGetRandomConfiguredMessage(NarrativeNotificationType type)
        {
            if (notificationLibrary == null)
            {
                return string.Empty;
            }

            IReadOnlyList<string> messages = notificationLibrary.GetMessages(type);

            if (messages == null || messages.Count == 0)
            {
                return string.Empty;
            }

            List<string> validMessages = new();

            for (int i = 0; i < messages.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(messages[i]))
                {
                    validMessages.Add(messages[i]);
                }
            }

            if (validMessages.Count == 0)
            {
                return string.Empty;
            }

            int randomIndex = Random.Range(0, validMessages.Count);
            return validMessages[randomIndex];
        }

        private string BuildFallback(UnlockNotificationMessage message)
        {
            return message.NotificationType switch
            {
                NarrativeNotificationType.FragmentUnlocked =>
                    $"A memory fragment has become clearer: {message.MemoryId}.",

                NarrativeNotificationType.ObjectUnlocked =>
                    $"Something connected to this memory has become clearer: {message.EntityId}.",

                NarrativeNotificationType.StateAvailable =>
                    $"Something has changed. I may be able to understand {message.EntityId} differently now.",

                _ =>
                    "Something in my memory seems to have become a little clearer."
            };
        }
    }
}