using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Controlador de cámara isométrica basado en Cinemachine.
/// Permite control externo de zoom y exploración.
/// </summary>
public sealed class CinemachineController : MonoBehaviour
{
    #region Serialized Fields

    [Header("Cinemachine")]

    [SerializeField]
    [Tooltip("Referencia a la cámara Cinemachine.")]
    private CinemachineCamera cineCamera;

    [Header("Zoom")]

    [SerializeField]
    [Tooltip("Zoom mínimo permitido.")]
    private float minOrthoSize = 3f;

    [SerializeField]
    [Tooltip("Zoom máximo permitido.")]
    private float maxOrthoSize = 10f;

    [SerializeField]
    [Tooltip("Velocidad de interpolación del zoom.")]
    private float zoomSpeed = 5f;

    [SerializeField]
    [Tooltip("Zoom inicial por defecto si no es definido externamente.")]
    private float defaultInitialZoom = 6f;

    [Header("Sway")]

    [SerializeField]
    [Tooltip("Intensidad del sway en eje X.")]
    private float swayAmountX = 0.05f;

    [SerializeField]
    [Tooltip("Intensidad del sway en eje Z.")]
    private float swayAmountZ = 0.02f;

    [SerializeField]
    [Tooltip("Velocidad del sway procedural.")]
    private float swaySpeed = 1f;

    #endregion

    #region Private Fields

    /// <summary>
    /// Transform que Cinemachine sigue como pivot lógico de cámara.
    /// </summary>
    private Transform followTarget;

    /// <summary>
    /// Referencia al componente encargado de definir y aplicar límites del pivot.
    /// </summary>
    private CameraTargetProxy followTargetBounds;

    /// <summary>
    /// Tamaño ortográfico actual.
    /// </summary>
    private float currentOrthoSize;

    /// <summary>
    /// Indica si el modo exploración por mouse está activo.
    /// </summary>
    private bool isMouseExplorationEnabled = true;

    /// <summary>
    /// Cámara principal usada para proyecciones de pantalla a mundo.
    /// </summary>
    private Camera mainCamera;

    /// <summary>
    /// Indica si el zoom debe ser controlado externamente.
    /// </summary>
    private bool isZoomOverridden = false;

    /// <summary>
    /// Indica si temporalmente deben ignorarse los límites de zoom.
    /// </summary>
    private bool ignoreZoomLimits = false;

    #endregion

    #region Unity Methods

    /// <summary>
    /// Inicializa referencias y estado base del controlador.
    /// </summary>
    private void Start()
    {
        if (cineCamera == null || cineCamera.Follow == null)
        {
            Debug.LogError("Cinemachine mal configurado.");
            enabled = false;
            return;
        }

        mainCamera = Camera.main;
        followTarget = cineCamera.Follow;
        followTargetBounds = followTarget.GetComponent<CameraTargetProxy>();

        currentOrthoSize = Mathf.Clamp(defaultInitialZoom, minOrthoSize, maxOrthoSize);
        cineCamera.Lens.OrthographicSize = currentOrthoSize;
    }

    /// <summary>
    /// Ejecuta la lógica de zoom y sway al final del frame.
    /// </summary>
    private void LateUpdate()
    {
        HandleZoom();
        ApplyCameraSway();
    }

    #endregion

    #region Core Logic

    /// <summary>
    /// Maneja el zoom centrado en la posición del mouse.
    /// </summary>
    private void HandleZoom()
    {
        if (isZoomOverridden)
        {
            return;
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Abs(scroll) < 0.01f)
        {
            return;
        }

        if (mainCamera == null)
        {
            return;
        }

        Vector3 mouseBefore = GetMouseWorldPosition();

        float targetSize = currentOrthoSize - scroll * zoomSpeed;
        currentOrthoSize = Mathf.Clamp(targetSize, minOrthoSize, maxOrthoSize);

        cineCamera.Lens.OrthographicSize = currentOrthoSize;

        Vector3 mouseAfter = GetMouseWorldPosition();
        Vector3 delta = mouseBefore - mouseAfter;

        Vector3 targetPosition = followTarget.position + new Vector3(delta.x, 0f, delta.z);
        followTarget.position = ClampFollowTargetPosition(targetPosition);
    }

    /// <summary>
    /// Aplica un ligero movimiento procedural a la cámara.
    /// </summary>
    private void ApplyCameraSway()
    {
        if (followTarget == null)
        {
            return;
        }

        float swayX = Mathf.PerlinNoise(Time.time * swaySpeed, 0f) * 2f - 1f;
        float swayZ = Mathf.PerlinNoise(0f, Time.time * swaySpeed) * 2f - 1f;

        Vector3 swayOffset = new Vector3(
            swayX * swayAmountX,
            0f,
            swayZ * swayAmountZ
        );

        followTarget.localPosition += swayOffset * Time.deltaTime;
    }

