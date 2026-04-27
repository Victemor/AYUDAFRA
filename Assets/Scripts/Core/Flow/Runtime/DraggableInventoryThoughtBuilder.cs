namespace Game.Runtime
{
    /// <summary>
    /// Generador centralizado de mensajes fallback del sistema draggable.
    /// </summary>
    public static class DraggableInventoryThoughtBuilder
    {
        public static string BuildInventoryFull()
        {
            return "Siento que no puedo cargar con más objetos de mi memoria en este momento.";
        }

        public static string BuildInvalidPlacement()
        {
            return "No creo que este objeto tenga algo que hacer aquí.";
        }

        public static string BuildOccupiedSlot()
        {
            return "Ese espacio ya está ocupado. Primero tendría que retirar lo que hay allí.";
        }

        public static string BuildOccupiedWrongObject()
        {
            return "Ese lugar ya tiene algo que no parece corresponderle. Debería recoger primero ese objeto.";
        }

        public static string BuildFinalizedItemLocked()
        {
            return "Siento que este objeto ya encontró su lugar.";
        }
    }
}