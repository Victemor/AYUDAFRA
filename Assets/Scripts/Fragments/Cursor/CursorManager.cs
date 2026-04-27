using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.CursorSystem
{
    /// <summary>
    /// Sistema global de cursor.
    /// 
    /// Responsabilidades:
    /// - Persistir entre escenas.
    /// - Resolver el cursor activo a partir de estado base + overrides.
    /// - Permitir ocultar o mostrar el cursor globalmente.
    /// - Aplicar el cursor real mediante la API de Unity.
    /// 
    /// No conoce lógica concreta de inventario, botones o interactuables.
    /// Esos sistemas solo registran solicitudes temporales.
    /// </summary>
    [DefaultExecutionOrder(-250)]
    public sealed class CursorManager : MonoBehaviour
    {
        #region Singleton

        /// <summary>
        /// Instancia global del sistema de cursor.
        /// </summary>
        public static CursorManager Instance { get; private set; }

        #endregion

        #region Nested Types

        /// <summary>
        /// Solicitud temporal de cursor.
        /// </summary>
        private struct CursorRequest
        {
            public Object Owner;
            public CursorType Type;
            public int Priority;
        }

        #endregion

        #region Serialized Fields

        [Header("Catalog")]

        [SerializeField]
        [Tooltip("Catálogo central con las texturas y hotspots de los cursores.")]
        private CursorCatalog cursorCatalog;

        [Header("Behavior")]

        [SerializeField]
        [Tooltip("Si está activo, el sistema persistirá entre escenas.")]
        private bool dontDestroyOnLoad = true;

        [SerializeField]
        [Tooltip("Activa logs de diagnóstico del sistema de cursor.")]
        private bool verboseLogging = false;

        #endregion

        #region Private Fields

        private readonly List<CursorRequest> requests = new();
        private readonly HashSet<Object> hiddenRequesters = new();

        private CursorType currentAppliedType = CursorType.None;
        private bool currentVisibleState = true;
        private GamePlayStateController cachedGamePlayStateController;

        #endregion

        #region Unity Methods

        /// <summary>
        /// Inicializa el singleton y el catálogo.
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }

            if (cursorCatalog != null)
            {
                cursorCatalog.Initialize();
            }

            TryBindGamePlayStateController();
            Refresh();
        }

        /// <summary>
        /// Se suscribe a eventos al habilitarse.
        /// </summary>
        private void OnEnable()
        {
            TryBindGamePlayStateController();
            SubscribeToGameplayState();
        }

        /// <summary>
        /// Se desuscribe de eventos al deshabilitarse.
        /// </summary>
        private void OnDisable()
        {
            UnsubscribeFromGameplayState();
        }

        /// <summary>
        /// Intenta re-enlazar el GamePlayStateController si aún no existe referencia.
        /// Útil al cambiar de escena o cuando el orden de inicialización varía.
        /// </summary>
        private void Update()
        {
            if (cachedGamePlayStateController == null && GamePlayStateController.Instance != null)
            {
                TryBindGamePlayStateController();
                SubscribeToGameplayState();
                Refresh();
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Registra una solicitud temporal de cursor para un owner.
        /// Si el owner ya tenía una solicitud, esta se actualiza.
        /// </summary>
        /// <param name="owner">Objeto que solicita el cursor.</param>
        /// <param name="type">Tipo de cursor requerido.</param>
        /// <param name="priority">Prioridad de la solicitud. Un valor más alto gana.</param>
        public void SetRequest(Object owner, CursorType type, int priority)
        {
            if (owner == null)
            {
                return;
            }

            for (int i = 0; i < requests.Count; i++)
            {
                if (requests[i].Owner == owner)
                {
                    requests[i] = new CursorRequest
                    {
                        Owner = owner,
                        Type = type,
                        Priority = priority
                    };

                    Refresh();
                    return;
                }
            }

            requests.Add(new CursorRequest
            {
                Owner = owner,
                Type = type,
                Priority = priority
            });

            Refresh();
        }

        /// <summary>
        /// Elimina la solicitud temporal de cursor asociada a un owner.
        /// </summary>
        /// <param name="owner">Owner a remover.</param>
        public void ClearRequest(Object owner)
        {
            if (owner == null)
            {
                return;
            }

            for (int i = requests.Count - 1; i >= 0; i--)
            {
                if (requests[i].Owner == owner)
                {
                    requests.RemoveAt(i);
                }
            }

            Refresh();
        }

        /// <summary>
        /// Solicita ocultar completamente el cursor.
        /// Mientras exista al menos un owner ocultándolo, el cursor permanecerá invisible.
        /// </summary>
        /// <param name="owner">Owner que solicita el ocultamiento.</param>
        public void Hide(Object owner)
        {
            if (owner == null)
            {
                return;
            }

            hiddenRequesters.Add(owner);
            Refresh();
        }

        /// <summary>
        /// Libera una solicitud previa de ocultamiento.
        /// </summary>
        /// <param name="owner">Owner que deja de ocultar el cursor.</param>
        public void Show(Object owner)
        {
            if (owner == null)
            {
                return;
            }

            hiddenRequesters.Remove(owner);
            Refresh();
        }

        /// <summary>
        /// Fuerza una reevaluación del cursor activo.
        /// </summary>
        public void Refresh()
        {
            Debug.Log($"[CURSOR] Visible={hiddenRequesters.Count == 0} | HiddenCount={hiddenRequesters.Count}");
            CleanupInvalidOwners();

            bool shouldBeVisible = hiddenRequesters.Count == 0;
            CursorType resolvedType = ResolveCursorType();

            ApplyVisibility(shouldBeVisible);
            ApplyCursor(resolvedType);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Determina el cursor final a aplicar.
        /// </summary>
        /// <returns>Tipo de cursor resuelto.</returns>
        private CursorType ResolveCursorType()
        {
            CursorRequest? bestRequest = null;

            for (int i = 0; i < requests.Count; i++)
            {
                CursorRequest request = requests[i];

                if (request.Owner == null)
                {
                    continue;
                }

                if (!bestRequest.HasValue || request.Priority > bestRequest.Value.Priority)
                {
                    bestRequest = request;
                }
            }

            if (bestRequest.HasValue)
            {
                return bestRequest.Value.Type;
            }

            return ResolveBaseCursorFromGameState();
        }

        /// <summary>
        /// Determina el cursor base a partir del estado jugable actual.
        /// </summary>
        /// <returns>Cursor base del contexto actual.</returns>
        private CursorType ResolveBaseCursorFromGameState()
        {
            if (cachedGamePlayStateController == null)
            {
                return CursorType.Default;
            }

            if (cachedGamePlayStateController.IsDraggingObject)
            {
                return CursorType.DragItem;
            }

            if (cachedGamePlayStateController.IsFreeExploration)
            {
                return CursorType.Explorer;
            }

            return CursorType.Default;
        }

        /// <summary>
        /// Aplica la visibilidad del cursor únicamente si cambió.
        /// </summary>
        /// <param name="visible">Nuevo estado visible.</param>
        private void ApplyVisibility(bool visible)
        {
            if (currentVisibleState == visible)
            {
                return;
            }

            currentVisibleState = visible;
            Cursor.visible = visible;

            Log($"Visibility changed: {visible}");
        }

        /// <summary>
        /// Aplica la textura del cursor únicamente si cambió.
        /// </summary>
        /// <param name="type">Tipo de cursor a aplicar.</param>
        private void ApplyCursor(CursorType type)
        {
            if (currentAppliedType == type)
            {
                return;
            }

            currentAppliedType = type;

            if (cursorCatalog == null || !cursorCatalog.TryGet(type, out CursorDefinition definition))
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                Log($"Cursor applied: {type} | Fallback to system cursor.");
                return;
            }

            Cursor.SetCursor(definition.Texture, definition.Hotspot, definition.Mode);
            Log($"Cursor applied: {type}");
        }

        /// <summary>
        /// Remueve owners destruidos o inválidos de las colecciones internas.
        /// </summary>
        private void CleanupInvalidOwners()
        {
            for (int i = requests.Count - 1; i >= 0; i--)
            {
                if (requests[i].Owner == null)
                {
                    requests.RemoveAt(i);
                }
            }

            hiddenRequesters.RemoveWhere(owner => owner == null);
        }

        /// <summary>
        /// Intenta enlazar la referencia al controlador global de estado jugable.
        /// </summary>
        private void TryBindGamePlayStateController()
        {
            cachedGamePlayStateController = GamePlayStateController.Instance;
        }

        /// <summary>
        /// Se suscribe a cambios de snapshot del estado jugable.
        /// </summary>
        private void SubscribeToGameplayState()
        {
            if (cachedGamePlayStateController == null)
            {
                return;
            }

            cachedGamePlayStateController.SnapshotChanged -= HandleGamePlaySnapshotChanged;
            cachedGamePlayStateController.SnapshotChanged += HandleGamePlaySnapshotChanged;
        }

        /// <summary>
        /// Se desuscribe de cambios del estado jugable.
        /// </summary>
        private void UnsubscribeFromGameplayState()
        {
            if (cachedGamePlayStateController == null)
            {
                return;
            }

            cachedGamePlayStateController.SnapshotChanged -= HandleGamePlaySnapshotChanged;
        }

        /// <summary>
        /// Reacciona a cambios del estado jugable refrescando el cursor base.
        /// </summary>
        /// <param name="snapshot">Snapshot actualizado.</param>
        private void HandleGamePlaySnapshotChanged(GamePlayStateSnapshot snapshot)
        {
            Refresh();
        }

        /// <summary>
        /// Escribe logs si la verbosidad está activa.
        /// </summary>
        /// <param name="message">Mensaje a registrar.</param>
        private void Log(string message)
        {
            if (!verboseLogging)
            {
                return;
            }

            Debug.Log($"[CURSOR MANAGER] {message}");
        }

        #endregion
    }
}