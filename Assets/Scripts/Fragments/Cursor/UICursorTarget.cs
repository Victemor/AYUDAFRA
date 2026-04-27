using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.CursorSystem
{
    /// <summary>
    /// Solicita un cursor temporal cuando el puntero entra sobre un elemento UI.
    /// </summary>
    public sealed class UICursorTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField]
        [Tooltip("Cursor a solicitar mientras el puntero esté sobre este elemento UI.")]
        private CursorType hoverCursor = CursorType.Interact;

        [SerializeField]
        [Tooltip("Prioridad de esta solicitud frente a otras solicitudes simultáneas.")]
        private int priority = CursorPriority.UiHover;

        /// <summary>
        /// Solicita el cursor configurado.
        /// </summary>
        /// <param name="eventData">Datos del evento.</param>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (CursorManager.Instance == null)
            {
                return;
            }

            CursorManager.Instance.SetRequest(this, hoverCursor, priority);
        }

        /// <summary>
        /// Libera la solicitud al salir.
        /// </summary>
        /// <param name="eventData">Datos del evento.</param>
        public void OnPointerExit(PointerEventData eventData)
        {
            if (CursorManager.Instance == null)
            {
                return;
            }

            CursorManager.Instance.ClearRequest(this);
        }

        /// <summary>
        /// Libera la solicitud si el objeto se deshabilita.
        /// </summary>
        public void OnDisable()
        {
            if (CursorManager.Instance == null)
            {
                return;
            }

            CursorManager.Instance.ClearRequest(this);
        }
    }
}