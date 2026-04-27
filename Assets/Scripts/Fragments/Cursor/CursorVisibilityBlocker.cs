using UnityEngine;

namespace Game.CursorSystem
{
    /// <summary>
    /// Oculta el cursor mientras este componente esté habilitado.
    /// </summary>
    public sealed class CursorVisibilityBlocker : MonoBehaviour
    {
        /// <summary>
        /// Solicita ocultar el cursor al habilitarse.
        /// </summary>
        private void OnEnable()
        {
            CursorManager.Instance?.Hide(this);
        }

        /// <summary>
        /// Libera el ocultamiento al deshabilitarse.
        /// </summary>
        private void OnDisable()
        {
            CursorManager.Instance?.Show(this);
        }
    }
}