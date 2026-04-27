using UnityEngine;
using Game.Runtime;
using Game.Data;
using Game;

namespace Game.Runtime
{
    /// <summary>
    /// Contenedor runtime de emoción aplicada a un objeto.
    /// Se separa del data estático para permitir variabilidad en tiempo real.
    /// </summary>
    public struct RuntimeEmotionState
    {
        public EmotionType Emotion;
        public IntensityLevel Intensity;

        public RuntimeEmotionState(EmotionType emotion, IntensityLevel intensity)
        {
            Emotion = emotion;
            Intensity = intensity;
        }
    }
}