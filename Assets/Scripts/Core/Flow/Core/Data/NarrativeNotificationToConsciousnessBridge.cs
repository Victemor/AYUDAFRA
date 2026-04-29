using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using Game;
using Game.Core;
using Game.Data;
using Game.Save;

namespace Game.Runtime
{
    /// <summary>
    /// Puente entre el sistema de notificaciones narrativas y el sistema de consciencia.
    ///
    /// Módulo 2 — Gestor de Notificaciones Inteligente Anti-Spam:
    ///
    /// Regla 1 — Filtro de arranque:
    ///   Al iniciar, el bridge se coloca en modo "suprimido" hasta que
    ///   <see cref="SaveSystem.OnSaveLoaded"/> confirma que la carga terminó.
    ///   Cualquier notificación disparada durante la restauración del save
    ///   (objetos ya desbloqueados, estados pre-existentes) se descarta.
    ///
    /// Regla 2 — Anti-duplicación por tipo:
    ///   Si ya hay una notificación pendiente del mismo <see cref="NarrativeNotificationType"/>,
    ///   la nueva se descarta. Una sola acción que desbloquea 3 fragmentos
    ///   genera exactamente 1 notificación.
    ///
    /// Regla 3 — Cola ordenada:
    ///   Notificaciones de tipos distintos se procesan una tras otra,
    ///   nunca simultáneamente.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NarrativeNotificationToConsciousnessBridge : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Resolver encargado de elegir la referencia de localización del mensaje.")]
        private NarrativeNotificationResolver resolver;

        [SerializeField]
        [Tooltip("Si está activo, el objeto no se destruye entre escenas.")]
        private bool dontDestroyOnLoad = true;

        [SerializeField]
        [Tooltip("Tipos de notificación que nunca deben procesarse.")]
        private List<NarrativeNotificationType> blockedNotificationTypes = new();

        [SerializeField]
        [Tooltip("Segundos de espera antes de procesar la cola tras un cambio de estado.")]
        [Min(0f)]
        private float notificationDelay = 0.5f;

        [SerializeField]
        [Tooltip("Cantidad máxima de notificaciones distintas en cola.")]
        [Min(1)]
        private int maxPendingNotifications = 20;

        [SerializeField]
        [Tooltip("Activa logs de diagnóstico en consola.")]
        private bool debugLog;

        private static NarrativeNotificationToConsciousnessBridge instance;

        private readonly Queue<UnlockNotificationMessage> pendingNotifications = new();
        private Coroutine pendingRoutine;

