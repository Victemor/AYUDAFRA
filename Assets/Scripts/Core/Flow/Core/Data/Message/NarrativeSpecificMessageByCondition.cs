using System;
using UnityEngine;
using UnityEngine.Localization;
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
    ///
    /// Cambio de localización: el campo <c>message</c> (string plano) fue
    /// reemplazado por <c>localizedMessage</c> (<see cref="LocalizedString"/>)
    /// para que el texto se traduzca al idioma activo en runtime.
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
        [Tooltip("Clave de localización del mensaje forzado. Selecciona tabla y clave desde la tabla de localización.")]
        private LocalizedString localizedMessage;

        public NarrativeNotificationType NotificationType => notificationType;
        public ConditionGroup ConditionGroup => conditionGroup;

        /// <summary>
        /// Referencia de localización del mensaje específico.
        /// Resuélvela con <c>GetLocalizedStringAsync()</c> antes de mostrarla.
        /// </summary>
        public LocalizedString LocalizedMessage => localizedMessage;

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

            if (localizedMessage == null || localizedMessage.IsEmpty)
            {
                return false;
            }

            return conditionGroup.Evaluate(runtimeContext, out _);
        }
    }
}