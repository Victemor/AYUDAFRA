using System.Collections.Generic;

namespace Game.Conditions
{
    /// <summary>
    /// Resultado completo de una evaluación de condiciones.
    /// Contiene el resultado final y el detalle por grupos.
    /// </summary>
    public sealed class ConditionEvaluationSnapshot
    {
        /// <summary>
        /// Resultado final agregado de la evaluación.
        /// </summary>
        public bool Result { get; }

        /// <summary>
        /// Detalle de evaluación por grupos.
        /// </summary>
        public IReadOnlyList<ConditionGroupDebugResult> Groups { get; }

        public ConditionEvaluationSnapshot(bool result, List<ConditionGroupDebugResult> groups)
        {
            Result = result;
            Groups = groups ?? new List<ConditionGroupDebugResult>();
        }
    }
}