        /// <summary>
        /// Mientras sea true, todas las notificaciones se descartan sin encolar.
        /// Se activa en Awake() y se libera cuando SaveSystem.OnSaveLoaded dispara,
        /// garantizando que el spam de "objetos ya desbloqueados al inicio" se ignora.
        /// </summary>
        private bool isSuppressedDuringLoad = true;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;

            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }

            if (resolver == null)
            {
                resolver = GetComponent<NarrativeNotificationResolver>();
            }

            // Suprime notificaciones hasta que el save esté completamente cargado.
            isSuppressedDuringLoad = true;
        }

        private void OnEnable()
        {
            GameEvents.OnUnlockNotification                 += HandleUnlockNotification;
            SaveSystem.OnSaveLoaded                         += HandleSaveLoaded;

            if (GamePlayStateController.Instance != null)
            {
                GamePlayStateController.Instance.StateChanged += HandleGameStateChanged;
            }
        }

        private void OnDisable()
        {
            GameEvents.OnUnlockNotification                 -= HandleUnlockNotification;
            SaveSystem.OnSaveLoaded                         -= HandleSaveLoaded;

            if (GamePlayStateController.Instance != null)
            {
                GamePlayStateController.Instance.StateChanged -= HandleGameStateChanged;
            }

            if (pendingRoutine != null)
            {
                StopCoroutine(pendingRoutine);
                pendingRoutine = null;
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Event Handlers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// El save terminó de cargar. A partir de ahora las notificaciones
        /// son válidas porque representan cambios reales del jugador.
        /// </summary>
        private void HandleSaveLoaded()
        {
            isSuppressedDuringLoad = false;

            if (debugLog)
            {
                Debug.Log("[NarrativeNotification] Supresión de arranque liberada.", this);
            }
        }

        private void HandleUnlockNotification(UnlockNotificationMessage message)
        {
            // Regla 1: descarta durante la carga inicial.
            if (isSuppressedDuringLoad)
            {
                if (debugLog)
                {
                    Debug.Log(
                        $"[NarrativeNotification] Descartada (supresión de arranque): {message.NotificationType}",
                        this);
                }
                return;
            }

            if (IsBlocked(message.NotificationType))
            {
                return;
            }

            EnqueueNotification(message);
            StartPendingRoutineIfNeeded();
        }

        private void HandleGameStateChanged(GamePlayState previousState, GamePlayState currentState)
        {
            if (isSuppressedDuringLoad)
            {
                return;
            }

            if (!CanWriteNotificationNow())
            {
                return;
            }

            StartPendingRoutineIfNeeded();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Queue
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Regla 2 — Anti-duplicación: descarta si ya hay una notificación del mismo tipo en cola.
        /// Regla 3 — Cola ordenada: diferentes tipos se encolan para procesarse uno a uno.
        /// </summary>
        private void EnqueueNotification(UnlockNotificationMessage message)
        {
            // Anti-duplicación por tipo.
            foreach (UnlockNotificationMessage pending in pendingNotifications)
            {
                if (pending.NotificationType == message.NotificationType)
                {
                    if (debugLog)
                    {
                        Debug.Log(
                            $"[NarrativeNotification] Descartada por duplicado de tipo '{message.NotificationType}'.",
                            this);
                    }
                    return;
                }
            }

            // Límite de cola.
            while (pendingNotifications.Count >= maxPendingNotifications)
            {
                pendingNotifications.Dequeue();
            }

            pendingNotifications.Enqueue(message);

            if (debugLog)
            {
                Debug.Log($"[NarrativeNotification] Encolada: {message.NotificationType} (cola: {pendingNotifications.Count})", this);
            }
        }

        private void StartPendingRoutineIfNeeded()
        {
            if (pendingRoutine != null)
            {
                return;
            }

            pendingRoutine = StartCoroutine(ProcessPendingNotifications());
        }

        private IEnumerator ProcessPendingNotifications()
        {
            if (notificationDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(notificationDelay);
            }
            else
            {
                yield return null;
            }

            while (pendingNotifications.Count > 0)
            {
                if (!CanWriteNotificationNow())
                {
                    pendingRoutine = null;
                    yield break;
                }

                UnlockNotificationMessage message = pendingNotifications.Dequeue();

                if (!IsBlocked(message.NotificationType))
                {
                    yield return ProcessNotification(message);
                }

                yield return null;
            }

            pendingRoutine = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Processing
        // ─────────────────────────────────────────────────────────────────────

        private IEnumerator ProcessNotification(UnlockNotificationMessage message)
        {
            if (resolver == null)
            {
                Debug.LogWarning("[NarrativeNotificationBridge] Resolver no asignado.", this);
                yield break;
            }

            if (ConsciousnessSystem.Instance == null)
            {
                Debug.LogWarning("[NarrativeNotificationBridge] ConsciousnessSystem no encontrado.", this);
                yield break;
            }

            if (GameStateRepository.Instance == null)
            {
                Debug.LogWarning("[NarrativeNotificationBridge] GameStateRepository no encontrado.", this);
                yield break;
            }

            RuntimeContext runtimeContext = new RuntimeContext(GameStateRepository.Instance);
            LocalizedString localizedString = resolver.ResolveMessage(message, runtimeContext);

            if (localizedString == null || localizedString.IsEmpty)
            {
                if (debugLog)
                {
                    Debug.Log(
                        $"[NarrativeNotification] Descartada (sin entrada localizable): {message.NotificationType}",
                        this);
                }
                yield break;
            }

            string tableName = localizedString.TableReference.TableCollectionName;
            string key       = localizedString.TableEntryReference.Key;
            long   keyId     = localizedString.TableEntryReference.KeyId;

            if (string.IsNullOrWhiteSpace(tableName))
            {
                Debug.LogWarning("[NarrativeNotificationBridge] LocalizedString sin tabla válida.", this);
                yield break;
            }

            if (string.IsNullOrWhiteSpace(key) && keyId <= 0)
            {
                Debug.LogWarning("[NarrativeNotificationBridge] LocalizedString sin clave válida.", this);
                yield break;
            }

            if (debugLog)
            {
                var handle = localizedString.GetLocalizedStringAsync();
                yield return handle;
                Debug.Log($"[NarrativeNotification] Pensamiento registrado: {handle.Result}", this);
            }

            ConsciousnessSystem.Instance.AddThought(localizedString);
        }

        private bool CanWriteNotificationNow()
        {
            if (GamePlayStateController.Instance == null)
            {
                return false;
            }

            GamePlayState currentState = GamePlayStateController.Instance.CurrentState;
            return currentState == GamePlayState.Menu || currentState == GamePlayState.Exploration;
        }

        private bool IsBlocked(NarrativeNotificationType type)
        {
            return blockedNotificationTypes != null &&
                   blockedNotificationTypes.Contains(type);
        }
    }
}