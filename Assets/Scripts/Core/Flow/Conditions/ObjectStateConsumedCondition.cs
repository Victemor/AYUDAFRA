using UnityEngine;
using Game.Data;
using Game.Runtime;

namespace Game.Conditions
{
    /// <summary>
    /// Condición que valida si un objeto ya consumió un estado específico.
    /// Usa el historial del objeto, no su estado actual.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Conditions/Object State Consumed")]
    public class ObjectStateConsumedCondition : Condition
    {
        [Tooltip("Memoria que contiene el objeto.")]
        [SerializeField] private MemoryDefinition targetMemory;

        [Tooltip("Objeto a evaluar.")]
        [SerializeField] private ObjectDefinition targetObject;

        [Tooltip("ID del estado que debió ser consumido.")]
        [SerializeField] private string stateId;

        /// <summary>
        /// Memoria objetivo serializada.
        /// </summary>
        public MemoryDefinition TargetMemory => targetMemory;

        /// <summary>
        /// Objeto objetivo serializado.
        /// </summary>
        public ObjectDefinition TargetObject => targetObject;

        /// <summary>
        /// ID de estado consultado.
        /// </summary>
        public string StateId
        {
            get => stateId;
            set => stateId = value;
        }

        public override bool Evaluate(RuntimeContext context)
        {
            if (context == null || context.Repository == null)
                return false;

            if (targetMemory == null || targetObject == null || string.IsNullOrEmpty(stateId))
                return false;

            MemoryRuntimeData memoryRuntime = context.Repository.GetMemory(targetMemory);
            if (memoryRuntime == null)
                return false;

            ObjectRuntimeData objectRuntime = memoryRuntime.GetObject(targetObject);
            if (objectRuntime == null)
                return false;

            return objectRuntime.HasConsumedState(stateId);
        }

        public override string GetDescription()
        {
            string memoryName = targetMemory != null ? targetMemory.name : "NULL";
            string objectName = targetObject != null ? targetObject.name : "NULL";

            return $"[ConsumedCondition] Object '{objectName}' in Memory '{memoryName}' consumed state '{stateId}'";
        }
    }
}