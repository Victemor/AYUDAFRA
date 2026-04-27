using UnityEngine;
using UnityEngine.EventSystems;
using Game.Data;

namespace Game.Runtime
{
    /// <summary>
    /// Representación visual y física de un objeto draggable en escena.
    /// </summary>
    public sealed class DraggableItemWorldInstance : MonoBehaviour
    {
        [Header("Binding")]

        [SerializeField]
        [Tooltip("Definición única del objeto draggable.")]
        private DraggableItemDefinition itemDefinition;

        [SerializeField]
        [Tooltip("Si está activo, esta instancia se considera colocada manualmente en escena desde editor.")]
        private bool isSceneAuthored = true;

        [Header("Debug")]

        [SerializeField]
        [Tooltip("Activa logs detallados del ciclo de vida del objeto draggable.")]
        private bool debugLogs = true;

        private Collider[] cachedColliders;
        private string owningSlotId;
        private bool isCarryVisual;
        private bool hasRegistered;

        /// <summary>
        /// Definición asociada.
        /// </summary>
        public DraggableItemDefinition ItemDefinition => itemDefinition;

        /// <summary>
        /// ID del slot dueño cuando la instancia está dentro de un slot físico.
        /// </summary>
        public string OwningSlotId => owningSlotId;

        /// <summary>
        /// Devuelve true si esta instancia representa el visual del item en mano.
        /// </summary>
        public bool IsCarryVisual => isCarryVisual;

        /// <summary>
        /// Devuelve true si esta instancia viene de la escena.
        /// </summary>
        public bool IsSceneAuthored => isSceneAuthored;

        private void Awake()
        {
            cachedColliders = GetComponentsInChildren<Collider>(true);

            Log(
                $"Awake -> Item: {(itemDefinition != null ? itemDefinition.Id : "NULL")} | " +
                $"SceneAuthored: {isSceneAuthored}"
            );
        }

        private void Start()
        {
            TryRegisterToInventorySystem();
        }

        private void OnEnable()
        {
            if (!hasRegistered)
            {
                TryRegisterToInventorySystem();
            }
        }

        private void OnDestroy()
        {
            if (DraggableInventorySystem.Instance != null)
            {
                Log("OnDestroy -> UnregisterWorldInstance");
                DraggableInventorySystem.Instance.UnregisterWorldInstance(this);
            }
        }

        private void OnMouseOver()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (Input.GetMouseButtonDown(1))
            {
                Log(
                    $"Right click detected -> " +
                    $"{(itemDefinition != null ? itemDefinition.Id : "NULL")}"
                );

                if (DraggableInventorySystem.Instance != null)
                {
                    DraggableInventorySystem.Instance.TryPickupWorldInstance(this);
                }
                else
                {
                    Debug.LogWarning(
                        $"[DraggableItemWorldInstance] DraggableInventorySystem.Instance es null en {name}.",
                        this
                    );
                }
            }
        }

        /// <summary>
        /// Inicializa o sobreescribe la definición de esta instancia.
        /// </summary>
        public void Initialize(DraggableItemDefinition definition, bool sceneAuthored)
        {
            itemDefinition = definition;
            isSceneAuthored = sceneAuthored;
            owningSlotId = string.Empty;
            isCarryVisual = false;
            hasRegistered = false;

            Log(
                $"Initialize -> Item: {(itemDefinition != null ? itemDefinition.Id : "NULL")} | " +
                $"SceneAuthored: {isSceneAuthored}"
            );
        }

        /// <summary>
        /// Configura la instancia como objeto suelto en mundo.
        /// </summary>
        public void ConfigureAsWorldItem()
        {
            owningSlotId = string.Empty;
            isCarryVisual = false;
            SetCollidersEnabled(true);

            Log("ConfigureAsWorldItem");
        }

        /// <summary>
        /// Configura la instancia como objeto en mano.
        /// </summary>
        public void ConfigureAsCarryItem()
        {
            owningSlotId = string.Empty;
            isCarryVisual = true;
            SetCollidersEnabled(false);

            Log("ConfigureAsCarryItem");
        }

        /// <summary>
        /// Configura la instancia como objeto colocado en slot.
        /// </summary>
        public void ConfigureAsPlacedInSlot(string slotId, bool locked)
        {
            owningSlotId = slotId ?? string.Empty;
            isCarryVisual = false;
            SetCollidersEnabled(!locked);

            Log(
                $"ConfigureAsPlacedInSlot -> SlotId: {owningSlotId} | Locked: {locked}"
            );
        }

        private void TryRegisterToInventorySystem()
        {
            if (hasRegistered)
            {
                return;
            }

            if (itemDefinition == null)
            {
                Debug.LogError(
                    $"[DraggableItemWorldInstance] ItemDefinition no asignado en {name}.",
                    this
                );
                return;
            }

            if (DraggableInventorySystem.Instance == null)
            {
                Log("TryRegisterToInventorySystem -> InventorySystem aún no existe.");
                return;
            }

            hasRegistered = true;

            Log(
                $"TryRegisterToInventorySystem -> RegisterWorldInstance | " +
                $"Item: {itemDefinition.Id}"
            );

            DraggableInventorySystem.Instance.RegisterWorldInstance(this);
        }

        private void SetCollidersEnabled(bool enabled)
        {
            if (cachedColliders == null)
            {
                return;
            }

            for (int i = 0; i < cachedColliders.Length; i++)
            {
                if (cachedColliders[i] != null)
                {
                    cachedColliders[i].enabled = enabled;
                }
            }
        }

        private void Log(string message)
        {
            if (!debugLogs)
            {
                return;
            }

            Debug.Log($"[DraggableItemWorldInstance] {message}", this);
        }
    }
}