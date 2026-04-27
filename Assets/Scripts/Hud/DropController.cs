using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using Game.Data;
using Game.Runtime;
using Game.Systems;
using UnityEngine.SceneManagement;
using Game.Core;

/// <summary>
/// Controlador visual e interactivo de una gota del menú.
/// </summary>
public class DropController : MonoBehaviour
{
    #region Inspector References

    [SerializeField] private ParticleSystem visitedParticles;

    #endregion

    #region Configuration

    [SerializeField] private float hoverScaleMultiplier = 1.2f;
    [SerializeField] private float hoverScaleDuration = 1.2f;
    [SerializeField] private float dragScaleMultiplier = 0.8f;
    [SerializeField] private float dragScaleDuration = 0.2f;
    [SerializeField] private float dragThreshold = 0.05f;
    [SerializeField] private float highlightScaleMultiplier = 1.8f;
    [SerializeField] private float maxResidualVelocity = 0.08f;

    [Header("Dim Animation")]
    [SerializeField] private float dimDuration = 0.25f;
    [SerializeField, Range(0f, 1f)] private float dimmedAlpha = 0.35f;

    private Tween alphaTween;

    #endregion

    #region Runtime State

    private MemoryDefinition memoryDefinition;
    public DropData dropData { get; private set; }

    public bool isDragging { get; private set; }
    private bool hasDragged;

    private Vector3 dragOffset;
    private Vector3 initialMousePosition;
    private float mouseDownTime;

    private Vector3 originalScale;
    private Tween scaleTween;

    private SpriteRenderer spriteRenderer;
    private Material instanceMaterial;

    private FragmentsGraphController graphController;
    private Vector3 residualVelocity;
    private Tween highlightTween;

    #endregion

    #region Ripple Effect

    [SerializeField] private RippleDropController[] rippleControllers;

    #endregion

    #region Initialization

    public void Initialize(
        MemoryDefinition memoryDefinition,
        DropData data,
        FragmentsGraphController graphController)
    {
        this.memoryDefinition = memoryDefinition;
        dropData = data;
        this.graphController = graphController;

        transform.localPosition = dropData.Position;
        originalScale = transform.localScale;

        InitializeMaterialInstance();
        UpdateVisualState();
        StartRipple();
    }

