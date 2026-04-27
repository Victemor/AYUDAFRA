/// <summary>
/// Resultado de evaluación emocional basado en el slider.
/// </summary>
public readonly struct EmotionResult
{
    public readonly EmotionData Emotion;
    public readonly float Intensity;
    public readonly bool IsNeutral;

    public EmotionResult(EmotionData emotion, float intensity, bool isNeutral)
    {
        Emotion = emotion;
        Intensity = intensity;
        IsNeutral = isNeutral;
    }
}