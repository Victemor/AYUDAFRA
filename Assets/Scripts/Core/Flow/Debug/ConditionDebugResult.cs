using System.Collections.Generic;

namespace Game.Conditions
{
    /// <summary>
    /// Resultado de evaluación de una condición individual.
    /// </summary>
    public readonly struct ConditionDebugResult
    {
        /// <summary>
        /// Descripción legible de la condición evaluada.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Resultado booleano de la condición.
        /// </summary>
        public bool Result { get; }

        public ConditionDebugResult(string description, bool result)
        {
            Description = description;
            Result = result;
        }
    }

    /// <summary>
    /// Resultado de un grupo de condiciones.
    /// </summary>
    public sealed class ConditionGroupDebugResult
    {
        private readonly List<ConditionDebugResult> results = new();
        private bool groupResult;

        /// <summary>
        /// Resultados individuales del grupo.
        /// </summary>
        public IReadOnlyList<ConditionDebugResult> Results => results;

        /// <summary>
        /// Resultado agregado del grupo.
        /// </summary>
        public bool GroupResult => groupResult;

        public void Add(Condition condition, bool result)
        {
            results.Add(new ConditionDebugResult(
                condition != null ? condition.GetDescription() : "NULL CONDITION",
                result));
        }

        public void SetGroupResult(bool value)
        {
            groupResult = value;
        }
    }
}