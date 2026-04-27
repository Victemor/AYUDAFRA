using Game;
using Game.Data;
using Game.Runtime;

public sealed class FragmentActionExecutionContext
{
    /// <summary>
    /// Contexto runtime global del sistema narrativo.
    /// </summary>
    public RuntimeContext RuntimeContext { get; }

    /// <summary>
    /// Controlador raíz del sistema de acciones.
    /// </summary>
    public ActionBehaviorController Controller { get; }

    /// <summary>
    /// Memoria base asociada al ejecutor actual.
    /// </summary>
    public MemoryDefinition MemoryDefinition { get; }

    /// <summary>
    /// Runtime de la memoria actual.
    /// </summary>
    public MemoryRuntimeData MemoryRuntime { get; }

    public FragmentActionExecutionContext(
        RuntimeContext runtimeContext,
        ActionBehaviorController controller,
        MemoryDefinition memoryDefinition,
        MemoryRuntimeData memoryRuntime)
    {
        RuntimeContext = runtimeContext;
        Controller = controller;
        MemoryDefinition = memoryDefinition;
        MemoryRuntime = memoryRuntime;
    }
}