using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Serialization;
using Game.Conditions;
using Game.Data;

/// <summary>
/// Contenedor serializable de parámetros para una acción de fragmento.
///
/// Estrategia de localización con retrocompatibilidad:
/// - Los campos <c>string</c> originales (<c>text</c>, <c>dialogText</c>) se conservan
///   para que no se pierdan los textos ya escritos en el Inspector.
/// - Los campos <c>LocalizedString</c> (<c>localizedText</c>, <c>localizedDialogText</c>)
///   son opcionales. Cuando están asignados tienen prioridad sobre los string planos.
/// - Los handlers aplican la lógica: si LocalizedString está asignado → ruta localizada;
///   si no → ruta raw (string plano directo).
/// </summary>
[System.Serializable]
public class FragmentAction
{
    [Tooltip("Tipo de acción a ejecutar.")]
    [SerializeField] private FragmentActionType actionType;

    [Tooltip("Tiempo de espera después de ejecutar esta acción.")]
    [SerializeField, Range(0f, 60f)] private float waitAfter = 0f;

    [FormerlySerializedAs("intensyRain")]
    [Tooltip("Intensidad objetivo de la lluvia.")]
    [SerializeField, Range(0, 400)] private int rainIntensity;

    [FormerlySerializedAs("timeTransitionRainIn")]
    [Tooltip("Tiempo de transición de la lluvia.")]
    [SerializeField, Range(0f, 10f)] private float rainTransitionTime = 0f;

    [FormerlySerializedAs("timeTransitionRainOut")]
    [Tooltip("Tiempo legacy de transición de salida de lluvia.")]
    [SerializeField, Range(0f, 10f)] private float rainTransitionOutTime = 0f;

    [FormerlySerializedAs("timeHoldRain")]
    [Tooltip("Tiempo legacy de espera asociado a lluvia.")]
    [SerializeField, Range(0f, 20f)] private float rainHoldTime = 0f;

    [FormerlySerializedAs("intensyWind")]
    [Tooltip("Intensidad objetivo del viento.")]
    [SerializeField, Range(0f, 10f)] private float windIntensity = 0f;

    [FormerlySerializedAs("timeWindTransition")]
    [Tooltip("Tiempo de transición del viento.")]
    [SerializeField, Range(0f, 10f)] private float windTransitionTime = 0f;

    [Tooltip("Objeto sprite afectado por la acción.")]
    [SerializeField] private VisualObjectSpriteController targetSpriteObject;

    [FormerlySerializedAs("timeTransitionSpriteObject")]
    [Tooltip("Tiempo de transición del sprite.")]
    [SerializeField, Range(0f, 10f)] private float spriteTransitionTime = 0f;

    [ColorUsage(true, true)]
    [Tooltip("Color objetivo para el material del sprite.")]
    [SerializeField] private Color emissiveColor = Color.white;

    [Tooltip("Indica si el fade del material también afecta partículas.")]
    [SerializeField] private bool fadeMaterialParticles = false;

    [Tooltip("Objeto de fuego afectado por la acción.")]
    [SerializeField] private FireController targetFireObject;

    [FormerlySerializedAs("intensyFire")]
    [Tooltip("Intensidad objetivo del fuego.")]
    [SerializeField, Range(0f, 0.6f)] private float fireIntensity = 0f;

    [SerializeField] private float fireHoldTime = 0f;

    [FormerlySerializedAs("timeTransitionFireIn")]
    [Tooltip("Tiempo de transición de encendido del fuego.")]
    [SerializeField, Range(0f, 10f)] private float fireTransitionInTime = 0f;

    [FormerlySerializedAs("timeTransitionFireOut")]
    [Tooltip("Tiempo de transición de apagado del fuego.")]
    [SerializeField, Range(0f, 10f)] private float fireTransitionOutTime = 0f;

    [FormerlySerializedAs("waitTime")]
    [Tooltip("Tiempo legacy usado por acciones antiguas de WaitTime.")]
    [SerializeField, Range(0f, 60f)] private float legacyWaitTime = 0f;

    [Tooltip("Intensidad objetivo del bloom.")]
    [SerializeField, Range(0f, 10f)] private float bloomIntensity = 0f;

    [Tooltip("Tint del bloom.")]
    [SerializeField] private Color bloomTint = Color.white;

    [Tooltip("Tiempo de transición del bloom.")]
    [SerializeField, Range(0f, 10f)] private float bloomTransitionTime = 0f;