    /// <summary>
    /// Convierte la posición del mouse a mundo sobre un plano horizontal.
    /// </summary>
    private Vector3 GetMouseWorldPosition()
    {
        if (mainCamera == null)
        {
            return followTarget != null ? followTarget.position : Vector3.zero;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.up, Vector3.zero);

        if (plane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }

        return followTarget != null ? followTarget.position : Vector3.zero;
    }

    /// <summary>
    /// Garantiza que la posición calculada para el pivot respete los límites configurados.
    /// </summary>
    private Vector3 ClampFollowTargetPosition(Vector3 position)
    {
        if (followTargetBounds == null)
        {
            return position;
        }

        return followTargetBounds.GetClampedPosition(position);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Define los límites mínimo y máximo de zoom permitidos.
    /// </summary>
    public void SetZoomLimits(float min, float max)
    {
        if (min > max)
        {
            Debug.LogWarning("Min zoom mayor que max zoom.");
            return;
        }

        minOrthoSize = min;
        maxOrthoSize = max;

        currentOrthoSize = Mathf.Clamp(currentOrthoSize, minOrthoSize, maxOrthoSize);
        cineCamera.Lens.OrthographicSize = currentOrthoSize;
    }

    /// <summary>
    /// Aplica instantáneamente un valor de zoom respetando los límites actuales.
    /// </summary>
    public void SetZoomInstant(float zoomValue)
    {
        float clamped = Mathf.Clamp(zoomValue, minOrthoSize, maxOrthoSize);

        currentOrthoSize = clamped;
        cineCamera.Lens.OrthographicSize = clamped;
    }

    /// <summary>
    /// Define suavemente un valor de zoom objetivo respetando los límites actuales.
    /// </summary>
    public void SetZoomSmooth(float zoomValue)
    {
        currentOrthoSize = Mathf.Clamp(zoomValue, minOrthoSize, maxOrthoSize);
    }

    /// <summary>
    /// Activa o desactiva la exploración por mouse.
    /// </summary>
    public void EnableMouseExploration(bool enabled)
    {
        isMouseExplorationEnabled = enabled;

        if (followTargetBounds != null)
        {
            followTargetBounds.SetMouseInputEnabled(enabled);
        }
    }

    /// <summary>
    /// Re-sincroniza el estado interno de exploración del proxy con la posición actual del pivot.
    /// </summary>
    public void SyncExplorationAnchor()
    {
        if (followTargetBounds != null)
        {
            followTargetBounds.SyncMouseFollowState();
        }
    }

    /// <summary>
    /// Configura los parámetros del sway procedural.
    /// </summary>
    public void SetSway(float amountX, float amountZ, float speed)
    {
        swayAmountX = Mathf.Max(0f, amountX);
        swayAmountZ = Mathf.Max(0f, amountZ);
        swaySpeed = Mathf.Max(0f, speed);
    }

    /// <summary>
    /// Define el zoom inicial o actual de la cámara respetando los límites vigentes.
    /// </summary>
    public void SetInitialZoom(float zoomValue)
    {
        float clamped = Mathf.Clamp(zoomValue, minOrthoSize, maxOrthoSize);

        currentOrthoSize = clamped;
        cineCamera.Lens.OrthographicSize = clamped;
    }

    /// <summary>
    /// Retorna el zoom actual de la cámara.
    /// </summary>
    public float GetCurrentZoom()
    {
        return currentOrthoSize;
    }

    /// <summary>
    /// Activa o desactiva el control externo del zoom.
    /// </summary>
    public void SetZoomOverride(bool enabled)
    {
        isZoomOverridden = enabled;
    }

    /// <summary>
    /// Fuerza el zoom directamente en el lente.
    /// </summary>
    public void SetZoomDirect(float zoomValue)
    {
        float value = ignoreZoomLimits
            ? zoomValue
            : Mathf.Clamp(zoomValue, minOrthoSize, maxOrthoSize);

        currentOrthoSize = value;
        cineCamera.Lens.OrthographicSize = value;
    }

    /// <summary>
    /// Permite ignorar temporalmente los límites de zoom.
    /// </summary>
    public void SetIgnoreZoomLimits(bool ignore)
    {
        ignoreZoomLimits = ignore;
    }

    #endregion
}