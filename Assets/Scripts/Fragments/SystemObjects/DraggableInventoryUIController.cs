using System.Collections.Generic;
using UnityEngine;
using Game.Runtime;

namespace Game.UI.Inventory
{
    /// <summary>
    /// Controlador visual de la UI del inventario draggable.
    ///
    /// El primer Refresh() se difiere hasta Start() para garantizar que
    /// ScenePersistenceController (orden -100000) ya haya cargado el save antes de
    /// renderizar los slots. Esto evita los cuadros blancos que aparecían porque
    /// OnEnable() se ejecuta antes de que cualquier Start() corra, cuando los datos
    /// del inventario aún no están restaurados.
    /// </summary>
    public sealed class DraggableInventoryUIController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Slots visuales del inventario en orden fijo.")]
        private List<DraggableInventorySlotUI> slots = new();

        [SerializeField]
        [Tooltip("Activa logs de depuración del controlador UI.")]
        private bool debugLogs = false;

        /// <summary>
        /// True a partir del primer Start(). Impide que OnEnable() refresque antes
        /// de que el save esté cargado.
        /// </summary>
        private bool isInitialized;

        #region Unity Messages

        private void OnEnable()
        {
            GameEvents.OnDraggableInventoryChanged += Refresh;

            // Si ya pasamos por Start(), el save está cargado y el refresh es seguro.
            // Si aún no, Start() se encargará del primer refresh.
            if (isInitialized)
            {
                Refresh();
            }
        }

        private void Start()
        {
            isInitialized = true;
            Refresh();
        }

        private void OnDisable()
        {
            GameEvents.OnDraggableInventoryChanged -= Refresh;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Refresca el contenido visual completo del inventario.
        /// </summary>
        public void Refresh()
        {
            if (DraggableInventorySystem.Instance == null || DraggableInventorySystem.Instance.Inventory == null)
            {
                Log("Refresh aborted -> InventorySystem o Inventory null.");
                return;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null)
                {
                    continue;
                }

                slots[i].Refresh();

                Log(
                    $"Refresh -> UI Slot Object: {slots[i].name} | " +
                    $"Runtime SlotIndex: {slots[i].SlotIndex}"
                );
            }
        }

        #endregion

        #region Private

        private void Log(string message)
        {
            if (!debugLogs)
            {
                return;
            }

            Debug.Log($"[DraggableInventoryUIController] {message}", this);
        }

        #endregion
    }
}