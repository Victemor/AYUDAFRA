using UnityEngine;

namespace Game.Conditions
{
    /// <summary>
    /// Clase base para todas las condiciones del sistema.
    /// Implementada como ScriptableObject para permitir reutilización y configuración desde el inspector.
    /// </summary>
    public abstract class Condition : ScriptableObject
    {
        /// <summary>
        /// Evalúa la condición en el contexto actual del juego.
        /// </summary>
        public abstract bool Evaluate(RuntimeContext context);

        /// <summary>
        /// Devuelve una descripción legible para debugging.
        /// </summary>
        public abstract string GetDescription();
    }
}