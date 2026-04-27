using Game.Data;
using Game.Runtime;

namespace Game.Conditions
{
    /// <summary>
    /// Representa un objetivo runtime que debe reevaluarse cuando una dependencia cambia.
    /// </summary>
    public sealed class ConditionDependencyTarget
    {
        public enum DependencyTargetType
        {
            MemoryUnlock,
            ObjectTransition
        }

        /// <summary>
        /// Tipo de objetivo a reevaluar.
        /// </summary>
        public DependencyTargetType TargetType { get; }

        /// <summary>
        /// Memoria objetivo.
        /// </summary>
        public MemoryDefinition Memory { get; }

        /// <summary>
        /// Objeto objetivo cuando aplica.
        /// </summary>
        public ObjectDefinition Object { get; }

        /// <summary>
        /// Clave única del objetivo para evitar duplicados.
        /// </summary>
        public string Key { get; }

        public ConditionDependencyTarget(DependencyTargetType targetType, MemoryDefinition memory, ObjectDefinition obj)
        {
            TargetType = targetType;
            Memory = memory;
            Object = obj;

            Key = targetType == DependencyTargetType.MemoryUnlock
                ? $"MEMORY::{memory?.Id}"
                : $"OBJECT::{memory?.Id}::{obj?.Id}";
        }

        /// <summary>
        /// Ejecuta la reevaluación del objetivo contra el runtime actual.
        /// </summary>
        public void Evaluate(RuntimeContext context)
        {
            if (context == null || context.Repository == null || Memory == null)
                return;

            MemoryRuntimeData memoryRuntime = context.Repository.GetMemory(Memory);
            if (memoryRuntime == null)
                return;

            switch (TargetType)
            {
                case DependencyTargetType.MemoryUnlock:
                    memoryRuntime.EvaluateUnlock(context);
                    break;

                case DependencyTargetType.ObjectTransition:
                    if (Object == null)
                        return;

                    ObjectRuntimeData objectRuntime = memoryRuntime.GetObject(Object);
                    if (objectRuntime == null)
                        return;

                    objectRuntime.Evaluate(context);
                    break;
            }
        }

        public override string ToString()
        {
            return Key;
        }
    }
}