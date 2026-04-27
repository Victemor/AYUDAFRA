using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Define el tipo de seguimiento del target de cámara.
/// </summary>
public enum FollowMode
{
    Transform,
    MouseWorldPosition
}

/// <summary>
/// Representa el punto de seguimiento de la cámara.
/// Puede seguir un Transform o una exploración por input de mouse acumulado.
/// Controla además los límites del escenario.
/// </summary>
[DefaultExecutionOrder(100)]
public sealed class CameraTargetProxy : MonoBehaviour
{
    #region Serialized Fields

    [Header("Follow Target")]

    [SerializeField]
    [Tooltip("Modo de seguimiento del target.")]
    private FollowMode followMode = FollowMode.Transform;

    [SerializeField]
    [Tooltip("Transform que será seguido en modo Transform.")]
    private Transform targetToFollow;

    [SerializeField]
    [Tooltip("Offset aplicado respecto al objetivo.")]
    private Vector3 followOffset = Vector3.zero;

    [SerializeField]
    [Tooltip("Velocidad de interpolación del seguimiento y exploración.")]
    private float followSpeed = 5f;

    [Header("Mouse Exploration")]

    [SerializeField]
    [Tooltip("Sensibilidad del desplazamiento por input de mouse.")]
    private float mouseSensitivity = 2.5f;

    [SerializeField]
    [Tooltip("Delay antes de permitir movimiento en exploración.")]
    private float movementDelay = 0.4f;

    [SerializeField]
    [Tooltip("Distancia mínima al objetivo para hacer snap exacto y evitar quedarse corto cerca de los límites.")]
    private float positionSnapThreshold = 0.01f;

    [SerializeField]
    [Tooltip("Si está activo, la exploración se bloquea cuando el puntero está sobre elementos de UI. Si está desactivado, la cámara seguirá moviéndose incluso sobre UI.")]
    private bool blockMouseExplorationOverUi = true;

    [Header("Mouse World Settings")]

    [SerializeField]
    [Tooltip("Máscara de capas usadas para proyectar el mouse al mundo.")]
    private LayerMask groundLayerMask;

    [SerializeField]
    [Tooltip("Altura fija usada para fallback si no hay colisión.")]
    private float fallbackY = 0f;

    [Header("World Limits")]

    [SerializeField]
    [Tooltip("Límite mínimo en XZ que el target puede alcanzar.")]
    private Vector2 minPositionXZ = new Vector2(-10f, -5f);

    [SerializeField]
    [Tooltip("Límite máximo en XZ que el target puede alcanzar.")]
    private Vector2 maxPositionXZ = new Vector2(10f, 5f);

    [Header("Debug Visual")]

    [SerializeField]
    [Tooltip("Radio del gizmo de depuración.")]
    private float gizmoRadius = 0.3f;

    [SerializeField]
    [Tooltip("Color del gizmo de depuración.")]
    private Color gizmoColor = Color.yellow;

    #endregion

    #region Private Fields

    private Camera mainCamera;
    private Vector3 runtimeOffset;
    private bool ignoreBounds;
    private bool isMouseInputEnabled = true;
    private float mouseInputStartTime;
    private Vector3 mouseAnchorPosition;
    private Vector2 accumulatedMouseMovement;

    #endregion

    #region Unity Methods

    /// <summary>
    /// Inicializa referencias de runtime y sincroniza el estado inicial de exploración.
    /// </summary>
    private void Awake()
    {
        mainCamera = Camera.main;
        SyncMouseFollowState();
    }

    /// <summary>
    /// Actualiza la posición del target según el modo activo y aplica límites cuando corresponde.
    /// </summary>
    private void LateUpdate()
    {
        Vector3 desiredPosition = transform.position;

        switch (followMode)
        {
            case FollowMode.Transform:
                if (targetToFollow == null)
                {
                    return;
                }

                desiredPosition = GetFollowPositionForTarget(targetToFollow, runtimeOffset);
                break;

            case FollowMode.MouseWorldPosition:
                desiredPosition = ResolveMouseExplorationPosition();
                break;
        }

        desiredPosition = ClampToBounds(desiredPosition);

        Vector3 interpolatedPosition = Vector3.Lerp(
            transform.position,
            desiredPosition,
            Time.deltaTime * followSpeed
        );

        interpolatedPosition = ClampToBounds(interpolatedPosition);

        if ((interpolatedPosition - desiredPosition).sqrMagnitude <= positionSnapThreshold * positionSnapThreshold)
        {
            transform.position = desiredPosition;
            return;
        }

        transform.position = interpolatedPosition;
    }

