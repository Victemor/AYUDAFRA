using UnityEngine;
using UnityEngine.EventSystems;
using Game.Data;

namespace Game.Runtime
{
    /// <summary>
    /// Controlador del visual en mano para objetos draggable.
    /// En esta entrega no se usa representación visual en cursor.
    /// </summary>
    public sealed class DraggableCarryController : MonoBehaviour
    {
        public static DraggableCarryController Instance { get; private set; }

        [SerializeField]
        [Tooltip("Activa logs de depuración del carry controller.")]
        private bool debugLogs = true;

        private DraggableItemDefinition carriedDefinition;

        /// <summary>
        /// Definición actualmente seleccionada.
        /// </summary>
        public DraggableItemDefinition CarriedDefinition => carriedDefinition;

        /// <summary>
        /// Devuelve true si existe un item seleccionado.
        /// </summary>
        public bool HasCarryItem => carriedDefinition != null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Inicia el estado de carry lógico.
        /// No se usa en esta entrega representación visual del objeto en mano.
        /// </summary>
        public bool BeginCarry(DraggableItemDefinition definition)
        {
            if (definition == null)
            {
                return false;
            }

            carriedDefinition = definition;
            Log($"BeginCarry -> {definition.Id}");
            return true;
        }

        /// <summary>
        /// Limpia el estado lógico de carry.
        /// No se usa en esta entrega representación visual del objeto en mano.
        /// </summary>
        public void ClearCarryVisual()
        {
            if (carriedDefinition != null)
            {
                Log($"ClearCarryVisual -> {carriedDefinition.Id}");
            }

            carriedDefinition = null;
        }

        /// <summary>
        /// Consume el visual en mano.
        /// No se usa en esta entrega representación visual del objeto en mano.
        /// </summary>
        public DraggableItemWorldInstance ConsumeCarryVisual()
        {
            if (carriedDefinition != null)
            {
                Log($"ConsumeCarryVisual -> {carriedDefinition.Id} | No visual carry in this delivery");
            }

            carriedDefinition = null;
            return null;
        }

        private void Log(string message)
        {
            if (!debugLogs)
            {
                return;
            }

            Debug.Log($"[DraggableCarryController] {message}", this);
        }
    }
}