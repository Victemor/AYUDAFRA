using System;
using UnityEngine;
using Game.Conditions;
using Game.Data;

namespace Game.Runtime
{
    /// <summary>
    /// Regla de mensaje específico basada en una única condición lógica.
    /// 
    /// Implementación:
    /// - Usa un solo ConditionGroup serializado.
    /// - Por convención de diseño, ese grupo debe contener una sola condición.
    /// - Si el grupo se cumple, este mensaje tiene prioridad sobre los genéricos.
    /// </summary>
    [Serializable]
    public sealed class NarrativeSpecificMessageByCondition
    {
        [SerializeField]
        [Tooltip("Tipo de notificación al que aplica esta regla.")]
        private NarrativeNotificationType notificationType;

        [SerializeField]
        [Tooltip("Grupo de condición que habilita este mensaje específico. Debe contener una sola condición.")]
        private ConditionGroup conditionGroup;

        [SerializeField]
        [TextArea]
        [Tooltip("Mensaje forzado que se enviará cuando la condición se cumpla.")]
        private string message;

        public NarrativeNotificationType NotificationType => notificationType;
        public ConditionGroup ConditionGroup => conditionGroup;
        public string Message => message;

        /// <summary>
        /// Devuelve true si esta regla aplica a la notificación actual
        /// y su condición configurada se cumple.
        /// </summary>
        public bool Matches(
            UnlockNotificationMessage notification,
            RuntimeContext runtimeContext)
        {
            if (notification.NotificationType != notificationType)
            {
                return false;
            }

            if (conditionGroup == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            return conditionGroup.Evaluate(runtimeContext, out _);
        }
    }
}