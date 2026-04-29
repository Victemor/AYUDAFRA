using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using Game.Data;

namespace Game.Runtime
{
    /// <summary>
    /// Biblioteca configurable de mensajes narrativos.
    /// Permite definir mensajes genéricos por tipo y mensajes específicos
    /// basados en una sola condición lógica.
    ///
    /// Cambio de localización: las listas de mensajes almacenan
    /// <see cref="LocalizedString"/> en lugar de <c>string</c> plano,
    /// de modo que los mensajes se resuelven al idioma activo en runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Narrative/Notification Library")]
    public sealed class NarrativeNotificationLibrary : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Mensajes posibles cuando se desbloquea un fragmento de memoria.")]
        private List<LocalizedString> fragmentUnlockedMessages = new();

        [SerializeField]
        [Tooltip("Mensajes posibles cuando un objeto dentro de una memoria se vuelve más claro o se desbloquea.")]
        private List<LocalizedString> objectUnlockedMessages = new();

        [SerializeField]
        [Tooltip("Mensajes posibles cuando un nuevo estado del objeto queda disponible para interactuar.")]
        private List<LocalizedString> stateAvailableMessages = new();

        [SerializeField]
        [Tooltip("Mensajes específicos con prioridad sobre los mensajes genéricos.")]
        private List<NarrativeSpecificMessageByCondition> specificMessages = new();

        /// <summary>
        /// Devuelve las referencias de localización configuradas para un tipo de notificación.
        /// </summary>
        public IReadOnlyList<LocalizedString> GetMessages(NarrativeNotificationType type)
        {
            return type switch
            {
                NarrativeNotificationType.FragmentUnlocked => fragmentUnlockedMessages,
                NarrativeNotificationType.ObjectUnlocked   => objectUnlockedMessages,
                NarrativeNotificationType.StateAvailable   => stateAvailableMessages,
                _                                          => Array.Empty<LocalizedString>()
            };
        }

        /// <summary>
        /// Intenta obtener la referencia de localización de un mensaje específico por condición.
        /// </summary>
        public bool TryGetSpecificMessage(
            UnlockNotificationMessage notification,
            RuntimeContext runtimeContext,
            out LocalizedString resolvedLocalizedString)
        {
            resolvedLocalizedString = null;

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
                    resolvedLocalizedString = entry.LocalizedMessage;
                    return true;
                }
            }

            return false;
        }
    }
}