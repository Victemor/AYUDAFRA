using DG.Tweening;
using TMPro;
using UnityEngine;
using Game.Runtime;

/// <summary>
/// Controlador visual del label editable de un Drop (fragmento).
/// Gestiona la animación de aparición, modo edición y persistencia del nombre.
/// </summary>
public class DropLabelController : MonoBehaviour
{
    [SerializeField] private GameObject container;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_InputField editInput;
    [SerializeField] private TMP_Text charCounter;

    [SerializeField] private float showDuration = 0.25f;
    [SerializeField] private float hideDuration = 0.15f;

    [SerializeField] private Vector3 offset = new Vector3(0, -40f, 0);

    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform backgroundRect;

    [Header("Sizing")]
    [SerializeField] private float baseWidth = 80f;
    [SerializeField] private float minWidth = 80f;
    [SerializeField] private float charWidth = 12f;
    [SerializeField] private float horizontalPadding = 24f;
    [SerializeField] private float resizeDuration = 0.15f;

    [SerializeField] private int maxChars = 20;
    [SerializeField] private int minChars = 1;

    private Camera cam;
    private DropController currentDrop;
    private Sequence animSequence;
    private bool isEditing = false;

    private void Awake()
    {
        cam = Camera.main;
    }

    public void Show(DropController drop)
    {
        if (currentDrop == drop && container.activeSelf)
            return;

        currentDrop = drop;

        string label =
            string.IsNullOrEmpty(drop.dropData.CustomLabel)
                ? drop.dropData.FragmentName
                : drop.dropData.CustomLabel;

        labelText.text = label;
        UpdateBackgroundSize(label.Length);
        container.SetActive(true);

        animSequence?.Kill();
        container.transform.localScale = Vector3.zero;

        animSequence = DOTween.Sequence()
            .Append(container.transform.DOScale(1f, showDuration).SetEase(Ease.OutBack))
            .SetUpdate(true);
    }

    public void Hide()
    {
        if (!container.activeSelf)
            return;

        currentDrop = null;

        animSequence?.Kill();
        animSequence = DOTween.Sequence()
            .Append(container.transform.DOScale(0f, hideDuration).SetEase(Ease.InBack))
            .OnComplete(() => container.SetActive(false))
            .SetUpdate(true);
    }

    public void EnterEditMode()
    {
        if (currentDrop == null || isEditing)
            return;

        isEditing = true;

        labelText.gameObject.SetActive(false);
        editInput.gameObject.SetActive(true);
        charCounter.gameObject.SetActive(true);

        editInput.text = currentDrop.dropData.CustomLabel ?? string.Empty;

        UpdateBackgroundSize(editInput.text.Length);
        UpdateCounter(editInput.text.Length);

        editInput.onValueChanged.RemoveAllListeners();
        editInput.onValueChanged.AddListener(value =>
        {
            UpdateCounter(value.Length);
            UpdateBackgroundSize(value.Length);
        });

        editInput.ActivateInputField();
    }

    public void ConfirmAndExitEditMode()
    {
        if (!isEditing)
            return;

        isEditing = false;

        editInput.onValueChanged.RemoveAllListeners();

        string value = editInput.text.Trim();

        currentDrop.dropData.SetCustomLabel(value);
        GameManager.Instance.SaveProgress();

        // Notifica al sistema de tutoriales que el jugador renombró un fragmento.
        GameEvents.RaiseFragmentRenamed();

        labelText.gameObject.SetActive(true);
        editInput.gameObject.SetActive(false);
        charCounter.gameObject.SetActive(false);

        string label =
            string.IsNullOrEmpty(value)
                ? currentDrop.dropData.FragmentName
                : value;

        labelText.text = label;
        UpdateBackgroundSize(label.Length);
        Hide();
    }

    private void LateUpdate()
    {
        if (currentDrop == null)
            return;

        Vector3 screenPos = cam.WorldToScreenPoint(currentDrop.transform.position);
        float scale = canvas.scaleFactor;

        container.transform.position = new Vector3(
            screenPos.x + offset.x * scale,
            screenPos.y + offset.y * scale,
            0);
    }

    private void UpdateBackgroundSize(int charCount)
    {
        float dynamicWidth = baseWidth + (charCount * charWidth) + horizontalPadding;
        float targetWidth = Mathf.Max(dynamicWidth, minWidth);

        Vector2 size = backgroundRect.sizeDelta;
        size.x = targetWidth;

        backgroundRect
            .DOSizeDelta(size, resizeDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true);
    }

    private void UpdateCounter(int count)
    {
        charCounter.text = $"{count}/{maxChars}";
    }

    public void OnEditValueChanged(string value)
    {
        UpdateCounter(value.Length);
    }
}