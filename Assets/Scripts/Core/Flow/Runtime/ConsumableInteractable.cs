using UnityEngine;
using Game.Runtime;
using Game.Data;
using Game;

/// <summary>
/// Conecta un objeto del mundo con su representación en el sistema runtime.
/// Permite consumir estados mediante interacción explícita.
/// </summary>
public class ConsumableInteractable : MonoBehaviour, IInteractable
{
    [Header("Configuración Runtime")]

    [Tooltip("Memoria a la que pertenece el objeto.")]
    [SerializeField] private MemoryDefinition memory;

    [Tooltip("Definición del objeto en el sistema.")]
    [SerializeField] private ObjectDefinition objectDefinition;

    private ObjectRuntimeData runtimeObject;

    private void Start()
    {
        Initialize();
    }

    /// <summary>
    /// Inicializa la referencia al runtime del objeto.
    /// </summary>
    private void Initialize()
    {
        GameStateRepository repo = GameStateRepository.Instance;

        if (repo == null)
        {
            Debug.LogError("[ConsumableInteractable] GameStateRepository no encontrado.");
            return;
        }

        MemoryRuntimeData memoryRuntime = repo.GetMemory(memory);
        if (memoryRuntime == null)
        {
            Debug.LogError($"[ConsumableInteractable] MemoryRuntime NULL → {memory?.name}");
            return;
        }

        runtimeObject = memoryRuntime.GetObject(objectDefinition);

        if (runtimeObject == null)
        {
            Debug.LogError($"[ConsumableInteractable] ObjectRuntime NULL → {objectDefinition?.name}");
        }
    }

    public bool CanInteract()
    {
        return runtimeObject != null;
    }

    public void Interact()
    {
        if (runtimeObject == null)
        {
            Debug.LogError($"[ConsumableInteractable] runtimeObject NULL en {name}");
            return;
        }

        RuntimeContext context = new RuntimeContext(GameStateRepository.Instance);

        bool consumed = runtimeObject.ConsumeCurrentState(context);

        if (!consumed)
        {
            Debug.Log($"[ConsumableInteractable] Estado no habilitado o ya consumido → {runtimeObject.CurrentStateId}");
        }
        else
        {
            Debug.Log($"[ConsumableInteractable] Estado consumido correctamente → {runtimeObject.CurrentStateId}");
        }
    }
}