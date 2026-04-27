using UnityEngine;
using Game.Data;
using Game.Runtime;

namespace Game.Conditions
{
    /// <summary>
    /// Evalúa la intensidad emocional del estado actual de un objeto.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Conditions/Object State Intensity")]
    public class ObjectStateIntensityCondition : Condition
    {
        [Tooltip("Memoria donde se encuentra el objeto.")]
        [SerializeField] private MemoryDefinition memory;

        [Tooltip("Objeto a evaluar.")]
        [SerializeField] private ObjectDefinition targetObject;

        [Tooltip("Intensidad requerida.")]
        [SerializeField] private IntensityLevel requiredIntensity;

        /// <summary>
        /// Memoria observada por la condición.
        /// </summary>
        public MemoryDefinition Memory => memory;

        /// <summary>
        /// Objeto observado por la condición.
        /// </summary>
        public ObjectDefinition TargetObject => targetObject;

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
            if (!objectRuntime.IsValidStateIndex(index))
                return false;

            RuntimeEmotionState emotionState = objectRuntime.GetEmotionForState(index);
            return emotionState.Intensity == requiredIntensity;
        }

        public override string GetDescription()
        {
            string objectName = targetObject != null ? targetObject.name : "NULL";
            return $"Object [{objectName}] Intensity == {requiredIntensity}";
        }
    }
}