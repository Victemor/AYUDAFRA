using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// UI del slider emocional. Solo representa y emite valores.
/// </summary>
public sealed class EmotionSliderUI : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text leftEmotionText;
    [SerializeField] private TMP_Text rightEmotionText;

    public event Action<float> OnValueChanged;

    private void Awake()
    {
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0.5f;

        slider.onValueChanged.AddListener(value =>
        {
            OnValueChanged?.Invoke(value);
        });
    }

    /// <summary>
    /// Solo actualiza labels visuales.
    /// </summary>
    public void SetLabels(EmotionData left, EmotionData right)
    {
        if (leftEmotionText != null)
        {
            leftEmotionText.text = left.name;
            //leftEmotionText.color = left.Color;
        }

        if (rightEmotionText != null)
        {
            rightEmotionText.text = right.name;
            //rightEmotionText.color = right.Color;
        }
    }
    /// <summary>
    /// Establece el valor del slider sin emitir eventos redundantes.
    /// </summary>
    public void SetValue(float value, bool notify = true)
    {
        if (slider == null) return;

        if (notify)
        {
            slider.value = value;
        }
        else
        {
            slider.SetValueWithoutNotify(value);
        }
    }
    /// <summary>
    /// Devuelve el valor actual del slider.
    /// </summary>
    public float GetValue()
    {
        return slider != null ? slider.value : 0.5f;
    }
}