using UnityEngine;
using System.Collections;

/// <summary>
/// Orquestador principal de la ejecución de Action Behaviors.
/// 
/// Responsabilidades:
/// - Iniciar y gestionar el ciclo de vida de ejecución de acciones.
/// - Exponer controladores globales a los ejecutores sin acoplarlos directamente.
/// - Gestionar pausas lógicas del flujo (no detiene coroutines, solo bloquea progresión).
/// 
/// Este controlador actúa como punto de entrada y contexto compartido
/// para el sistema de acciones.
/// </summary>
public class ActionBehaviorController : MonoBehaviour
{
    #region Serialized Fields        

    [Header("Action Behavior Executor")]

    [SerializeField]
    [Tooltip("Ejecutor encargado de procesar la secuencia de acciones.")]
    private ActionBehaviorExecutor actionBehaviorExecutor;

    [Header("Global Controllers")]

    [SerializeField]
    [Tooltip("Controlador del sistema de cámara (exploración/cinemática).")]
    private CameraSystemController cameraSystemController;

    [SerializeField]
    [Tooltip("Controlador del clima emocional del entorno.")]
    private EmotionalClimateController emotionalClimateController;

    [SerializeField]
    [Tooltip("Controlador de lluvia del entorno.")]
    private RainController rainController;

    [SerializeField]
    [Tooltip("Controlador de viento del entorno.")]
    private WindController windController;

    #endregion

    #region Properties (Global Access)

    /// <summary>
    /// Acceso de solo lectura al sistema de cámara.
    /// Permite a los ejecutores modificar comportamiento visual sin acoplarse a la escena.
    /// </summary>
    public CameraSystemController CameraSystem => cameraSystemController;

    /// <summary>
    /// Acceso de solo lectura al sistema de clima emocional.
    /// </summary>
    public EmotionalClimateController EmotionalClimate => emotionalClimateController;

    /// <summary>
    /// Acceso de solo lectura al sistema de lluvia.
    /// </summary>
    public RainController Rain => rainController;

    /// <summary>
    /// Acceso de solo lectura al sistema de viento.
    /// </summary>
    public WindController Wind => windController;

    #endregion

    #region Private Fields

    /// <summary>
    /// Referencia a la rutina principal de ejecución.
    /// </summary>
    private Coroutine actionFlowRoutine;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (actionBehaviorExecutor == null)
        {
            Debug.LogError("[ActionBehaviorController] No se ha asignado un ActionBehaviorExecutor válido.");
            enabled = false;
            return;
        }

        StartCoroutine(PlayActionBehavior());
    }

    #endregion

    #region Execution Flow

    /// <summary>
    /// Inicia la ejecución del flujo de acciones como coroutine.
    /// </summary>
    private IEnumerator PlayActionBehavior()
    {
        actionFlowRoutine = StartCoroutine(actionBehaviorExecutor.Execute(this));
        yield return actionFlowRoutine;
    }

    #endregion

}