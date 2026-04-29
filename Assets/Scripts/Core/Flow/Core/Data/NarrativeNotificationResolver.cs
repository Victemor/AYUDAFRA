using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using Game.Data;
using Game;

namespace Game.Runtime
{
    /// <summary>
    /// Resuelve la referencia de localización final de una notificación narrativa.
    ///
    /// Prioridad:
    /// 1. Mensaje específico por condición (desde <see cref="NarrativeNotificationLibrary"/>).
    /// 2. Mensaje genérico aleatorio por tipo (desde la biblioteca).
    /// 3. <c>null</c> — los mensajes dinámicos con EntityId no son localizables
    ///    de forma estática; el Bridge los descarta para no persistir texto sin clave.
    ///
    /// Razón del diseño: los fallback originales contenían datos dinámicos como
    /// <c>message.EntityId</c>. Persistir ese texto en el sistema de consciencia
    /// rompería la localización al cambiar de idioma. Se descarta conscientemente
    /// en lugar de guardar texto ilocalizable.
    /// </summary>
    public sealed class NarrativeNotificationResolver : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Biblioteca de mensajes narrativos configurables.")]
        private NarrativeNotificationLibrary notificationLibrary;

        /// <summary>
        /// Resuelve la referencia de localización para una notificación dada.
        /// Devuelve <c>null</c> si no existe una entrada localizable para este mensaje.
        /// </summary>
        public LocalizedString ResolveMessage(
            UnlockNotificationMessage message,
            RuntimeContext runtimeContext)
        {
            if (notificationLibrary != null &&
                notificationLibrary.TryGetSpecificMessage(
                    message,
                    runtimeContext,
                    out LocalizedString specificMessage) &&
                specificMessage != null &&
                !specificMessage.IsEmpty)
            {
                return specificMessage;
            }

            LocalizedString configuredMessage = TryGetRandomConfiguredMessage(message.NotificationType);

            if (configuredMessage != null && !configuredMessage.IsEmpty)
            {
                return configuredMessage;
            }

            return null;
        }

        private LocalizedString TryGetRandomConfiguredMessage(NarrativeNotificationType type)
        {
            if (notificationLibrary == null)
            {
                return null;
            }

            IReadOnlyList<LocalizedString> messages = notificationLibrary.GetMessages(type);

            if (messages == null || messages.Count == 0)
            {
                return null;
            }

            List<LocalizedString> validMessages = new();

            for (int i = 0; i < messages.Count; i++)
            {
                if (messages[i] != null && !messages[i].IsEmpty)
                {
                    validMessages.Add(messages[i]);
                }
            }

            if (validMessages.Count == 0)
            {
                return null;
            }

            return validMessages[Random.Range(0, validMessages.Count)];
        }
    }
}