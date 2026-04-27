using UnityEngine;
using System.Collections;
using Game.Core;

/// <summary>
/// Orquestador de estados de cámara.
/// Gestiona la transición entre modo exploración y modo cinemático.
/// </summary>
public sealed class CameraSystemController : MonoBehaviour
{
    #region Enums

    /// <summary>
    /// Estados disponibles de la cámara.
    /// </summary>
    private enum CameraState
    {
        Exploration,
        Cinematic
    }

    #endregion

    #region Serialized Fields

    [Header("Referencias")]

    [SerializeField]
    [Tooltip("Controlador principal de cámara.")]
    private CinemachineController cameraController;

    [SerializeField]
    [Tooltip("Pivot que Cinemachine sigue.")]
    private CameraTargetProxy followTarget;

    [SerializeField]
    [Tooltip("Objeto que la cámara seguirá en modo cinemático.")]
    private Transform cinematicTarget;

    [Header("Exploration Settings")]

    [SerializeField]
    [Tooltip("Zoom mínimo permitido en modo exploración.")]
    private float explorationMinZoom = 5f;

    [SerializeField]
    [Tooltip("Zoom máximo permitido en modo exploración.")]
    private float explorationMaxZoom = 12f;

    [SerializeField]
    [Tooltip("Zoom inicial aplicado al entrar en exploración.")]
    private float explorationInitialZoom = 8f;

    [Header("Cinematic Settings")]

    [SerializeField]
    [Tooltip("Zoom mínimo permitido en modo cinemático.")]
    private float cinematicMinZoom = 3f;

    [SerializeField]
    [Tooltip("Zoom máximo permitido en modo cinemático.")]
    private float cinematicMaxZoom = 5f;

    [SerializeField]
    [Tooltip("Zoom inicial aplicado al entrar en modo cinemático.")]
    private float cinematicInitialZoom = 4f;

    [Header("Cinematic Composition")]

    [SerializeField]
    [Tooltip("Compensación horizontal aplicada únicamente en modo cinemático para reservar espacio visual de UI.")]
    private float cinematicHorizontalCenterOffset = 0f;

    [Header("Transition Settings")]

    [SerializeField]
    [Tooltip("Duración de la transición.")]
    private float transitionDuration = 0.6f;

    [SerializeField]
    [Tooltip("Curva de interpolación.")]
    private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    #endregion

    #region Private Fields

    private CameraState currentState;
    private Transform currentCinematicTarget;
    private Coroutine transitionRoutine;

    #endregion

    #region Unity Methods

