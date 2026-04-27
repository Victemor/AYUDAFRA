namespace Game.Data
{
    /// <summary>
    /// Estado runtime interno de un objeto draggable.
    /// 
    /// Nota:
    /// - A nivel de diseño el objeto solo tiene dos estados conceptuales: Movible o Final.
    /// - Internamente se usa un estado más granular para robustez de save/load y flujo.
    /// </summary>
    public enum DraggableItemState
    {
        NotSpawned = 0,
        InWorld = 1,
        InInventory = 2,
        Held = 3,
        InFragmentSlot = 4,
        Finalized = 5
    }

}