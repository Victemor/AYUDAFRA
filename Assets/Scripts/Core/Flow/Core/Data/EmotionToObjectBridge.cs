using UnityEngine;
using Game.Runtime;
using Game.Data;

/// <summary>
/// Puente entre el sistema emocional (UI/Feedback)
/// y el sistema narrativo (ObjectRuntimeData).
/// 
/// Su responsabilidad es traducir el resultado emocional del jugador
/// en datos persistentes por estado dentro del runtime.
/// </summary>
public class EmotionToObjectBridge : MonoBehaviour
{
    #region Serialized Fields

    [Header("Referencias")]

    [SerializeField]
    [Tooltip("Controlador de secuencia emocional que emite el resultado del jugador.")]
    private EmotionSequenceController sequenceController;

    [SerializeField]
    [Tooltip("Memoria objetivo donde se aplicará la emoción.")]
    private MemoryDefinition memory;

    [SerializeField]
    [Tooltip("Objeto dentro de la memoria que recibirá la emoción.")]
    private ObjectDefinition targetObject;

    #endregion

    #region Private Fields

    private ObjectRuntimeData runtimeObject;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        Initialize();
        Subscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Inicializa referencias runtime necesarias.
    /// </summary>
    private void Initialize()
    {
        if (sequenceController == null)
        {
            Debug.LogError("[EmotionBridge] SequenceController NULL");
            return;
        }

        var repo = GameStateRepository.Instance;

        if (repo == null)
        {
            Debug.LogError("[EmotionBridge] GameStateRepository NULL");
            return;
        }

        var memoryRuntime = repo.GetMemory(memory);

        if (memoryRuntime == null)
        {
            Debug.LogError($"[EmotionBridge] MemoryRuntime NULL → {memory?.name}");
            return;
        }

        runtimeObject = memoryRuntime.GetObject(targetObject);

        if (runtimeObject == null)
        {
            Debug.LogError($"[EmotionBridge] ObjectRuntime NULL → {targetObject?.name}");
        }
    }

    #endregion

    #region Events

    /// <summary>
    /// Se suscribe al resultado emocional del jugador.
    /// </summary>
    private void Subscribe()
    {
        if (sequenceController != null)
        {
            sequenceController.OnEmotionSelected += OnEmotionSelected;
        }
    }

    /// <summary>
    /// Se desuscribe para evitar fugas de memoria.
    /// </summary>
    private void Unsubscribe()
    {
        if (sequenceController != null)
        {
            sequenceController.OnEmotionSelected -= OnEmotionSelected;
        }
    }

    #endregion

    #region Core Logic

    /// <summary>
    /// Callback principal cuando el jugador selecciona una emoción.
    /// </summary>
    private void OnEmotionSelected(EmotionResult result)
    {
        if (runtimeObject == null)
            return;

        // Ignorar neutro (decisión de diseño)
        if (result.IsNeutral)
        {
            Debug.Log("[EmotionBridge] Neutral → no se aplica emoción");
            return;
        }

        int stateIndex = runtimeObject.GetStateIndex();

        var emotion = ConvertEmotion(result.Emotion);
        var intensity = ConvertIntensity(result.Intensity);

        runtimeObject.SetEmotionForState(
            stateIndex,
            new RuntimeEmotionState(emotion, intensity)
        );

        Debug.Log($"[EmotionBridge] Applied → {emotion} | {intensity} | StateIndex: {stateIndex}");
    }

    #endregion

    #region Converters

    /// <summary>
    /// Convierte emoción del sistema global al sistema narrativo.
    /// Se usa mapping explícito para evitar dependencias implícitas.
    /// </summary>
    private Game.Data.EmotionType ConvertEmotion(EmotionData data)
    {
        switch (data.EmotionType)
        {
            case EmotionTypeGlobal.Guilt:
                return Game.Data.EmotionType.Guilt;

            case EmotionTypeGlobal.Repression:
                return Game.Data.EmotionType.Repression;

            case EmotionTypeGlobal.Nostalgia:
                return Game.Data.EmotionType.Nostalgia;

            case EmotionTypeGlobal.Confusion:
                return Game.Data.EmotionType.Confusion;

            case EmotionTypeGlobal.Acceptance:
                return Game.Data.EmotionType.Acceptance;

            case EmotionTypeGlobal.Neutral:
                return Game.Data.EmotionType.Neutral;

            default:
                Debug.LogWarning($"[EmotionBridge] Emotion no mapeada: {data.EmotionType}");
                return Game.Data.EmotionType.Neutral;
        }
    }

    /// <summary>
    /// Convierte valor continuo (0-1) a nivel discreto de intensidad narrativa.
    /// </summary>
    private IntensityLevel ConvertIntensity(float value)
    {
        if (value < 0.33f) return IntensityLevel.Low;
        if (value < 0.66f) return IntensityLevel.Medium;
        return IntensityLevel.High;
    }

    #endregion
}