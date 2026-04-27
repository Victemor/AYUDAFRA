using UnityEngine;
using Game.Data;
using Game.Runtime;

namespace Game.Conditions
{
    /// <summary>
    /// Evalúa si un objeto específico está en un estado determinado.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Conditions/Object State")]
    public class ObjectStateCondition : Condition
    {
        [Tooltip("Memoria donde se encuentra el objeto.")]
        [SerializeField] private MemoryDefinition memory;

        [Tooltip("Objeto a evaluar.")]
        [SerializeField] private ObjectDefinition targetObject;

        [Tooltip("ID del estado requerido.")]
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
        /// ID de estado requerido serializado.
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

            if (memory == null || targetObject == null || string.IsNullOrEmpty(requiredStateId))
                return false;

            MemoryRuntimeData memoryRuntime = context.Repository.GetMemory(memory);
            if (memoryRuntime == null)
                return false;

            ObjectRuntimeData objectRuntime = memoryRuntime.GetObject(targetObject);
            if (objectRuntime == null)
                return false;

            return objectRuntime.CurrentStateId == requiredStateId;
        }

        public override string GetDescription()
        {
            string objectName = targetObject != null ? targetObject.name : "NULL";
            return $"Object [{objectName}] State == {requiredStateId}";
        }
    }
}