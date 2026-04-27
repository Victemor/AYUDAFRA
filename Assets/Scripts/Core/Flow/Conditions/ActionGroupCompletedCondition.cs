using UnityEngine;
using Game.Data;
using Game.Runtime;

namespace Game.Conditions
{
    /// <summary>
    /// Evalúa si ya se ejecutó el grupo de acciones asociado
    /// a un estado específico de un objeto dentro de una memoria.
    /// 
    /// El identificador del grupo se resuelve mediante el StateId
    /// seleccionado del objeto objetivo.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Conditions/Action Group Completed")]
    public sealed class ActionGroupCompletedCondition : Condition
    {
        [Header("Target")]

        [Tooltip("Memoria donde vive el objeto cuyo grupo de acciones será evaluado.")]
        [SerializeField] private MemoryDefinition targetMemory;

        [Tooltip("Objeto que contiene el grupo de acciones a consultar.")]
        [SerializeField] private ObjectDefinition targetObject;

        [Tooltip("StateId del objeto que también funciona como id del grupo de acciones.")]
        [SerializeField] private string targetActionGroupId;

        /// <summary>
        /// Memoria objetivo de la condición.
        /// </summary>
        public MemoryDefinition TargetMemory => targetMemory;

        /// <summary>
        /// Objeto objetivo de la condición.
        /// </summary>
        public ObjectDefinition TargetObject => targetObject;

        /// <summary>
        /// Identificador técnico del grupo a consultar.
        /// </summary>
        public string TargetActionGroupId => targetActionGroupId;

        /// <inheritdoc />
        public override bool Evaluate(RuntimeContext context)
        {
            if (context == null || context.Repository == null)
            {
                return false;
            }

            if (targetMemory == null || targetObject == null || string.IsNullOrWhiteSpace(targetActionGroupId))
            {
                return false;
            }

            MemoryRuntimeData memoryRuntime = context.Repository.GetMemory(targetMemory);
            if (memoryRuntime == null)
            {
                return false;
            }

            ObjectRuntimeData objectRuntime = memoryRuntime.GetObject(targetObject);
            if (objectRuntime == null)
            {
                return false;
            }

            ActRuntimeData actRuntime = objectRuntime.GetAct(targetActionGroupId);
            if (actRuntime == null)
            {
                return false;
            }

            bool result = actRuntime.hasExecuted;

           // Game.Debugging.ConditionDebugController.Instance?
             //   .LogConditionResult(GetDescription(), result);

            return result;
        }

        /// <inheritdoc />
        public override string GetDescription()
        {
            string memoryName = targetMemory != null ? targetMemory.name : "NULL";
            string objectName = targetObject != null ? targetObject.name : "NULL";
            string groupId = string.IsNullOrWhiteSpace(targetActionGroupId) ? "NULL" : targetActionGroupId;

            return $"ActionGroupCompleted | Memory: [{memoryName}] | Object: [{objectName}] | Group: [{groupId}]";
        }
    }
}