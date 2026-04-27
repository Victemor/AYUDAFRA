using UnityEngine;

/// <summary>
/// Configuración completa de una emoción.
/// Define parámetros visuales y ambientales basados en interpolación.
/// </summary>
[CreateAssetMenu(menuName = "Game/Emotion/Emotion Data")]
public sealed class EmotionData : ScriptableObject
{
    #region Serialized Fields

    [Header("Identidad")]

    [SerializeField] private EmotionTypeGlobal emotionType;
    [SerializeField] private Color color = Color.white;

    [Header("Bloom")]

    [SerializeField]
    private FloatRange bloomIntensity;

    [Header("Clima")]

    [SerializeField]
    private IntRange rain;

    [SerializeField]
    private IntRange wind;

    [SerializeField]
    private FloatRange fire;

    [Header("Sway Cámara")]

    [SerializeField]
    private SwaySettings sway;

    #endregion

    #region Public Properties

    public EmotionTypeGlobal EmotionType => emotionType;
    public Color Color => color;

    #endregion

    #region Public API (BASED ON t)

    public float GetBloomIntensity(float t) => bloomIntensity.Evaluate(t);
    public int GetRain(float t) => rain.Evaluate(t);
    public int GetWind(float t) => wind.Evaluate(t);
    public float GetFire(float t) => fire.Evaluate(t);

    public (float x, float z, float speed) GetSway(float t)
    {
        return sway.Evaluate(t);
    }

    #endregion
}