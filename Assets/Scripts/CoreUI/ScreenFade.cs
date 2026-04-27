using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.SceneManagement;

[ExecuteAlways]
public class ScreenFade : MonoBehaviour
{
    [Header("Componente de imagen negra")]
    [SerializeField] private Image blackImage;

    [Header("Tiempos")]
    [SerializeField] private float fadeInTime = 1f;
    [SerializeField] private float fadeOutTime = 1f;
    [SerializeField] public float blinkTime = 0.3f;

    private void Reset()
    {
        blackImage = GetComponent<Image>();
    }
    private void Start()
    {
        blackImage.raycastTarget = false;
        Color c = blackImage.color;
        c.a = 1f;
        blackImage.color = c;
        FadeOut();
    }


    public void FadeIn(string fromObject)
    {
        if (SceneManager.GetActiveScene().name != "Intro")
        {
            SystemsGameplay.Instance.GetTutorialController().HideTutorial(); 
        }


        Debug.Log($"FadeIn llamado desde {fromObject}");
        if (blackImage == null) return;

        blackImage.DOFade(1f, fadeInTime).SetEase(Ease.InOutQuad);

    }

    public void FadeOut()
    {
        if (blackImage == null) return;

        blackImage.DOFade(0f, fadeOutTime).SetEase(Ease.InOutQuad);
            
        //    .OnComplete(() =>
        //{
        //    blackImage.raycastTarget = true;
        //});
    }

    public void Blink()
    {
        if (blackImage == null) return;

        Sequence blinkSeq = DOTween.Sequence();
        blinkSeq.Append(blackImage.DOFade(1f, blinkTime).SetEase(Ease.InOutQuad));
        blinkSeq.Append(blackImage.DOFade(0f, blinkTime).SetEase(Ease.InOutQuad));
    }
}
