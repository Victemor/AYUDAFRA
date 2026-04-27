using Game.Data;
using System;
using Game.Runtime;

namespace Game.Runtime
{
    /// <summary>
    /// Sistema de eventos global para desacoplar lógica y UI.
    /// </summary>
    public static class GameEvents
    {
        public static event System.Action<MemoryDefinition> OnMemoryUnlocked;
        public static event System.Action<MemoryDefinition> OnMemoryAlert;

        public static event System.Action<ObjectRuntimeData> OnObjectStateChanged;
        public static event System.Action<MemoryRuntimeData> OnMemoryStateChanged;

        public static event System.Action<DraggableItemRuntimeData> OnDraggableItemStateChanged;
        public static event System.Action<FragmentDraggableSlotRuntimeData> OnFragmentDraggableSlotStateChanged;
        public static event System.Action OnDraggableInventoryChanged;

        /// <summary>Señal genérica de acción del jugador.</summary>
        public static event System.Action OnPlayerAction;

        /// <summary>Se dispara cuando se crea o elimina una conexión entre dos memorias.</summary>
        public static event System.Action<string, string> OnMemoryConnectionChanged;

        /// <summary>Se dispara cuando el jugador confirma el renombre de un fragmento.</summary>
        public static event System.Action OnFragmentRenamed;

        /// <summary>Se dispara cuando el jugador empieza a arrastrar un drop del menú.</summary>
        public static event System.Action OnDropMoved;

        /// <summary>
        /// Se dispara cuando el jugador hace clic derecho sobre un drop del menú.
        /// </summary>
        public static event System.Action OnDropRightClicked;

        /// <summary>
        /// Se dispara cuando el jugador hace clic en un objeto interactuable del mundo con éxito.
        /// </summary>
        public static event System.Action OnInteractableClicked;

        public static void RaiseMemoryUnlocked(MemoryDefinition memory) => OnMemoryUnlocked?.Invoke(memory);
        public static void RaiseMemoryAlert(MemoryDefinition memory) => OnMemoryAlert?.Invoke(memory);
        public static void RaiseObjectStateChanged(ObjectRuntimeData runtimeObject) => OnObjectStateChanged?.Invoke(runtimeObject);
        public static void RaiseMemoryStateChanged(MemoryRuntimeData runtimeMemory) => OnMemoryStateChanged?.Invoke(runtimeMemory);
        public static void RaisePlayerAction() => OnPlayerAction?.Invoke();
        public static void RaiseMemoryConnectionChanged(string memoryIdA, string memoryIdB) => OnMemoryConnectionChanged?.Invoke(memoryIdA, memoryIdB);
        public static void RaiseFragmentRenamed() => OnFragmentRenamed?.Invoke();
        public static void RaiseDropMoved() => OnDropMoved?.Invoke();

        /// <summary>
        /// Notifica que el jugador hizo clic derecho sobre un drop del menú.
        /// Llamado desde DropController.OnMouseOver() con GetMouseButtonDown(1).
        /// </summary>
        public static void RaiseDropRightClicked() => OnDropRightClicked?.Invoke();

        /// <summary>
        /// Notifica que el jugador hizo clic en un objeto interactuable con éxito.
        /// </summary>
        public static void RaiseInteractableClicked() => OnInteractableClicked?.Invoke();

        /// <summary>Se dispara cuando una entidad narrativa se desbloquea.</summary>
        public static event Action<UnlockNotificationMessage> OnUnlockNotification;
        public static void RaiseUnlockNotification(UnlockNotificationMessage message) => OnUnlockNotification?.Invoke(message);

        public static void RaiseDraggableItemStateChanged(DraggableItemRuntimeData item) => OnDraggableItemStateChanged?.Invoke(item);
        public static void RaiseFragmentDraggableSlotStateChanged(FragmentDraggableSlotRuntimeData slot) => OnFragmentDraggableSlotStateChanged?.Invoke(slot);
        public static void RaiseDraggableInventoryChanged() => OnDraggableInventoryChanged?.Invoke();
    }
}