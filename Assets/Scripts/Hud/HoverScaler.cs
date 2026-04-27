using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

public class HoverScaler : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    private Vector3 baseScale;
    private Tween tween;

    public void Inita(Vector3 baseScale)
    {
        this.baseScale = baseScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        tween?.Kill();
        tween = transform.DOScale(baseScale * 1.15f, 0.15f)
            .SetEase(Ease.OutBack)
            .SetUpdate(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        tween?.Kill();
        tween = transform.DOScale(baseScale, 0.15f)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }
}
