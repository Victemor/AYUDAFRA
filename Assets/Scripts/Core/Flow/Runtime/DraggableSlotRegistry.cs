using System.Collections.Generic;
using UnityEngine;
using Game.Data;

namespace Game.Runtime
{
    /// <summary>
    /// Registro global runtime de slots físicos de fragmentos para items draggable.
    /// </summary>
    public sealed class DraggableSlotRegistry : MonoBehaviour
    {
        public static DraggableSlotRegistry Instance { get; private set; }

        [SerializeField]
        [Tooltip("Activa logs de depuración del registro de slots.")]
        private bool debugLogs = false;

        private readonly Dictionary<string, FragmentDraggableSlotRuntimeData> slotsById = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Obtiene un slot por ID o null si no existe.
        /// </summary>
        public FragmentDraggableSlotRuntimeData GetSlot(string slotId)
        {
            if (string.IsNullOrWhiteSpace(slotId))
            {
                return null;
            }

            slotsById.TryGetValue(slotId, out FragmentDraggableSlotRuntimeData runtimeSlot);
            return runtimeSlot;
        }

        /// <summary>
        /// Obtiene un slot existente o lo crea si no existe.
        /// </summary>
        public FragmentDraggableSlotRuntimeData GetOrCreateSlot(
            string slotId,
            string fragmentId,
            IReadOnlyList<DraggableItemDefinition> allowedItems)
        {
            if (string.IsNullOrWhiteSpace(slotId))
            {
                return null;
            }

            if (slotsById.TryGetValue(slotId, out FragmentDraggableSlotRuntimeData existing))
            {
                existing.UpdateAllowedItems(allowedItems);
                return existing;
            }

            FragmentDraggableSlotRuntimeData created = new FragmentDraggableSlotRuntimeData(
                slotId,
                fragmentId,
                allowedItems);

            slotsById.Add(slotId, created);

            Log($"GetOrCreateSlot -> Created {slotId} | Fragment: {fragmentId}");
            return created;
        }

        /// <summary>
        /// Devuelve todos los slots registrados.
        /// </summary>
        public IEnumerable<FragmentDraggableSlotRuntimeData> GetAllSlots()
        {
            return slotsById.Values;
        }

        /// <summary>
        /// Limpia el registro completo.
        /// </summary>
        public void Clear()
        {
            slotsById.Clear();
            Log("Clear");
        }

        private void Log(string message)
        {
            if (!debugLogs)
            {
                return;
            }

            Debug.Log($"[DraggableSlotRegistry] {message}", this);
        }
    }
}