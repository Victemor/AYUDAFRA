using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Permite mover un personaje hacia un punto calculado por click,
/// bloqueando el movimiento si hay sprites visuales sobre el cursor.
/// No requiere colliders en objetos.
/// Incluye sistema de debug para inspección.
/// </summary>
public sealed class PlayerClickMovement : MonoBehaviour
{
    #region Serialized Fields

    [Header("Movimiento")]

    [SerializeField]
    [Tooltip("Velocidad de movimiento del personaje.")]
    private float moveSpeed = 5f;

    [Header("Raycast")]

    [SerializeField]
    [Tooltip("Cámara usada para proyectar el click al mundo.")]
    private Camera mainCamera;

    [SerializeField]
    [Tooltip("Capas válidas para el movimiento (suelo).")]
    private LayerMask groundLayer;

    [Header("Bloqueo Visual (Sin Colliders)")]

    [SerializeField]
    [Tooltip("Capas de sprites que bloquean el click.")]
    private LayerMask visualBlockingLayer;

    [SerializeField]
    [Tooltip("Radio en pantalla para detección de sprites (en píxeles).")]
    private float screenDetectionRadius = 40f;

    [Header("Debug")]

    [SerializeField]
    [Tooltip("Activa logs de debug para detección de sprites.")]
    private bool debugDetection = true;

    [SerializeField]
    [Tooltip("Dibuja rayos en escena para debug.")]
    private bool debugDrawRay = true;

    #endregion

    #region Private Fields

    private Vector3 targetPosition;
    private bool isMoving;
    private bool isMovementEnabled = true;

    private SpriteRenderer[] cachedSprites;

    #endregion

    #region Unity Methods

    /// <summary>
    /// Cachea todos los sprites al iniciar.
    /// </summary>
    private void Start()
    {
        cachedSprites = FindObjectsOfType<SpriteRenderer>();
    }

    /// <summary>
    /// Detecta input y ejecuta movimiento.
    /// </summary>
    private void Update()
    {
        if (!isMovementEnabled) return;

        if (Input.GetMouseButtonDown(0))
        {
            if (IsPointerOverUI()) return;

            HandleClick();
        }

        if (isMoving)
        {
            MoveTowardsTarget();
        }
    }

    #endregion

    #region Core Logic

    /// <summary>
    /// Verifica si el puntero está sobre UI.
    /// </summary>
    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    /// <summary>
    /// Detecta si hay un sprite visible bajo el mouse usando proyección en pantalla.
    /// </summary>
    private bool IsMouseOverBlockingSprite()
    {
        if (mainCamera == null || cachedSprites == null) return false;

        Vector2 mouseScreen = Input.mousePosition;

        SpriteRenderer topSprite = null;
        float bestDistance = float.MaxValue;

        foreach (var sprite in cachedSprites)
        {
            if (sprite == null) continue;

            if (((1 << sprite.gameObject.layer) & visualBlockingLayer) == 0)
                continue;

            // 🔥 Más preciso que transform.position
            Vector3 screenPos = mainCamera.WorldToScreenPoint(sprite.bounds.center);

            if (screenPos.z < 0f) continue;

            float distance = Vector2.Distance(mouseScreen, screenPos);

            if (distance < screenDetectionRadius)
            {
                if (topSprite == null ||
                    sprite.sortingOrder > topSprite.sortingOrder ||
                    (sprite.sortingOrder == topSprite.sortingOrder && distance < bestDistance))
                {
                    topSprite = sprite;
                    bestDistance = distance;
                }
            }
        }

        if (debugDetection)
        {
            if (topSprite != null)
            {
                Debug.Log(
                    $"[Mouse Detection] Sprite: {topSprite.name} | SortingOrder: {topSprite.sortingOrder} | Distancia: {bestDistance:F2}"
                );
            }
            else
            {
                Debug.Log("[Mouse Detection] No hay sprite bloqueando.");
            }
        }

        return topSprite != null;
    }

    /// <summary>
    /// Maneja el click del usuario.
    /// </summary>
    private void HandleClick()
    {
        if (mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (debugDrawRay)
        {
            Debug.DrawRay(ray.origin, ray.direction * 50f, Color.yellow, 1f);
        }

        // 🔴 Bloqueo por sprite
        if (IsMouseOverBlockingSprite())
        {
            if (debugDetection)
            {
                Debug.Log("[Click] Bloqueado por sprite.");
            }
            return;
        }

        // 🟢 Movimiento al suelo
        if (Physics.Raycast(ray, out RaycastHit hit, 800f, groundLayer))
        {
            if (debugDetection)
            {
                Debug.Log($"[Click] Movimiento hacia: {hit.point}");
            }

            targetPosition = hit.point;
            targetPosition.y = transform.position.y;

            isMoving = true;
        }
    }

    /// <summary>
    /// Mueve el personaje hacia el destino.
    /// </summary>
    private void MoveTowardsTarget()
    {
        Vector3 currentPosition = transform.position;

        Vector3 newPosition = Vector3.MoveTowards(
            currentPosition,
            targetPosition,
            moveSpeed * Time.deltaTime
        );

        transform.position = newPosition;

        if ((currentPosition - targetPosition).sqrMagnitude < 0.001f)
        {
            transform.position = targetPosition;
            isMoving = false;
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Deshabilita completamente el movimiento.
    /// </summary>
    public void DisableMovement()
    {
        isMovementEnabled = false;
        isMoving = false;
    }

    /// <summary>
    /// Habilita nuevamente el movimiento.
    /// </summary>
    public void EnableMovement()
    {
        isMovementEnabled = true;
    }

    #endregion
}