using UnityEngine;

namespace Game.CursorSystem
{
    /// <summary>
    /// Controla la visibilidad del cursor mientras esta escena o contexto esté activo.
    /// </summary>
    public sealed class SceneCursorVisibility : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Si está activo, el cursor permanecerá oculto mientras este componente esté habilitado.")]
        private bool hideCursor = true;

        private void OnEnable()
        {
            if (CursorManager.Instance == null)
            {
                return;
            }

            if (hideCursor)
            {
                CursorManager.Instance.Hide(this);
            }
            else
            {
                CursorManager.Instance.Show(this);
            }
        }

        private void OnDisable()
        {
            if (CursorManager.Instance == null)
            {
                return;
            }

            CursorManager.Instance.Show(this);
        }
    }
}