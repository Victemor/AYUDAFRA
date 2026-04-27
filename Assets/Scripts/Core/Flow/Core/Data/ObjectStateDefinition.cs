using System.Collections.Generic;
using UnityEngine;
using Game.Conditions;

namespace Game.Data
{
    /// <summary>
    /// Define un estado específico de un objeto interactivo.
    /// </summary>
    [System.Serializable]
    public class ObjectStateDefinition
    {
        [Header("Identification")]

        [Tooltip("ID único del estado dentro del objeto.")]
        [SerializeField] private string stateId;

        [Header("Narrative")]

        [Tooltip("Emoción base asociada a este estado.")]
        [SerializeField] private EmotionType emotion;

        [Tooltip("Intensidad base asociada a este estado.")]
        [SerializeField] private IntensityLevel intensity;

        [Header("Conditions")]

        [Tooltip("Condiciones necesarias para transicionar a este estado.")]
        [SerializeField] private List<ConditionGroup> transitionConditions = new();

        [Header("Config")]

        [Tooltip("Indica si este estado puede volver a consumirse múltiples veces.")]
        [SerializeField] private bool allowReplay = true;

        /// <summary>
        /// Identificador único del estado.
        /// </summary>
        public string StateId => stateId;

        /// <summary>
        /// Emoción base del estado.
        /// </summary>
        public EmotionType Emotion => emotion;

        /// <summary>
        /// Intensidad base del estado.
        /// </summary>
        public IntensityLevel Intensity => intensity;

        /// <summary>
        /// Condiciones de transición hacia este estado.
        /// </summary>
        public IReadOnlyList<ConditionGroup> TransitionConditions => transitionConditions;

        /// <summary>
        /// Indica si el estado permite replay.
        /// </summary>
        public bool AllowReplay => allowReplay;
    }
}