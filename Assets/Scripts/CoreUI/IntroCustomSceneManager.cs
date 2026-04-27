using DG.Tweening;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class IntroCustomSceneManager : MonoBehaviour
{
    [SerializeField] private float secondsText = 2f;
    [SerializeField] private float secondsActive = 10f;
    [SerializeField] private ScreenFade screenFade;
    [SerializeField] private TextMeshProUGUI textMeshProUGUI;

    void Start()
    {
        // Asegura que el texto comience invisible
        Color initialColor = textMeshProUGUI.color;
        textMeshProUGUI.color = new Color(initialColor.r, initialColor.g, initialColor.b, 0f);

        StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        // Fade In del texto usando DOTween
        textMeshProUGUI.DOFade(1f, secondsText);

        // Espera que se complete el fade
        yield return new WaitForSeconds(secondsText + secondsActive);

        // Fade final y cambio de escena
        screenFade.FadeIn(this.name);
        yield return new WaitForSeconds(1f);
        SceneManager.LoadScene("IntroAux");
    }
}
