using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Ejecuta instrucciones de menú de forma secuencial basada en eventos.
/// </summary>
public class MenuTutorialSequencer : MonoBehaviour
{
    [System.Serializable]
    public class SequenceStep
    {
        public string instructionId;
        public float offsetY;
        public bool chainNext;
        [Header("Auto Hide Settings")]
        public bool autoHide;
        public float duration = 3f;
    }

    [Header("References")]
    private TutorialController tutorialController;

    [Header("Sequence")]
    [SerializeField] private List<SequenceStep> sequence;

    private int currentIndex = -1;
    private bool waitingForClose = false;
    private Tween autoHideTween; // Para cancelar el timer si se cierra manualmente


    private void OnDisable()
    {
        if (tutorialController != null)
        {
            Debug.Log($"<color=red>[MenuTutorialSequencer]</color> OnDisable: Desuscribiendo de eventos.");
            tutorialController.OnInstructionClosed -= HandleInstructionClosed;
        }
        autoHideTween?.Kill();
    }

    private void Start()
    {
        Debug.Log($"<color=cyan>[MenuTutorialSequencer]</color> Start: Iniciando secuenciador.");
        tutorialController = SystemsGameplay.Instance.GetTutorialController();
        
        if (tutorialController == null)
        {
            Debug.LogError("[MenuTutorialSequencer] ERROR: TutorialController no encontrado.");
            return;
        }

        tutorialController.OnInstructionClosed += HandleInstructionClosed;
        Debug.Log($"<color=cyan>[MenuTutorialSequencer]</color> Suscrito a OnInstructionClosed. Total pasos en secuencia: {sequence.Count}");
        
        DOVirtual.DelayedCall(0.5f, () =>
        {
            Debug.Log($"<color=cyan>[MenuTutorialSequencer]</color> DelayedCall ejecutada. Intentando iniciar secuencia.");
            PlayNextValid();
        });
    }

    /// <summary>
    /// Busca y ejecuta el siguiente step válido.
    /// </summary>
    private void PlayNextValid()
    {
        Debug.Log($"<color=yellow>[MenuTutorialSequencer]</color> PlayNextValid llamado. currentIndex: {currentIndex}, waitingForClose: {waitingForClose}");

        if (waitingForClose)
        {
            Debug.LogWarning("[MenuTutorialSequencer] PlayNextValid abortado: ya hay una instrucción activa esperando cierre.");
            return;
        }

        for (int i = currentIndex + 1; i < sequence.Count; i++)
        {
            var step = sequence[i];
            Debug.Log($"[MenuTutorialSequencer] Evaluando paso índice {i}: ID '{step.instructionId}'");

            if (string.IsNullOrEmpty(step.instructionId))
            {
                Debug.Log($"[MenuTutorialSequencer] Paso {i} saltado: ID vacío.");
                continue;
            }

            if (tutorialController.HasBeenShown(step.instructionId))
            {
                Debug.Log($"[MenuTutorialSequencer] Paso {i} ('{step.instructionId}') saltado: ya ha sido mostrada anteriormente.");
                continue;
            }

            currentIndex = i;
            waitingForClose = true;

            Debug.Log($"<color=green>[MenuTutorialSequencer]</color> EJECUTANDO: Mostrando instrucción '{step.instructionId}' en índice {i}.");
            tutorialController.ShowInstruction(step.instructionId, step.offsetY);

            // Lógica de Auto-Hide
            if (step.autoHide)
            {
                Debug.Log($"<color=magenta>[MenuTutorialSequencer]</color> AutoHide detectado. Se cerrará en {step.duration}s.");
                autoHideTween?.Kill(); // Limpiar por seguridad
                autoHideTween = DOVirtual.DelayedCall(step.duration, () =>
                {
                    Debug.Log($"<color=magenta>[MenuTutorialSequencer]</color> Tiempo agotado. Llamando a HideTutorial para: {step.instructionId}");
                    tutorialController.HideTutorial(step.instructionId);
                });
            }

            return;
        }

        Debug.Log("<color=white>[MenuTutorialSequencer]</color> No se encontraron más pasos válidos en la secuencia.");
        currentIndex = sequence.Count;
    }

    /// <summary>
    /// Se dispara cuando el usuario cierra una instrucción.
    /// </summary>
    private void HandleInstructionClosed(string id)
    {
        Debug.Log($"<color=orange>[MenuTutorialSequencer]</color> EVENTO RECIBIDO: Instrucción cerrada -> {id}");
        
        // Si se cerró manualmente o por evento, matamos cualquier timer de auto-hide pendiente
        autoHideTween?.Kill();

        DOVirtual.DelayedCall(1f, () =>
        {
            if (currentIndex < 0 || currentIndex >= sequence.Count)
            {
                Debug.Log($"[MenuTutorialSequencer] DelayedCall: currentIndex ({currentIndex}) fuera de rango. Finalizando.");
                return;
            }

            var step = sequence[currentIndex];
            Debug.Log($"[MenuTutorialSequencer] Procesando cierre de paso {currentIndex} ('{step.instructionId}'). chainNext es: {step.chainNext}");

            waitingForClose = false;
            
            if (step.chainNext)
            {
                Debug.Log($"[MenuTutorialSequencer] Encadenando al siguiente paso automáticamente.");
                PlayNextValid();
            }
            else
            {
                Debug.Log($"[MenuTutorialSequencer] No se encadena (chainNext = false). Esperando disparador externo.");
            }
        });
    }
}
