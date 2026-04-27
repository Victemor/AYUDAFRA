using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game;
using Game.Core;
using Game.Data;

namespace Game.Runtime
{
    [DisallowMultipleComponent]
    public sealed class NarrativeNotificationToConsciousnessBridge : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Resolver encargado de elegir el texto final del mensaje.")]
        private NarrativeNotificationResolver resolver;

        [SerializeField]
        [Tooltip("Si está activo, el objeto no se destruye entre escenas.")]
        private bool dontDestroyOnLoad = true;

        [SerializeField]
        [Tooltip("Tipos de notificación que NO deben mostrarse ni encolarse.")]
        private List<NarrativeNotificationType> blockedNotificationTypes = new();

        [SerializeField]
        [Tooltip("Segundos que espera una notificación antes de intentar escribirse.")]
        [Min(0f)]
        private float notificationDelay = 0.5f;

        [SerializeField]
        [Tooltip("Cantidad máxima de notificaciones pendientes.")]
        [Min(1)]
        private int maxPendingNotifications = 20;

        [SerializeField]
        [Tooltip("Si está activo, también escribe el mensaje en consola.")]
        private bool debugLog;

        private static NarrativeNotificationToConsciousnessBridge instance;

        private readonly Queue<UnlockNotificationMessage> pendingNotifications = new();
        private Coroutine pendingRoutine;

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
        }

        private void OnEnable()
        {
            GameEvents.OnUnlockNotification += HandleUnlockNotification;

            if (GamePlayStateController.Instance != null)
            {
                GamePlayStateController.Instance.StateChanged += HandleGameStateChanged;
            }
        }

        private void OnDisable()
        {
            GameEvents.OnUnlockNotification -= HandleUnlockNotification;

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

        private void HandleUnlockNotification(UnlockNotificationMessage message)
        {
            if (IsBlocked(message.NotificationType))
            {
                return;
            }

            EnqueueNotification(message);
            StartPendingRoutineIfNeeded();
        }

        private void HandleGameStateChanged(GamePlayState previousState, GamePlayState currentState)
        {
            if (!CanWriteNotificationNow())
            {
                return;
            }

            StartPendingRoutineIfNeeded();
        }

        private void EnqueueNotification(UnlockNotificationMessage message)
        {
            while (pendingNotifications.Count >= maxPendingNotifications)
            {
                pendingNotifications.Dequeue();
            }

            pendingNotifications.Enqueue(message);

            if (debugLog)
            {
                Debug.Log($"[NarrativeNotification] Queued: {message.NotificationType}", this);
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
                    ProcessNotification(message);
                }

                yield return null;
            }

            pendingRoutine = null;
        }

        private void ProcessNotification(UnlockNotificationMessage message)
        {
            if (resolver == null)
            {
                Debug.LogWarning("[NarrativeNotificationBridge] Resolver is not assigned.", this);
                return;
            }

            if (ConsciousnessSystem.Instance == null)
            {
                Debug.LogWarning("[NarrativeNotificationBridge] ConsciousnessSystem was not found.", this);
                return;
            }

            if (GameStateRepository.Instance == null)
            {
                Debug.LogWarning("[NarrativeNotificationBridge] GameStateRepository was not found.", this);
                return;
            }

            RuntimeContext runtimeContext = new RuntimeContext(GameStateRepository.Instance);
            string resolvedMessage = resolver.ResolveMessage(message, runtimeContext);

            if (string.IsNullOrWhiteSpace(resolvedMessage))
            {
                return;
            }

            if (debugLog)
            {
                Debug.Log($"[NarrativeNotification] {resolvedMessage}", this);
            }

            ConsciousnessSystem.Instance.AddThought(resolvedMessage);
        }

        private bool CanWriteNotificationNow()
        {
            if (GamePlayStateController.Instance == null)
            {
                return false;
            }

            GamePlayState currentState = GamePlayStateController.Instance.CurrentState;

            return currentState == GamePlayState.Menu ||
                   currentState == GamePlayState.Exploration;
        }

        private bool IsBlocked(NarrativeNotificationType type)
        {
            return blockedNotificationTypes != null &&
                   blockedNotificationTypes.Contains(type);
        }
    }
}