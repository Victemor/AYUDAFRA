using System.Collections;
using UnityEngine;
using Game.Data;
using Game.Runtime;

public sealed class FragmentActionDraggableHandler
{
    public IEnumerator Execute(FragmentAction action, MonoBehaviour host)
    {
        switch (action.ActionType)
        {
            case FragmentActionType.SpawnFirstAvailableDraggableItem:
                ExecuteSpawnFirstAvailableDraggableItem(action, host);
                break;
        }

        yield break;
    }

    public bool CanHandle(FragmentActionType actionType)
    {
        return actionType == FragmentActionType.SpawnFirstAvailableDraggableItem;
    }

    private void ExecuteSpawnFirstAvailableDraggableItem(FragmentAction action, MonoBehaviour host)
    {
        if (DraggableInventorySystem.Instance == null)
        {
            Debug.LogWarning("[FragmentActionDraggableHandler] DraggableInventorySystem no encontrado.", host);
            return;
        }

        if (action.DraggableSpawnCandidates == null || action.DraggableSpawnCandidates.Count == 0)
        {
            Debug.LogWarning("[FragmentActionDraggableHandler] No hay candidatos draggable configurados.", host);
            return;
        }

        if (action.DraggableSpawnPoint == null)
        {
            Debug.LogWarning("[FragmentActionDraggableHandler] DraggableSpawnPoint no asignado.", host);
            return;
        }

        bool spawned = DraggableInventorySystem.Instance.TrySpawnFirstAvailableItem(
            action.DraggableSpawnCandidates,
            action.DraggableSpawnPoint);

        if (!spawned)
        {
            Debug.LogWarning(
                "[FragmentActionDraggableHandler] No fue posible spawnear ningún draggable de la lista. " +
                "Todos ya existen runtime o son inválidos.",
                host);
        }
    }
}