    #endregion

    #region Bounds

    /// <summary>
    /// Aplica restricciones de límites configurados en el plano XZ
    /// sin considerar el estado runtime de ignoreBounds.
    /// </summary>
    private Vector3 ClampToConfiguredBounds(Vector3 position)
    {
        position.x = Mathf.Clamp(position.x, minPositionXZ.x, maxPositionXZ.x);
        position.z = Mathf.Clamp(position.z, minPositionXZ.y, maxPositionXZ.y);
        return position;
    }

    /// <summary>
    /// Aplica restricciones de límites en el plano XZ cuando están habilitadas.
    /// </summary>
    private Vector3 ClampToBounds(Vector3 position)
    {
        if (ignoreBounds)
        {
            return position;
        }

        return ClampToConfiguredBounds(position);
    }

    /// <summary>
    /// Retorna una posición válida dentro de los límites configurados.
    /// Si los límites están ignorados, retorna la posición sin modificar.
    /// </summary>
    public Vector3 GetClampedPosition(Vector3 position)
    {
        return ClampToBounds(position);
    }

    /// <summary>
    /// Retorna una posición válida dentro de los límites configurados,
    /// ignorando temporalmente el estado runtime de ignoreBounds.
    /// </summary>
    public Vector3 GetClampedPositionIgnoringRuntimeFlag(Vector3 position)
    {
        return ClampToConfiguredBounds(position);
    }

    /// <summary>
    /// Retorna la posición actual del proxy ajustada al área válida de exploración.
    /// Se usa como destino de transición al volver desde modo cinemático.
    /// </summary>
    public Vector3 GetExplorationEntryPosition()
    {
        return ClampToConfiguredBounds(transform.position);
    }

    /// <summary>
    /// Permite definir límites dinámicamente.
    /// </summary>
    public void SetBounds(Vector2 min, Vector2 max)
    {
        if (min.x > max.x || min.y > max.y)
        {
            Debug.LogWarning("Bounds inválidos.");
            return;
        }

        minPositionXZ = min;
        maxPositionXZ = max;
    }

    /// <summary>
    /// Activa o desactiva el uso de límites del follow target.
    /// </summary>
    public void SetIgnoreBounds(bool ignore)
    {
        ignoreBounds = ignore;

        if (!ignoreBounds)
        {
            transform.position = ClampToConfiguredBounds(transform.position);
        }
    }

    #endregion

    #region Mouse Exploration

    /// <summary>
    /// Indica si el puntero actual está interactuando con UI.
    /// </summary>
    private bool IsPointerOverUi()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        if (Input.touchCount > 0)
        {
            return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        }

