using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManagerScene : MonoBehaviour
{
    [SerializeField] private DialogController dialogController;
    private ScreenFade screenFade = null;
    [SerializeField] private GameObject backButton;
    [SerializeField] private GameObject InfoButton;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        try
        {
            Invoke("OpenInstructions", 1f);
        }
        catch
        {
            Debug.Log("Error mostrando instrucciones");
        }
     
    }

    private void OpenInstructions()
    {
        // if (GameManager.Instance.fragmentsOpened == 1)
        // {
        //     dialogController.ShowDialog("Haz zoom con el scroll del mouse para ver el fragmento m�s de cerca. F�jate en todos los detalles. Puedes regresar con la tecla Esc.");
        // }
    }

    public void ShowInfo()
    {
        InfoButton.transform.DOPunchScale(Vector3.one * (1.2f - 1f), 0.4f, 5, 0.5f).OnComplete(() =>
        {
            dialogController.ShowDialog("Haz zoom con el scroll del mouse para ver el fragmento m�s de cerca. F�jate en todos los detalles. Puedes regresar con la tecla Esc.");
        });
 
    }
    public void GoBack()
    {
        backButton.transform.DOPunchScale(Vector3.one * (1.2f - 1f), 0.4f, 5, 0.5f).OnComplete(() =>
        {
            BackToMenu();
        });
    }
    private void BackToMenu()
    {
        screenFade = GameObject.Find("BlackPanel").GetComponent<ScreenFade>();
        screenFade.FadeIn(this.name);

        DOVirtual.DelayedCall(1f, () =>
        {
            // Aqu� cargas la escena MainMenu
            DOTween.KillAll();
            SceneManager.LoadScene("MainMenu");

        });

    }

}
