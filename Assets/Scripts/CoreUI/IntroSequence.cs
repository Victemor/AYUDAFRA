using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;
using Game.Core;

public class IntroSequence : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private TextMeshProUGUI text1;
    [SerializeField] private TextMeshProUGUI text2_1;
    [SerializeField] private TextMeshProUGUI text2_2;
    [SerializeField] private TextMeshProUGUI text2_3;
    [SerializeField] private TextMeshProUGUI text3;
    [SerializeField] private TextMeshProUGUI text4; // Texto de instrucción para hacer clic
    [SerializeField] private ScreenFade screenFade;

    [Header("Tiempos globales")]
    [SerializeField] private float initialDelay = 1f;
    [SerializeField] private float timeBetweenTexts = 1f;
    [SerializeField] private float fadeDuration = 1f;

    [Header("Duración por texto")]
    [SerializeField] private float text1DisplayDuration = 2f;
    [SerializeField] private float text2StepDelay = 1f;
    [SerializeField] private float text3DisplayDuration = 2f;

    [Header("Escala texto final")]
    [SerializeField] private float finalTextScaleMultiplier = 1.2f;
    [SerializeField] private float finalTextScaleDuration = 2f;


    private void Start()
    {
        GamePlayStateController.Instance.EnterTransition();
        text1.alpha = 0f;
        text2_1.alpha = 0f;
        text2_2.alpha = 0f;
        text2_3.alpha = 0f;
        text3.alpha = 0f;
        text4.alpha = 0f;
        text3.transform.localScale = Vector3.one;

        StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
            yield return new WaitForSeconds(initialDelay);

            // Texto 1
            yield return ShowText(text1, text1DisplayDuration);
            yield return new WaitForSeconds(timeBetweenTexts);

            // Texto 2.1 → 2.2 → 2.3 en cascada
            yield return text2_1.DOFade(1f, fadeDuration).WaitForCompletion();
            yield return new WaitForSeconds(text2StepDelay);

            yield return text2_2.DOFade(1f, fadeDuration).WaitForCompletion();
            yield return new WaitForSeconds(text2StepDelay / 2);

            yield return text2_3.DOFade(1f, fadeDuration).WaitForCompletion();

            // Mostrar texto 4 (instrucción para continuar)
            yield return new WaitForSeconds(text2StepDelay / 2);

            bool interacted = false;
            Coroutine waitInput = StartCoroutine(WaitForUserInput(() => interacted = true));

            // Fade-in del texto 4
            yield return text4.DOFade(1f, fadeDuration/2).WaitForCompletion();

            // Esperar a que el clic o la tecla ocurra (si no pasó ya)
            yield return new WaitUntil(() => interacted);

            // Fade out de todos los textos 2 y el texto 4
            Sequence fadeOutSequence = DOTween.Sequence();
            fadeOutSequence.Join(text2_1.DOFade(0f, fadeDuration));
            fadeOutSequence.Join(text2_2.DOFade(0f, fadeDuration));
            fadeOutSequence.Join(text2_3.DOFade(0f, fadeDuration));
            fadeOutSequence.Join(text4.DOFade(0f, fadeDuration));
            yield return fadeOutSequence.WaitForCompletion();

            yield return new WaitForSeconds(timeBetweenTexts);

            // Texto 3 con escala
            yield return ShowTextWithScale(text3, text3DisplayDuration);

            // Fade final y cambio de escena
            screenFade.FadeIn(this.name);
            yield return new WaitForSeconds(1f);
            GamePlayStateController.Instance.EnterMenu();
            SceneManager.LoadScene("MainMenu"); 
       
    }
    private IEnumerator WaitForUserInput(System.Action onInput)
    {
        while (!Input.GetMouseButtonDown(0) && !Input.anyKeyDown)
            yield return null;
        onInput?.Invoke();
    }

    private IEnumerator ShowText(TextMeshProUGUI text, float duration)
    {
        yield return text.DOFade(1f, fadeDuration).WaitForCompletion();
        yield return new WaitForSeconds(duration);
        yield return text.DOFade(0f, fadeDuration).WaitForCompletion();
    }

    private IEnumerator ShowTextWithScale(TextMeshProUGUI text, float duration)
    {
        text.alpha = 0f;
        text.transform.localScale = Vector3.one;

        text.DOFade(1f, fadeDuration);
        text.transform.DOScale(Vector3.one * finalTextScaleMultiplier, finalTextScaleDuration).SetEase(Ease.InOutSine);

        yield return new WaitForSeconds(duration);
        yield return text.DOFade(0f, fadeDuration).WaitForCompletion();
    }
}