    [FormerlySerializedAs("halfAnimationModeFootprint")]
    [Tooltip("Indica si la animación de huellas usa el modo reducido.")]
    [SerializeField] private bool useHalfFootprintAnimation = false;

    [Tooltip("Controlador de huellas afectado por la acción.")]
    [SerializeField] private FootprintPathController footprintPathController;

    [FormerlySerializedAs("speedFootprint")]
    [Tooltip("Velocidad de reproducción de las huellas.")]
    [SerializeField, Range(0f, 3f)] private float footprintSpeed = 1f;

    [Header("Thought — Texto del pensamiento")]
    [TextArea]
    [Tooltip("Texto del pensamiento (fallback). Se usa cuando Localized Text no está asignado.")]
    [SerializeField] private string text;

    [Tooltip("(Opcional) Clave de localización del pensamiento. Si está asignada, tiene prioridad sobre el texto de arriba.")]
    [SerializeField] private LocalizedString localizedText;

    [Header("Condiciones opcionales")]
    [Tooltip("Si no se cumplen, la acción se omite.")]
    [SerializeField] private List<ConditionGroup> conditions = new();

    [Header("Input Config")]
    [Tooltip("Tipo de input a esperar.")]
    [SerializeField] private InputType inputType;

    [FormerlySerializedAs("key")]
    [Tooltip("Tecla específica si el tipo de input es SpecificKey.")]
    [SerializeField] private KeyCode specificKey;

    [Header("Dialog System")]
    [TextArea]
    [Tooltip("Texto del diálogo (fallback). Se usa cuando Localized Dialog Text no está asignado.")]
    [SerializeField] private string dialogText;

    [Tooltip("(Opcional) Clave de localización del diálogo. Si está asignada, tiene prioridad sobre el texto de arriba.")]
    [SerializeField] private LocalizedString localizedDialogText;

    [Tooltip("Controller que maneja el diálogo.")]
    [SerializeField] private DialogueController dialogController;

    [Tooltip("Punto exacto donde debe aparecer el diálogo.")]
    [SerializeField] private Transform dialogPoint;

    [Header("Camera Cinematic Target Mode")]
    [Tooltip("Modo de resolución del target cinemático.")]
    [SerializeField] private CinematicTargetMode cinematicTargetMode;

    [Tooltip("Target cinemático cuando el modo es por Transform.")]
    [SerializeField] private Transform cinematicTarget;

    [Tooltip("Posición manual cuando el modo es ManualPosition.")]
    [SerializeField] private Vector3 cinematicManualPosition;

    [Tooltip("Offset de cámara aplicado al target cinemático.")]
    [SerializeField] private Vector3 cinematicOffset;

    [Header("Cinematic Actor")]
    [Tooltip("Duración total del movimiento del rig.")]
    [SerializeField] private float cinematicMoveDuration = 5f;

    [Header("Camera Transition")]
    [FormerlySerializedAs("cameraModeTransitionDelay")]
    [Tooltip("Duración de transición usada por acciones de cámara.")]
    [SerializeField] private float cameraTransitionTime = 0f;

    [Header("Weather")]
    [Tooltip("Perfil de clima a aplicar.")]
    [SerializeField] private WeatherProfile weatherProfile;

    [Header("Emotion Selection")]
    [Tooltip("Controlador de secuencia emocional.")]
    [SerializeField] private EmotionSequenceController emotionSequenceController;

    [Tooltip("Emoción A.")]
    [SerializeField] private EmotionData emotionA;

    [Tooltip("Emoción B.")]
    [SerializeField] private EmotionData emotionB;

    [Tooltip("Memoria donde guardar el resultado.")]
    [SerializeField] private MemoryDefinition emotionMemory;

    [Tooltip("Objeto donde guardar el resultado.")]
    [SerializeField] private ObjectDefinition emotionObject;

    [Header("Draggable Spawn")]
    [Tooltip("Lista ordenada de candidatos draggable.")]
    [SerializeField] private List<DraggableItemDefinition> draggableSpawnCandidates = new();

    [Tooltip("Punto donde se intentará spawnear el item seleccionado.")]
    [SerializeField] private Transform draggableSpawnPoint;

    [Header("Cinematic Zoom")]
    [Tooltip("Indica si esta acción debe sobrescribir el zoom al entrar en modo cinemático.")]
    [SerializeField] private bool overrideCinematicZoom = false;

    [Tooltip("Zoom mínimo permitido durante esta acción cinemática.")]
    [SerializeField] private float cinematicMinZoom = 3f;