    /// <summary>
    /// Inicializa la cámara en modo exploración.
    /// </summary>
    private void Start()
    {
        if (!Validate())
        {
            enabled = false;
            return;
        }

        InitializeExplorationImmediate();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Permite modificar la duración de la transición en tiempo de ejecución.
    /// </summary>
    public void SetTransitionDuration(float duration)
    {
        if (duration < 0f)
        {
            Debug.LogWarning("TransitionDuration no puede ser negativo.");
            return;
        }

        transitionDuration = duration;
    }

    /// <summary>
    /// Entra en modo exploración con transición.
    /// </summary>
    public void EnterExplorationMode()
    {
        GamePlayStateController.Instance.ExitActionFlowToExploration();
        currentState = CameraState.Exploration;
        currentCinematicTarget = null;

        Vector3 targetPosition = followTarget.GetExplorationEntryPosition();

        float targetZoom = Mathf.Clamp(
            explorationInitialZoom,
            explorationMinZoom,
            explorationMaxZoom
        );

        StartTransition(targetPosition, targetZoom, () =>
        {
            followTarget.SetMouseFollow();
            followTarget.SetIgnoreBounds(false);

            cameraController.SetZoomLimits(explorationMinZoom, explorationMaxZoom);
            cameraController.SetInitialZoom(targetZoom);
            cameraController.SyncExplorationAnchor();
            cameraController.EnableMouseExploration(true);
        });
    }

    /// <summary>
    /// Entra en modo cinemático usando el target serializado.
    /// </summary>
    public void EnterCinematicMode()
    {
        if (cinematicTarget == null)
        {
            Debug.LogError("CinematicTarget no asignado.");
            return;
        }
        
        GamePlayStateController.Instance.EnterActionFlowCinematic();
        EnterCinematicMode(cinematicTarget, Vector3.zero);
    }

    /// <summary>
    /// Entra en modo cinemático con un target específico.
    /// </summary>
    public void EnterCinematicMode(Transform focusTarget, Vector3 offset)
    {
        if (!ValidateCinematicTarget(focusTarget))
        {
            return;
        }

        currentState = CameraState.Cinematic;
        currentCinematicTarget = focusTarget;

        Vector3 targetPosition = followTarget.GetFollowPositionForTarget(focusTarget, offset);
        targetPosition = ApplyCinematicHorizontalOffset(targetPosition);

        float targetZoom = Mathf.Clamp(
            cinematicInitialZoom,
            cinematicMinZoom,
            cinematicMaxZoom
        );

        followTarget.SetIgnoreBounds(true);

        StartTransition(targetPosition, targetZoom, () =>
        {
            Vector3 resolvedOffset = offset + new Vector3(cinematicHorizontalCenterOffset, 0f, 0f);
            followTarget.SetTarget(focusTarget, resolvedOffset);

            cameraController.SetZoomLimits(cinematicMinZoom, cinematicMaxZoom);
            cameraController.SetInitialZoom(targetZoom);
            cameraController.EnableMouseExploration(false);
        });
    }

    /// <summary>
    /// Entra en modo cinemático usando configuración personalizada de zoom.
    /// Mantiene compatibilidad con sistemas existentes.
    /// </summary>
    public void EnterCinematicMode(
        Transform focusTarget,
        Vector3 offset,
        bool overrideZoom,
        float minZoom,
        float maxZoom,
        float initialZoom)
    {
        if (!ValidateCinematicTarget(focusTarget))
        {
            return;
        }

        currentState = CameraState.Cinematic;
        currentCinematicTarget = focusTarget;

        float resolvedMinZoom = overrideZoom ? minZoom : cinematicMinZoom;
        float resolvedMaxZoom = overrideZoom ? maxZoom : cinematicMaxZoom;

        if (resolvedMinZoom > resolvedMaxZoom)
        {
            Debug.LogWarning("Min zoom mayor que max zoom.");
            return;
        }

        float resolvedInitialZoom = overrideZoom
            ? Mathf.Clamp(initialZoom, resolvedMinZoom, resolvedMaxZoom)
            : Mathf.Clamp(cinematicInitialZoom, cinematicMinZoom, cinematicMaxZoom);

        Vector3 targetPosition = followTarget.GetFollowPositionForTarget(focusTarget, offset);
        targetPosition = ApplyCinematicHorizontalOffset(targetPosition);

        followTarget.SetIgnoreBounds(true);

        StartTransition(targetPosition, resolvedInitialZoom, () =>
        {
            Vector3 resolvedOffset = offset + new Vector3(cinematicHorizontalCenterOffset, 0f, 0f);
            followTarget.SetTarget(focusTarget, resolvedOffset);

            cameraController.EnableMouseExploration(false);
            cameraController.SetZoomLimits(resolvedMinZoom, resolvedMaxZoom);
            cameraController.SetInitialZoom(resolvedInitialZoom);
        });
    }

    /// <summary>
    /// Sale del modo cinemático y vuelve a exploración.
    /// </summary>
    public void ExitCinematicMode()
    {
        EnterExplorationMode();
    }

    /// <summary>
    /// Indica si la cámara está actualmente en modo cinemático.
    /// </summary>
    public bool IsCinematicMode()
    {
        return currentState == CameraState.Cinematic;
    }

    /// <summary>
    /// Cambia el target de seguimiento sin modificar el estado ni aplicar transición.
    /// Mantiene compatibilidad con sistemas existentes.
    /// </summary>
    public void SetFollowTarget(Transform newTarget)
    {
        if (!ValidateCinematicTarget(newTarget))
        {
            return;
        }

        currentCinematicTarget = newTarget;
        Vector3 resolvedOffset = new Vector3(cinematicHorizontalCenterOffset, 0f, 0f);
        followTarget.SetTarget(newTarget, resolvedOffset);

        Debug.Log($"[Camera] Now following: {newTarget.name}");
    }

    /// <summary>
    /// Cambia el target de seguimiento con transición suave, sin modificar el estado actual.
    /// Mantiene compatibilidad con sistemas existentes.
    /// </summary>
    public void SetFollowTargetWithTransition(Transform newTarget)
    {
        if (!ValidateCinematicTarget(newTarget))
        {
            return;
        }

        currentCinematicTarget = newTarget;

        float currentZoom = GetCurrentZoom();
        Vector3 targetPosition = followTarget.GetFollowPositionForTarget(newTarget, Vector3.zero);
        targetPosition = ApplyCinematicHorizontalOffset(targetPosition);

        if (currentState == CameraState.Cinematic)
        {
            followTarget.SetIgnoreBounds(true);
        }

        StartTransition(targetPosition, currentZoom, () =>
        {
            Vector3 resolvedOffset = new Vector3(cinematicHorizontalCenterOffset, 0f, 0f);
            followTarget.SetTarget(newTarget, resolvedOffset);

            Debug.Log($"[Camera] Smooth follow → {newTarget.name}");
        });
    }

    #endregion

    #region Transition System

    /// <summary>
    /// Inicia una transición controlada de cámara.
    /// </summary>
    private void StartTransition(Vector3 targetPosition, float targetZoom, System.Action onComplete)
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
        }