    private void InitializeMaterialInstance()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.material != null)
        {
            instanceMaterial = new Material(spriteRenderer.material);
            spriteRenderer.material = instanceMaterial;
        }
    }

    #endregion

    #region Visual State

    private void UpdateVisualState()
    {
        var runtime = GameStateRepository.Instance.GetMemory(memoryDefinition);
        if (runtime.CurrentState >= MemoryState.Seen && visitedParticles != null)
        {
            visitedParticles.gameObject.SetActive(true);
        }
    }

    #endregion

    #region Interaction - Hover

    private void OnMouseEnter()
    {
        if (IsBlockedByTransition()) return;
        if (graphController.IsInEditLabelMode()) return;
        if (IsPointerOverUI() || isDragging) return;

        graphController.OnDropHoverEnter(this);
        AnimateScale(originalScale * hoverScaleMultiplier, hoverScaleDuration, Ease.OutBack);
    }

    private void OnMouseExit()
    {
        if (IsBlockedByTransition()) return;
        if (graphController.IsInEditLabelMode()) return;
        if (IsPointerOverUI() || isDragging) return;

        graphController.OnDropHoverExit(this);

        if (graphController.IsInConnectMode() && graphController.GetOwnerDrop() == this)
            return;

        AnimateScale(originalScale, hoverScaleDuration, Ease.OutSine);
    }

    #endregion

    #region Interaction - Drag & Click

    private void OnMouseDown()
    {
        if (graphController.IsInEditLabelMode()) return;
        if (IsPointerOverUI()) return;

        isDragging = true;
        hasDragged = false;

        graphController.HideAllUI();
        graphController.NotifyDropClicked();

        mouseDownTime = Time.time;
        initialMousePosition = GetMouseWorldPosition();
        dragOffset = transform.position - initialMousePosition;

        AnimateScale(originalScale * dragScaleMultiplier, dragScaleDuration, Ease.OutSine);
    }

    private bool IsBlockedByTransition()
    {
        return UIGameplayManager.Instance != null && UIGameplayManager.Instance.IsTransitioning;
    }

    private void OnMouseUp()
    {
        if (IsBlockedByTransition()) return;
        if (graphController.IsInEditLabelMode()) return;
        if (IsPointerOverUI()) return;

        isDragging = false;
        ClearResidualVelocity();

        AnimateScale(originalScale, hoverScaleDuration, Ease.OutBack);
        graphController.ApplyReleaseImpulse(this);
        graphController.StartRelaxation();

        if (!hasDragged)
        {
            OnClick();
        }

        graphController.StartRelaxation();
    }

    private void OnMouseOver()
    {
        if (IsBlockedByTransition()) return;

        if (Input.GetMouseButtonDown(1))
        {
            // Notifica al sistema de tutoriales que el jugador hizo clic derecho en un drop.
            GameEvents.RaiseDropRightClicked();

            graphController.OnDropLeftClick(this);
        }
    }

    private void Update()
    {
        if (IsBlockedByTransition()) return;
        if (isDragging)
        {
            HandleDrag();
        }
    }

    /// <summary>
    /// Lógica de arrastre de la gota.
    /// Cuando el drag supera el umbral por primera vez, notifica al sistema de tutoriales.
    /// </summary>
    private void HandleDrag()
    {
        Vector3 currentMousePosition = GetMouseWorldPosition();
        float distance = Vector3.Distance(initialMousePosition, currentMousePosition);

        if (!hasDragged && distance >= dragThreshold)
        {
            hasDragged = true;
            GameEvents.RaiseDropMoved();
        }

        Vector3 targetPosition = currentMousePosition + dragOffset;
        targetPosition.y = transform.position.y;

        transform.position = targetPosition;
        dropData.Position = transform.localPosition;

        graphController.ResolveChainRepulsion();
    }

    public void OnClickContextMenu() => OnClick();

    private void OnClick()
    {
        if (graphController.IsInConnectMode())
        {
            graphController.HandleConnectClick(this);
            return;
        }

        var memoryRuntime = GameStateRepository.Instance.GetMemory(memoryDefinition);
        memoryRuntime.MarkAsSeen();
        GameEvents.RaisePlayerAction();
        ConditionEvaluationSystem.Instance?.EvaluateAll();
        UpdateVisualState();

        if (!string.IsNullOrEmpty(memoryDefinition.SceneName))
        {
            UIMainMenuManager manager = FindObjectOfType<UIMainMenuManager>();
            if (manager != null)
            {
                GamePlayStateController.Instance.EnterFreeExploration();
                manager.OpenScene(memoryDefinition.SceneName);
            }
            else
            {
                Debug.LogError("UIMainMenuManager no encontrado en la escena");
                GamePlayStateController.Instance.EnterFreeExploration();
                SceneManager.LoadScene(memoryDefinition.SceneName);
            }
        }
        else
        {
            Debug.LogWarning($"Memory {memoryDefinition.Id} no tiene escena asignada");
        }
    }

    #endregion

    #region Repulsion

    public void ApplyRepulsion(Vector3 offset)
    {
        transform.position += offset;
        residualVelocity += offset;
        residualVelocity = Vector3.ClampMagnitude(residualVelocity, maxResidualVelocity * 1000);
        dropData.Position = transform.localPosition;
    }

    public Vector3 GetResidualVelocity() => residualVelocity;

    public void ApplyVelocity(Vector3 velocity)
    {
        residualVelocity = velocity;
        transform.position += velocity * Time.deltaTime;
        dropData.Position = transform.localPosition;
    }

    public void ClearResidualVelocity() => residualVelocity = Vector3.zero;

    public void CommitPosition()
    {
        dropData.Position = new Vector3(transform.localPosition.x, transform.localPosition.z);
    }

    #endregion

    #region Visual Effects

    public void FadeToIntensity(float targetIntensity, float duration)
    {
        if (instanceMaterial == null) return;
        float current = instanceMaterial.GetFloat("_Intensity");
        DOTween.To(() => current, v => { current = v; instanceMaterial.SetFloat("_Intensity", v); }, targetIntensity, duration)
            .SetEase(Ease.InOutSine);
    }

    public MemoryDefinition GetMemoryDefinition() => memoryDefinition;
    public void SetDimmed() => AnimateAlpha(dimmedAlpha);
    public void SetNormal() => AnimateAlpha(1f);

    public void SetHighlighted(bool highlighted)
    {
        highlightTween?.Kill();
        highlightTween = highlighted
            ? transform.DOScale(originalScale * highlightScaleMultiplier, 0.25f).SetEase(Ease.OutBack)
            : transform.DOScale(originalScale, 0.2f).SetEase(Ease.OutQuad);
    }

    private void AnimateAlpha(float targetAlpha)
    {
        alphaTween?.Kill();
        float start = GetCurrentAlpha();
        alphaTween = DOTween.To(() => start, a => { start = a; SetAlphaImmediate(a); }, targetAlpha, dimDuration)
            .SetEase(Ease.OutQuad);
    }

    private float GetCurrentAlpha()
    {
        if (spriteRenderer != null) return spriteRenderer.color.a;
        Renderer r = GetComponentInChildren<Renderer>();
        if (r != null && r.material.HasProperty("_Color")) return r.material.color.a;
        return 1f;
    }

    private void SetAlphaImmediate(float alpha)
    {
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = alpha;
            spriteRenderer.color = c;
        }

        if (visitedParticles != null)
        {
            var main = visitedParticles.main;
            Color c = main.startColor.color;
            c.a = alpha;
            main.startColor = c;
        }

        if (rippleControllers == null) return;
        foreach (var ripple in rippleControllers)
        {
            if (ripple != null)
                ripple.GetComponent<SpriteRenderer>().color = new Color(1f, 1f, 1f, alpha);
        }
    }

    #endregion

    #region Ripple Control

    private void StartRipple()
    {
        if (rippleControllers == null) return;
        foreach (var ripple in rippleControllers)
            ripple?.StartPulse();
    }

    #endregion

    #region Utilities

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePosition = Input.mousePosition;
        mousePosition.z = Camera.main.WorldToScreenPoint(transform.position).z;
        return Camera.main.ScreenToWorldPoint(mousePosition);
    }

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void AnimateScale(Vector3 targetScale, float duration, Ease ease)
    {
        scaleTween?.Kill();
        scaleTween = transform.DOScale(targetScale, duration).SetEase(ease);
    }

    #endregion
}