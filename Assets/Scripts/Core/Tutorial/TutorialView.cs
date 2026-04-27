using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Maneja la representación visual del tutorial, incluyendo animaciones de fade.
/// </summary>
public class TutorialView : MonoBehaviour
{
    [Header("UI References")]

    [Tooltip("CanvasGroup usado para controlar la transparencia.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("Texto del tutorial.")]
    [SerializeField] private TMP_Text tutorialText;

    [Tooltip("Imagen del tutorial.")]
    [SerializeField] private Image tutorialImage;

    [Tooltip("Duración del fade.")]
    [SerializeField] private float fadeDuration = 0.25f;

    private Coroutine fadeRoutine;
    private Vector2 originalTextPosition;

    private void Awake()
    {
        originalTextPosition = tutorialText.rectTransform.anchoredPosition;
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// Muestra la instrucción en pantalla con fade in.
    /// </summary>
    public void Show(string text, Sprite image, float offsetY)
    {
        tutorialText.text = $"{text}";

        ApplyImage(image);
        ApplyOffset(offsetY);

        StartFade(1f, true);
    }

    /// <summary>
    /// Oculta la vista con fade out.
    /// </summary>
    public void Hide()
    {
        StartFade(0f, false);
    }

    private void ApplyImage(Sprite image)
    {
        if (image == null)
        {
            tutorialImage.sprite = null;
            tutorialImage.color = new Color(1, 1, 1, 0); // Transparente
        }
        else
        {
            tutorialImage.sprite = image;
            tutorialImage.color = Color.white;
        }
    }

    private void ApplyOffset(float offsetY)
    {
        tutorialText.rectTransform.anchoredPosition =
            originalTextPosition + new Vector2(0, offsetY);
    }

    private void StartFade(float target, bool interactable)
    {
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeRoutine(target, interactable));
    }

    private IEnumerator FadeRoutine(float target, bool interactable)
    {
        canvasGroup.blocksRaycasts = interactable;

        float start = canvasGroup.alpha;
        float time = 0f;

        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(start, target, time / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = target;
        canvasGroup.blocksRaycasts = interactable;
    }
}