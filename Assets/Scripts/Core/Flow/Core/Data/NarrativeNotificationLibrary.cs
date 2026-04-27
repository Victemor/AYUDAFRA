using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Data;


namespace Game.Runtime
{
    /// <summary>
    /// Biblioteca configurable de mensajes narrativos.
    /// Permite definir mensajes genéricos por tipo y mensajes específicos
    /// basados en una sola condición lógica.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Narrative/Notification Library")]
    public sealed class NarrativeNotificationLibrary : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Mensajes posibles cuando se desbloquea un fragmento de memoria.")]
        private List<string> fragmentUnlockedMessages = new();

        [SerializeField]
        [Tooltip("Mensajes posibles cuando un objeto dentro de una memoria se vuelve más claro o se desbloquea.")]
        private List<string> objectUnlockedMessages = new();

        [SerializeField]
        [Tooltip("Mensajes posibles cuando un nuevo estado del objeto queda disponible para interactuar.")]
        private List<string> stateAvailableMessages = new();

        [SerializeField]
        [Tooltip("Mensajes específicos con prioridad sobre los mensajes genéricos.")]
        private List<NarrativeSpecificMessageByCondition> specificMessages = new();

        /// <summary>
        /// Devuelve los mensajes genéricos configurados para un tipo.
        /// </summary>
        public IReadOnlyList<string> GetMessages(NarrativeNotificationType type)
        {
            return type switch
            {
                NarrativeNotificationType.FragmentUnlocked => fragmentUnlockedMessages,
                NarrativeNotificationType.ObjectUnlocked => objectUnlockedMessages,
                NarrativeNotificationType.StateAvailable => stateAvailableMessages,
                _ => Array.Empty<string>()
            };
        }

        /// <summary>
        /// Intenta resolver un mensaje específico por condición.
        /// </summary>
        public bool TryGetSpecificMessage(
            UnlockNotificationMessage notification,
            RuntimeContext runtimeContext,
            out string resolvedMessage)
        {
            resolvedMessage = string.Empty;

            if (specificMessages == null || specificMessages.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < specificMessages.Count; i++)
            {
                NarrativeSpecificMessageByCondition entry = specificMessages[i];

                if (entry == null)
                {
                    continue;
                }

                if (entry.Matches(notification, runtimeContext))
                {
                    resolvedMessage = entry.Message;
                    return true;
                }
            }

            return false;
        }
    }
}