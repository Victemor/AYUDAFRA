using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;
using UnityEngine.TextCore.Text;

public class DialogController : MonoBehaviour
{
    [Header("Referencias")]
    public GameObject panelRoot;             // Panel negro de fondo
    public Image panelBackground;            // Imagen negra con opacidad
    public Image dialogBox;                  // Cuadro de diálogo (imagen)
    public TextMeshProUGUI dialogText;       // Texto central
    public Button closeButton;               // Botón cerrar
    public Image closeButtonImage;           // Imagen del botón (para fade)

    [Header("Sprites disponibles")]
    [Tooltip("Lista de sprites aleatorios para el cuadro de diálogo")]
    public List<Sprite> dialogBoxSprites;

    [Header("Tiempos (segundos)")]
    public float fadeDuration = 0.5f;
    public float closeButtonDelay = 1f;
    public float closeButtonBounceDuration = 0.4f;

    [Header("Escala rebote botón")]
    public float closeButtonScaleMultiplier = 1.2f;
    public Image imageAux ;

    private CanvasGroup textCanvasGroup;
    private CanvasGroup closeButtonCanvasGroup;

    private bool dialogVisible = false;


    void Awake()
    {
        // Asegurar componentes CanvasGroup
        textCanvasGroup = dialogText.GetComponent<CanvasGroup>();
        if (textCanvasGroup == null)
            textCanvasGroup = dialogText.gameObject.AddComponent<CanvasGroup>();

        closeButtonCanvasGroup = closeButtonImage.GetComponent<CanvasGroup>();
        if (closeButtonCanvasGroup == null)
            closeButtonCanvasGroup = closeButtonImage.gameObject.AddComponent<CanvasGroup>();

        // Ocultar todo al inicio
        HideAllImmediate();

        closeButton.onClick.AddListener(CloseDialog);
    }

    //void Update()
    //{
    //    // Permitir cerrar con tecla o clic si está visible
    //    //if (dialogVisible && (Input.anyKeyDown || Input.GetMouseButtonDown(0)))
    //    //{
    //    //    CloseDialog();
    //    //}
    //}

    public void ShowDialog(string text, Sprite spritep = null)
    {
        if (spritep != null)
        {
            imageAux.sprite = spritep;
        }


        HideAllImmediate(); // Asegurar oculto antes de mostrar

        panelRoot.SetActive(true);
        dialogText.text = text;
        dialogVisible = true;

        // Asignar sprite aleatorio si hay disponibles
        if (dialogBoxSprites != null && dialogBoxSprites.Count > 0)
        {
            Sprite randomSprite = dialogBoxSprites[Random.Range(0, dialogBoxSprites.Count)];
            dialogBox.sprite = randomSprite;
        }

        // Establecer alphas manualmente antes de hacer fade para evitar parpadeo inicial
        panelBackground.color = new Color(0, 0, 0, 0);
        dialogBox.color = new Color(dialogBox.color.r, dialogBox.color.g, dialogBox.color.b, 0);
        textCanvasGroup.alpha = 0;
        closeButtonCanvasGroup.alpha = 0;

        // Fades iniciales
        panelBackground.DOFade(0.948f, fadeDuration);
        dialogBox.DOFade(1f, fadeDuration);
        textCanvasGroup.DOFade(1f, fadeDuration);

        // Retraso para mostrar el botón
        DOVirtual.DelayedCall(closeButtonDelay, () =>
        {
            closeButtonCanvasGroup.DOFade(1f, fadeDuration);
        });
    }

    public void CloseDialog()
    {
        if (!dialogVisible) return;

        dialogVisible = false;

        // Escala rebote
        closeButton.transform.DOKill();
        closeButton.transform.localScale = Vector3.one;
        closeButton.transform.DOPunchScale(Vector3.one * (closeButtonScaleMultiplier - 1f), closeButtonBounceDuration, 5, 0.5f);

        // Fades out
        panelBackground.DOFade(0f, fadeDuration);
        dialogBox.DOFade(0f, fadeDuration);
        textCanvasGroup.DOFade(0f, fadeDuration);
        closeButtonCanvasGroup.DOFade(0f, fadeDuration);

        // Desactivar después de la animación
        DOVirtual.DelayedCall(fadeDuration, () =>
        {
            panelRoot.SetActive(false);
        });
    }

    private void HideAllImmediate()
    {
        panelRoot.SetActive(false);

        // Reiniciar alphas a 0
        panelBackground.color = new Color(0, 0, 0, 0);
        dialogBox.color = new Color(dialogBox.color.r, dialogBox.color.g, dialogBox.color.b, 0);

        if (textCanvasGroup != null)
            textCanvasGroup.alpha = 0;

        if (closeButtonCanvasGroup != null)
            closeButtonCanvasGroup.alpha = 0;
    }
}
