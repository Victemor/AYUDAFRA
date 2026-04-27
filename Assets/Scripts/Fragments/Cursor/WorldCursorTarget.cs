using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.CursorSystem
{
    /// <summary>
    /// Solicita un cursor temporal cuando el puntero está sobre un objeto del mundo.
    /// Requiere un Collider activo para recibir eventos de mouse de Unity.
    /// </summary>
    public sealed class WorldCursorTarget : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Cursor a mostrar cuando el puntero esté sobre este objeto.")]
        private CursorType hoverCursor = CursorType.Interact;

        [SerializeField]
        [Tooltip("Prioridad de esta solicitud frente a otras solicitudes simultáneas.")]
        private int priority = CursorPriority.WorldHover;

        [SerializeField]
        [Tooltip("Si está activo, no solicitará cursor cuando el puntero esté sobre UI.")]
        private bool ignoreWhenPointerOverUi = true;

        [SerializeField]
        [Tooltip("Si está activo, solo funciona durante exploración.")]
        private bool requireExplorationState = true;

        private bool isRequestingCursor;

        private void OnMouseEnter()
        {
            TryRequestCursor();
        }

        private void OnMouseOver()
        {
            TryRequestCursor();
        }

        private void OnMouseExit()
        {
            ClearCursorRequest();
        }

        private void OnDisable()
        {
            ClearCursorRequest();
        }

        private void TryRequestCursor()
        {
            if (!CanRequestCursor())
            {
                ClearCursorRequest();
                return;
            }

            isRequestingCursor = true;
            CursorManager.Instance?.SetRequest(this, hoverCursor, priority);
        }

        private void ClearCursorRequest()
        {
            if (!isRequestingCursor)
            {
                return;
            }

            isRequestingCursor = false;
            CursorManager.Instance?.ClearRequest(this);
        }

        private bool CanRequestCursor()
        {
            if (CursorManager.Instance == null)
            {
                return false;
            }

            if (ignoreWhenPointerOverUi &&
                EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject())
            {
                return false;
            }

            if (!requireExplorationState)
            {
                return true;
            }

            return Game.Core.GamePlayStateController.Instance != null &&
                   Game.Core.GamePlayStateController.Instance.IsInExploration;
        }
    }
}