        transitionRoutine = StartCoroutine(
            TransitionRoutine(targetPosition, targetZoom, onComplete)
        );
    }

    /// <summary>
    /// Ejecuta la transición oficial entre modos y cambios de target.
    /// </summary>
    private IEnumerator TransitionRoutine(Vector3 targetPosition, float targetZoom, System.Action onComplete)
    {
        Transform pivot = followTarget.transform;

        Vector3 startPosition = pivot.position;
        float startZoom = GetCurrentZoom();

        if (transitionDuration <= 0f)
        {
            pivot.position = targetPosition;
            cameraController.SetZoomDirect(targetZoom);

            cameraController.SetIgnoreZoomLimits(false);
            cameraController.SetZoomOverride(false);

            transitionRoutine = null;

            onComplete?.Invoke();
            yield break;
        }

        float elapsed = 0f;

        cameraController.EnableMouseExploration(false);
        cameraController.SetZoomOverride(true);
        cameraController.SetIgnoreZoomLimits(true);

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;

            float normalizedTime = Mathf.Clamp01(elapsed / transitionDuration);
            float curveTime = transitionCurve.Evaluate(normalizedTime);

            pivot.position = Vector3.Lerp(startPosition, targetPosition, curveTime);

            float zoom = Mathf.Lerp(startZoom, targetZoom, curveTime);
            cameraController.SetZoomDirect(zoom);

            yield return null;
        }

        pivot.position = targetPosition;
        cameraController.SetZoomDirect(targetZoom);

        cameraController.SetIgnoreZoomLimits(false);
        cameraController.SetZoomOverride(false);

        transitionRoutine = null;

        onComplete?.Invoke();
    }

    /// <summary>
    /// Inicializa la cámara en modo exploración sin transición.
    /// </summary>
    private void InitializeExplorationImmediate()
    {
        currentState = CameraState.Exploration;
        currentCinematicTarget = null;

        followTarget.SetIgnoreBounds(false);
        followTarget.SetMouseFollow();

        float initialZoom = Mathf.Clamp(
            explorationInitialZoom,
            explorationMinZoom,
            explorationMaxZoom
        );

        cameraController.SetZoomLimits(explorationMinZoom, explorationMaxZoom);
        cameraController.SetInitialZoom(initialZoom);
        cameraController.SyncExplorationAnchor();
        cameraController.EnableMouseExploration(true);
        cameraController.SetZoomOverride(false);
        cameraController.SetIgnoreZoomLimits(false);
    }

    /// <summary>
    /// Obtiene el zoom actual desde el controlador de cámara.
    /// </summary>
    private float GetCurrentZoom()
    {
        return cameraController != null
            ? cameraController.GetCurrentZoom()
            : 0f;
    }

    #endregion

    #region Composition

    /// <summary>
    /// Aplica la compensación horizontal exclusiva del modo cinemático.
    /// </summary>
    private Vector3 ApplyCinematicHorizontalOffset(Vector3 position)
    {
        return position + new Vector3(cinematicHorizontalCenterOffset, 0f, 0f);
    }

    #endregion

    #region Validation

    /// <summary>
    /// Valida la configuración base del contenedor.
    /// </summary>
    private bool Validate()
    {
        if (cameraController == null)
        {
            Debug.LogError("CameraController no asignado.");
            return false;
        }

        if (followTarget == null)
        {
            Debug.LogError("FollowTarget no asignado.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Valida que el target cinemático sea utilizable por el sistema.
    /// </summary>
    private bool ValidateCinematicTarget(Transform focusTarget)
    {
        if (focusTarget == null)
        {
            Debug.LogWarning("Focus target es null.");
            return false;
        }

        if (followTarget != null && focusTarget == followTarget.transform)
        {
            Debug.LogError("No puedes usar el pivot como target.");
            return false;
        }

        return true;
    }

    #endregion
}