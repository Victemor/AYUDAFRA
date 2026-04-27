using Game.Core;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Representa un objeto arrastrable dentro del sistema de drag.
/// Incluye validación para evitar interacción sobre UI.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DraggableObject : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField, Tooltip("Si está activo, el objeto no se puede arrastrar cuando el mouse está sobre UI.")]
    private bool blockDragOverUI = true;

    /// <summary>
    /// Offset entre el punto de impacto y la posición del objeto.
    /// </summary>
    public Vector3 DragOffset { get; private set; }

    /// <summary>
    /// Determina si el objeto puede iniciar drag en el estado actual.
    /// </summary>
    public bool CanStartDrag()
    {
        if (!blockDragOverUI)
            return true;

        // 🔑 Validación UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return false;

        return true;
    }

    /// <summary>
    /// Inicializa el arrastre calculando el offset.
    /// </summary>
    public void InitializeDrag(Vector3 hitPoint)
    {
        GamePlayStateController.Instance.EnterDraggingObject();
        DragOffset = transform.position - hitPoint;
    }

    /// <summary>
    /// Establece la nueva posición del objeto.
    /// </summary>
    public void SetPosition(Vector3 worldPosition)
    {
        GamePlayStateController.Instance.ExitDraggingObject();
        transform.position = worldPosition + DragOffset;
    }
}