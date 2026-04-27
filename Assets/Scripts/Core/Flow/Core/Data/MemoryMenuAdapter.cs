using System.Collections.Generic;
using UnityEngine;
using Game.Data;
using Game.Runtime;

public class MemoryMenuAdapter : MonoBehaviour
{
    [SerializeField] private MemoryDatabase database;

    /// <summary>
    /// Convierte el estado del sistema de memorias en DropData para el menú.
    /// </summary>
    public List<DropData> BuildMenuData()
    {
        var result = new List<DropData>();

        var repo = GameStateRepository.Instance;

        foreach (var memoryDef in database.Memories)
        {
            var runtime = repo.GetMemory(memoryDef);

            // 🔴 FILTRO: BLOQUEADO NO EXISTE EN MENÚ
            if (runtime.CurrentState == MemoryState.Locked)
                continue;

            var drop = GetOrCreateDrop(memoryDef.Id);

            // ✔ Estado visual
            drop.WasVisited = runtime.CurrentState >= MemoryState.Seen;

            // ✔ Conexiones (IMPORTANTE)
            SyncConnections(memoryDef, drop);

            result.Add(drop);
        }

        return result;
    }

    private DropData GetOrCreateDrop(string id)
    {
        var progress = GameManager.Instance.FragmentProgress;

        var existing = progress.drops.Find(d => d.FragmentName == id);

        if (existing != null)
            return existing;

        var newDrop = new DropData(id, Vector2.zero);
        progress.drops.Add(newDrop);

        return newDrop;
    }

    private void SyncConnections(MemoryDefinition memory, DropData drop)
    {
        // 🔥 AQUÍ defines la conexión desde TU sistema
        // (ver siguiente sección)
    }
}