    [Tooltip("Zoom máximo permitido durante esta acción cinemática.")]
    [SerializeField] private float cinematicMaxZoom = 10f;

    [Tooltip("Zoom inicial a aplicar al entrar en esta acción cinemática.")]
    [SerializeField] private float cinematicInitialZoom = 6f;

    [Header("Tutorial")]
    [Tooltip("ID de la instrucción de tutorial a mostrar u ocultar. " +
             "Debe coincidir exactamente con el ID en TutorialController.")]
    [SerializeField] private string tutorialId;

    [Tooltip("Desplazamiento vertical del panel de tutorial. " +
             "Úsalo para mover el tutorial arriba o abajo y evitar que tape elementos de UI.")]
    [SerializeField] private float tutorialOffsetY = 0f;

    // ── Properties ──────────────────────────────────────────────────────────

    public FragmentActionType ActionType => actionType;
    public float WaitAfter => waitAfter;
    public int RainIntensity => rainIntensity;
    public float RainTransitionTime => rainTransitionTime;
    public float RainTransitionOutTime => rainTransitionOutTime;
    public float RainHoldTime => rainHoldTime;
    public float WindIntensity => windIntensity;
    public float WindTransitionTime => windTransitionTime;
    public VisualObjectSpriteController TargetSpriteObject => targetSpriteObject;
    public float SpriteTransitionTime => spriteTransitionTime;
    public Color EmissiveColor => emissiveColor;
    public bool FadeMaterialParticles => fadeMaterialParticles;
    public FireController TargetFireObject => targetFireObject;
    public float FireIntensity => fireIntensity;
    public float FireHoldTime => fireHoldTime;
    public float FireTransitionInTime => fireTransitionInTime;
    public float FireTransitionOutTime => fireTransitionOutTime;
    public float LegacyWaitTime => legacyWaitTime;
    public float BloomIntensity => bloomIntensity;
    public Color BloomTint => bloomTint;
    public float BloomTransitionTime => bloomTransitionTime;
    public bool UseHalfFootprintAnimation => useHalfFootprintAnimation;
    public FootprintPathController FootprintPathController => footprintPathController;
    public float FootprintSpeed => footprintSpeed;

    /// <summary>Texto plano del pensamiento (fallback si LocalizedText no está asignado).</summary>
    public string Text => text;

    /// <summary>Referencia de localización del pensamiento. Nulo o vacío = usar Text como fallback.</summary>
    public LocalizedString LocalizedText => localizedText;

    public IReadOnlyList<ConditionGroup> Conditions => conditions;
    public InputType InputType => inputType;
    public KeyCode SpecificKey => specificKey;

    /// <summary>Texto plano del diálogo (fallback si LocalizedDialogText no está asignado).</summary>
    public string DialogText => dialogText;

    /// <summary>Referencia de localización del diálogo. Nulo o vacío = usar DialogText como fallback.</summary>
    public LocalizedString LocalizedDialogText => localizedDialogText;

    public DialogueController DialogController => dialogController;
    public Transform DialogPoint => dialogPoint;
    public CinematicTargetMode CinematicTargetMode => cinematicTargetMode;
    public Transform CinematicTarget => cinematicTarget;
    public Vector3 CinematicManualPosition => cinematicManualPosition;
    public Vector3 CinematicOffset => cinematicOffset;
    public float CinematicMoveDuration => cinematicMoveDuration;
    public float CameraTransitionTime => cameraTransitionTime;
    public WeatherProfile WeatherProfile => weatherProfile;
    public EmotionSequenceController EmotionSequenceController => emotionSequenceController;
    public EmotionData EmotionA => emotionA;
    public EmotionData EmotionB => emotionB;
    public MemoryDefinition EmotionMemory => emotionMemory;
    public ObjectDefinition EmotionObject => emotionObject;
    public IReadOnlyList<DraggableItemDefinition> DraggableSpawnCandidates => draggableSpawnCandidates;
    public Transform DraggableSpawnPoint => draggableSpawnPoint;
    public bool OverrideCinematicZoom => overrideCinematicZoom;
    public float CinematicMinZoom => cinematicMinZoom;
    public float CinematicMaxZoom => cinematicMaxZoom;
    public float CinematicInitialZoom => cinematicInitialZoom;

    /// <summary>ID de la instrucción de tutorial a mostrar u ocultar.</summary>
    public string TutorialId => tutorialId;

    /// <summary>Desplazamiento vertical del panel de tutorial.</summary>
    public float TutorialOffsetY => tutorialOffsetY;
}