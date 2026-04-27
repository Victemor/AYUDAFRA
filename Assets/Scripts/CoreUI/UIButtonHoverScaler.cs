using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

/// <summary>
/// Escala un botón al hacer hover y gestiona el cambio de cursor.
/// </summary>
public class UIButtonHoverScaler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Escala")]

    [Tooltip("Multiplicador de escala al hacer hover.")]
    [SerializeField] private float scaleMouse = 1.15f;

    [Tooltip("Duración de la animación de escala.")]
    [SerializeField] private float scaleMouseTime = 0.2f;

    private Vector3 originalScale;
    private Tween scaleTween;

    /// <summary>
    /// Bloquea cambios de cursor tras click.
    /// </summary>
    private bool lockCursorChange = false;

    private void Start()
    {
        originalScale = transform.localScale;
    }

    /// <summary>
    /// Se ejecuta al entrar el puntero.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (lockCursorChange)
            return;

        // Animación
        scaleTween?.Kill();
        scaleTween = transform
            .DOScale(originalScale * scaleMouse, scaleMouseTime)
            .SetEase(Ease.OutBack);

    }

    /// <summary>
    /// Se ejecuta al salir el puntero.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        if (lockCursorChange)
            return;

        // Animación
        scaleTween?.Kill();
        scaleTween = transform
            .DOScale(originalScale, scaleMouseTime)
            .SetEase(Ease.OutSine);

    }

    /// <summary>
    /// Se ejecuta al hacer click.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        lockCursorChange = true;
        Invoke(nameof(UnlockCursorChange), 2f);

    }

    /// <summary>
    /// Desbloquea el cambio de cursor.
    /// </summary>
    private void UnlockCursorChange()
    {
        lockCursorChange = false;
    }
}