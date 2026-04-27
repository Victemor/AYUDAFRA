using UnityEngine;
using Game.Data;
using Game.Runtime;

namespace Game.Conditions
{
    /// <summary>
    /// Evalúa si un objeto tiene una emoción específica en su estado actual.
    /// Si se define un StateId requerido, también valida que el estado actual coincida.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Conditions/Object State Emotion")]
    public class ObjectStateEmotionCondition : Condition
    {
        [Tooltip("Memoria donde se encuentra el objeto.")]
        [SerializeField] private MemoryDefinition memory;

        [Tooltip("Objeto a evaluar.")]
        [SerializeField] private ObjectDefinition targetObject;

        [Tooltip("Emoción requerida en el estado actual.")]
        [SerializeField] private EmotionTypeGlobal requiredEmotion;

        [Tooltip("Opcional. Si se define, exige que el estado actual coincida con este StateId.")]
        [SerializeField] private string requiredStateId;

        /// <summary>
        /// Memoria objetivo serializada.
        /// </summary>
        public MemoryDefinition Memory => memory;

        /// <summary>
        /// Objeto objetivo serializado.
        /// </summary>
        public ObjectDefinition TargetObject => targetObject;

        /// <summary>
        /// StateId opcional requerido.
        /// </summary>
        public string RequiredStateId
        {
            get => requiredStateId;
            set => requiredStateId = value;
        }

        public override bool Evaluate(RuntimeContext context)
        {
            if (context == null || context.Repository == null)
                return false;

            if (memory == null || targetObject == null)
                return false;

            MemoryRuntimeData memoryRuntime = context.Repository.GetMemory(memory);
            if (memoryRuntime == null)
                return false;

            ObjectRuntimeData objectRuntime = memoryRuntime.GetObject(targetObject);
            if (objectRuntime == null)
                return false;

            int index = objectRuntime.GetStateIndex();
            if (!objectRuntime.TryGetStateDefinition(index, out ObjectStateDefinition currentStateDefinition))
                return false;

            if (!string.IsNullOrEmpty(requiredStateId) && currentStateDefinition.StateId != requiredStateId)
                return false;

            RuntimeEmotionState emotionState = objectRuntime.GetEmotionForState(index);
            return emotionState.Emotion == (Game.Data.EmotionType)requiredEmotion;
        }

        public override string GetDescription()
        {
            string objectName = targetObject != null ? targetObject.name : "NULL";
            return $"Object [{objectName}] State[{requiredStateId}] Emotion == {requiredEmotion}";
        }
    }
}