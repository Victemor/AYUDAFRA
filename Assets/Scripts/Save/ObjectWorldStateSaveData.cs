using System;

namespace Game.Save
{
    /// <summary>
    /// Representa el estado persistente de un objeto del mundo.
    /// Permite restaurar visibilidad, colisiones y otros flags.
    /// </summary>
    [Serializable]
    public class ObjectWorldStateSaveData
    {
        public string id;
    
        public bool hasVisible;
        public bool visible;

        public bool hasColliderEnabled;
        public bool colliderEnabled;
    }
}