using UnityEngine;
using Game.Runtime;
using Game.Data;
using Game;

/// <summary>
/// Maneja interacción con objetos de memoria mediante colisión.
/// Solo consume estados cuando el estado actual ya está habilitado.
/// </summary>
public class MemoryObjectInteractor : MonoBehaviour
{
    [SerializeField] private MemoryDefinition memory;
    [SerializeField] private ObjectDefinition objectDefinition;

    private RuntimeContext context;

    private void Start()
    {
        context = new RuntimeContext(GameStateRepository.Instance);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (context == null || context.Repository == null)
        {
            return;
        }

        MemoryRuntimeData memoryRuntime = context.Repository.GetMemory(memory);
        if (memoryRuntime == null)
        {
            return;
        }

        ObjectRuntimeData obj = memoryRuntime.GetObject(objectDefinition);
        if (obj == null)
        {
            return;
        }

        bool consumed = obj.ConsumeCurrentState(context);

        if (!consumed)
        {
            Debug.Log($"[MemoryObjectInteractor] Estado no habilitado o ya consumido → {obj.CurrentStateId}");
        }
    }
}