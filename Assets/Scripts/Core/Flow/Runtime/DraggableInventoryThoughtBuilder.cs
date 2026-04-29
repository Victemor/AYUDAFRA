using UnityEngine.Localization;

namespace Game.Runtime
{
    /// <summary>
    /// Generador centralizado de referencias de localización para mensajes
    /// de feedback del sistema draggable.
    ///
    /// Cada método devuelve un <see cref="LocalizedString"/> que apunta a una
    /// entrada de la tabla de localización. Las claves deben existir en la tabla
    /// indicada por <c>TableName</c>.
    ///
    /// Claves requeridas en la tabla:
    /// - draggable_inventory_full
    /// - draggable_invalid_placement
    /// - draggable_occupied_slot
    /// - draggable_occupied_wrong_object
    /// - draggable_finalized_item_locked
    /// </summary>
    public static class DraggableInventoryThoughtBuilder
    {
        private const string TableName = "Tabla 1";

        public static LocalizedString BuildInventoryFull()
            => new LocalizedString(TableName, "draggable_inventory_full");

        public static LocalizedString BuildInvalidPlacement()
            => new LocalizedString(TableName, "draggable_invalid_placement");

        public static LocalizedString BuildOccupiedSlot()
            => new LocalizedString(TableName, "draggable_occupied_slot");

        public static LocalizedString BuildOccupiedWrongObject()
            => new LocalizedString(TableName, "draggable_occupied_wrong_object");

        public static LocalizedString BuildFinalizedItemLocked()
            => new LocalizedString(TableName, "draggable_finalized_item_locked");
    }
}