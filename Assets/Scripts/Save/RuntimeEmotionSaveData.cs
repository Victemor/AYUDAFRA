using System;

namespace Game.Save
{
    /// <summary>
    /// Representa la emoción runtime persistida de un estado de objeto.
    /// </summary>
    [Serializable]
    public class RuntimeEmotionSaveData
    {
        public int stateIndex;
        public int emotion;
        public int intensity;
    }
}