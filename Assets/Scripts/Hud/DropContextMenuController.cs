using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class DropContextMenuController : MonoBehaviour
{
    [SerializeField] private GameObject container;

    [Header("Buttons")]
    [SerializeField] private Button connectButton;
    [SerializeField] private Button disconnectButton;
    [SerializeField] private Button renameButton;

    [Header("Animation")]
    [SerializeField] private float showDuration = 0.3f;
    [SerializeField] private float hideDuration = 0.2f;

    [Header("UI")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private Vector3 offset = new Vector3(0, 40f, 0);

    private DropController currentDrop;
    private FragmentsGraphController graph;
    private Camera cam;

    private Sequence animSequence;

    private void Awake()
    {
        cam = Camera.main;

        container.transform.localScale = Vector3.zero;
        container.SetActive(false);

        SetupButtonHover(connectButton);
        SetupButtonHover(disconnectButton);
        SetupButtonHover(renameButton);
    }

    public void Show(
        DropController drop,
        FragmentsGraphController graphController)
    {
        // Si es la misma gota y ya está visible, no hacemos nada
        if (currentDrop == drop && container.activeSelf)
            return;

        currentDrop = drop;
        graph = graphController;

        // Cancelar cualquier animación anterior
        animSequence?.Kill();

        container.SetActive(true);
        container.transform.localScale = Vector3.zero;

        // Limpiar listeners
        connectButton.onClick.RemoveAllListeners();
        disconnectButton.onClick.RemoveAllListeners();
        renameButton.onClick.RemoveAllListeners();

        // Asignar listeners
        connectButton.onClick.AddListener(OnConnect);
        disconnectButton.onClick.AddListener(OnDisconnect);
        renameButton.onClick.AddListener(OnRename);

        // Animación de entrada
        animSequence = DOTween.Sequence()
            .Append(container.transform.DOScale(1f, showDuration)
                .SetEase(Ease.OutBack))
            .SetUpdate(true);
    }

    public void Hide()
    {
        if (!container.activeSelf)
            return;

        currentDrop = null;

        animSequence?.Kill();

        animSequence = DOTween.Sequence()
            .Append(container.transform.DOScale(0f, hideDuration)
                .SetEase(Ease.InBack))
            .OnComplete(() =>
            {
                container.SetActive(false);
            })
            .SetUpdate(true);
    }

    private void LateUpdate()
    {
        if (currentDrop == null)
            return;

        Vector3 screenPos =
            cam.WorldToScreenPoint(currentDrop.transform.position);

        float scale = canvas.scaleFactor;

        container.transform.position = new Vector3(
            screenPos.x + offset.x * scale,
            screenPos.y + offset.y * scale,
            0);
    }

    private void OnConnect()
    {
       
        if (currentDrop == null || graph == null)
            return;

        graph.EnterConnectMode(currentDrop);
        Hide();
    }

    private void OnDisconnect()
    {
        Debug.Log($"Quitar conexión desde {currentDrop.dropData.FragmentName}");
        currentDrop.OnClickContextMenu();
        Hide();
    }

    private void OnRename()
    {
        Debug.Log($"Renombrar {currentDrop.dropData.FragmentName}");
        graph.EnterLabelEditMode(currentDrop);
        Hide();
    }

    private void SetupButtonHover(Button button)
    {
        Transform t = button.transform;
        Vector3 baseScale = t.localScale;

        button.gameObject.AddComponent<HoverScaler>()
            .Inita(baseScale);
    }
}