        return EventSystem.current.IsPointerOverGameObject();
    }

    /// <summary>
    /// Indica si el input de exploración debe bloquearse por interacción con UI.
    /// </summary>
    private bool ShouldBlockExplorationInput()
    {
        return blockMouseExplorationOverUi && IsPointerOverUi();
    }

    /// <summary>
    /// Resuelve la posición objetivo de exploración a partir del input acumulado del mouse.
    /// Los límites del mundo son la única restricción espacial efectiva.
    /// </summary>
    private Vector3 ResolveMouseExplorationPosition()
    {
        if (!isMouseInputEnabled)
        {
            return transform.position;
        }

        if (Time.time < mouseInputStartTime)
        {
            return transform.position;
        }

        if (ShouldBlockExplorationInput())
        {
            return transform.position;
        }

        float mouseX = Input.GetAxis("Mouse X");
        float mouseZ = Input.GetAxis("Mouse Y");

        accumulatedMouseMovement += new Vector2(mouseX, mouseZ) * mouseSensitivity;

        Vector3 desiredPosition = mouseAnchorPosition + new Vector3(
            accumulatedMouseMovement.x,
            0f,
            accumulatedMouseMovement.y
        );

        Vector3 clampedDesiredPosition = ClampToBounds(desiredPosition);

        accumulatedMouseMovement = new Vector2(
            clampedDesiredPosition.x - mouseAnchorPosition.x,
            clampedDesiredPosition.z - mouseAnchorPosition.z
        );

        return clampedDesiredPosition;
    }

    /// <summary>
    /// Re-sincroniza el ancla y acumulación interna de exploración
    /// usando la posición actual del proxy.
    /// </summary>
    public void SyncMouseFollowState()
    {
        mouseAnchorPosition = transform.position;
        accumulatedMouseMovement = Vector2.zero;
        mouseInputStartTime = Time.time + movementDelay;
    }

    /// <summary>
    /// Activa o desactiva el procesamiento de input de exploración.
    /// </summary>
    public void SetMouseInputEnabled(bool enabled)
    {
        isMouseInputEnabled = enabled;

        if (!enabled)
        {
            accumulatedMouseMovement = Vector2.zero;
            return;
        }

        mouseInputStartTime = Time.time + movementDelay;
    }

    /// <summary>
    /// Activa o desactiva en runtime el bloqueo de exploración cuando el puntero está sobre UI.
    /// </summary>
    public void SetBlockMouseExplorationOverUi(bool enabled)
    {
        blockMouseExplorationOverUi = enabled;
    }

    #endregion

    #region Target Resolution

    /// <summary>
    /// Retorna la posición de seguimiento para un target específico.
    /// </summary>
    public Vector3 GetFollowPositionForTarget(Transform target, Vector3 offset)
    {
        if (target == null)
        {
            return transform.position;
        }

        return target.position + followOffset + offset;
    }

    #endregion

    #region Mouse World Settings

    /// <summary>
    /// Convierte la posición del mouse de pantalla a mundo sobre un plano horizontal.
    /// Conservado para compatibilidad y depuración.
    /// </summary>
    private Vector3 GetMouseWorldPosition()
    {
        if (mainCamera == null)
        {
            return transform.position;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 500f, groundLayerMask))
        {
            return hit.point;
        }

        Plane plane = new Plane(Vector3.up, new Vector3(0f, fallbackY, 0f));

        if (plane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }

        return transform.position;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Asigna un target de seguimiento para modo Transform.
    /// </summary>
    public void SetTarget(Transform newTarget, Vector3 offset = default)
    {
        if (newTarget == null)
        {
            Debug.LogWarning("Target es null.");
            return;
        }

        if (newTarget == transform)
        {
            Debug.LogError("No puedes asignar el followTarget a sí mismo.");
            return;
        }

        followMode = FollowMode.Transform;
        targetToFollow = newTarget;
        runtimeOffset = offset;
    }

    /// <summary>
    /// Activa el modo de exploración por mouse.
    /// </summary>
    public void SetMouseFollow()
    {
        followMode = FollowMode.MouseWorldPosition;
        targetToFollow = null;
        runtimeOffset = Vector3.zero;
        SyncMouseFollowState();
    }

    #endregion

    #region Gizmos

    /// <summary>
    /// Dibuja ayudas visuales de depuración para la posición actual y los límites.
    /// </summary>
    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, gizmoRadius);

        Gizmos.color = Color.red;

        Vector3 center = new Vector3(
            (minPositionXZ.x + maxPositionXZ.x) * 0.5f,
            transform.position.y,
            (minPositionXZ.y + maxPositionXZ.y) * 0.5f
        );

        Vector3 size = new Vector3(
            Mathf.Abs(maxPositionXZ.x - minPositionXZ.x),
            0.1f,
            Mathf.Abs(maxPositionXZ.y - minPositionXZ.y)
        );

        Gizmos.DrawWireCube(center, size);
#endif
    }

    #endregion
}