using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Joystick virtual invisible, dinámico y analógico.
/// El centro del joystick nace donde el jugador presiona y desaparece al soltar.
/// </summary>
public sealed class VirtualJoystickInput : MonoBehaviour
{
    #region Events

    /// <summary>
    /// Emite el input analógico actual del joystick.
    /// X representa lateralidad y Y avance/retroceso.
    /// </summary>
    public event Action<Vector2> OnJoystickChanged;

    #endregion

    #region Inspector

    [Header("Activation")]

    [SerializeField]
    [Tooltip("Si está activo, el joystick puede comenzar en cualquier parte de la pantalla.")]
    private bool allowFullScreenActivation = true;

    [SerializeField]
    [Tooltip("Fracción horizontal permitida si Allow Full Screen Activation está apagado. 0.5 equivale a mitad izquierda.")]
    [Range(0.1f, 1f)]
    private float activationAreaWidthRatio = 1f;

    [SerializeField]
    [Tooltip("Fracción vertical permitida si Allow Full Screen Activation está apagado. 1 permite toda la altura.")]
    [Range(0.1f, 1f)]
    private float activationAreaHeightRatio = 1f;

    [Header("Joystick")]

    [SerializeField]
    [Tooltip("Radio máximo del joystick invisible en píxeles.")]
    private float joystickRadiusPx = 140f;

    [SerializeField]
    [Tooltip("Zona muerta central en píxeles. Mientras más alto, menos responde cerca del centro.")]
    private float deadZonePx = 22f;

    [SerializeField]
    [Tooltip("Curva de respuesta del joystick. X = distancia normalizada desde el centro. Y = intensidad final enviada al movimiento.")]
    private AnimationCurve responseCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.3f, 0.05f),
        new Keyframe(0.6f, 0.4f),
        new Keyframe(1f, 1f)
    );

    [SerializeField]
    [Tooltip("Suavizado del input. Valores más altos hacen que el cambio de dirección sea menos reactivo.")]
    [Range(0f, 0.95f)]
    private float smoothingFactor = 0.18f;

    [SerializeField]
    [Tooltip("Diferencia mínima necesaria para volver a emitir input. Reduce ruido innecesario entre frames.")]
    private float emitThreshold = 0.0001f;

    [Header("Editor")]

    [SerializeField]
    [Tooltip("Permite probar el joystick invisible con mouse en Editor y Standalone.")]
    private bool useMouseInEditor = true;

    [SerializeField]
    [Tooltip("Muestra logs básicos del joystick para depuración.")]
    private bool debugInput;

    #endregion

    #region Runtime

    private bool isActive;
    private Vector2 centerScreenPosition;
    private Vector2 rawInput;
    private Vector2 smoothedInput;
    private Vector2 lastEmittedInput;

    #endregion

    #region Properties

    /// <summary>
    /// Indica si el joystick está activo.
    /// </summary>
    public bool IsActive => isActive;

    /// <summary>
    /// Último input suavizado.
    /// </summary>
    public Vector2 CurrentInput => smoothedInput;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (useMouseInEditor)
        {
            UpdateMouseInput();
        }
        else
        {
            UpdateTouchInput();
        }
#else
        UpdateTouchInput();
#endif

        UpdateSmoothedInput();
    }

    private void OnDisable()
    {
        ForceDeactivate();
    }

    #endregion

    #region Input

    /// <summary>
    /// Lee el input táctil del dispositivo.
    /// </summary>
    private void UpdateTouchInput()
    {
        if (Touchscreen.current == null)
        {
            ForceDeactivate();
            return;
        }

        var touch = Touchscreen.current.primaryTouch;
        Vector2 screenPosition = touch.position.ReadValue();

        if (touch.press.wasPressedThisFrame)
        {
            TryActivate(screenPosition);
            return;
        }

        if (touch.press.isPressed && isActive)
        {
            UpdateRawInput(screenPosition);
            return;
        }

        if (!touch.press.isPressed && isActive)
        {
            Deactivate();
        }
    }

    /// <summary>
    /// Lee el mouse para pruebas dentro del editor.
    /// </summary>
    private void UpdateMouseInput()
    {
        if (Mouse.current == null)
        {
            ForceDeactivate();
            return;
        }

        Vector2 screenPosition = Mouse.current.position.ReadValue();

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryActivate(screenPosition);
            return;
        }

        if (Mouse.current.leftButton.isPressed && isActive)
        {
            UpdateRawInput(screenPosition);
            return;
        }

        if (!Mouse.current.leftButton.isPressed && isActive)
        {
            Deactivate();
        }
    }

    #endregion

    #region Joystick Logic

    /// <summary>
    /// Activa el joystick usando la posición actual como centro temporal.
    /// </summary>
    private void TryActivate(Vector2 screenPosition)
    {
        if (!IsInsideActivationArea(screenPosition))
        {
            return;
        }

        isActive = true;
        centerScreenPosition = screenPosition;
        rawInput = Vector2.zero;
        smoothedInput = Vector2.zero;
        lastEmittedInput = Vector2.zero;

        if (debugInput)
        {
            Debug.Log($"[VirtualJoystickInput] Activado en {screenPosition}");
        }

        EmitInput(Vector2.zero, force: true);
    }

    /// <summary>
    /// Calcula dirección e intensidad desde el centro temporal hasta la posición actual.
    /// </summary>
    private void UpdateRawInput(Vector2 screenPosition)
    {
        Vector2 delta = screenPosition - centerScreenPosition;
        float distance = delta.magnitude;

        if (distance <= deadZonePx)
        {
            rawInput = Vector2.zero;
            return;
        }

        float usableRadius = Mathf.Max(1f, joystickRadiusPx - deadZonePx);
        float normalizedDistance = Mathf.Clamp01((distance - deadZonePx) / usableRadius);
        float intensity = Mathf.Clamp01(responseCurve.Evaluate(normalizedDistance));

        rawInput = delta.normalized * intensity;
    }

    /// <summary>
    /// Desactiva el joystick y emite input cero.
    /// </summary>
    private void Deactivate()
    {
        if (!isActive)
        {
            return;
        }

        isActive = false;
        rawInput = Vector2.zero;
        smoothedInput = Vector2.zero;

        if (debugInput)
        {
            Debug.Log("[VirtualJoystickInput] Desactivado");
        }

        EmitInput(Vector2.zero, force: true);
    }

    /// <summary>
    /// Desactiva el joystick sin depender del estado previo del dispositivo de entrada.
    /// </summary>
    private void ForceDeactivate()
    {
        if (!isActive && rawInput == Vector2.zero && smoothedInput == Vector2.zero)
        {
            return;
        }

        isActive = false;
        rawInput = Vector2.zero;
        smoothedInput = Vector2.zero;

        EmitInput(Vector2.zero, force: true);
    }

    /// <summary>
    /// Suaviza el input y lo emite mientras el joystick está activo.
    /// </summary>
    private void UpdateSmoothedInput()
    {
        if (!isActive)
        {
            return;
        }

        float response = 1f - Mathf.Clamp01(smoothingFactor);
        smoothedInput = Vector2.Lerp(smoothedInput, rawInput, response);

        if (smoothedInput.sqrMagnitude < emitThreshold)
        {
            smoothedInput = Vector2.zero;
        }

        EmitInput(smoothedInput, force: false);
    }

    /// <summary>
    /// Emite input únicamente cuando el cambio es relevante.
    /// </summary>
    private void EmitInput(Vector2 input, bool force)
    {
        if (!force && (input - lastEmittedInput).sqrMagnitude < emitThreshold)
        {
            return;
        }

        lastEmittedInput = input;
        OnJoystickChanged?.Invoke(input);
    }

    /// <summary>
    /// Valida si la posición puede activar el joystick.
    /// </summary>
    private bool IsInsideActivationArea(Vector2 screenPosition)
    {
        if (allowFullScreenActivation)
        {
            return true;
        }

        float maxX = Screen.width * activationAreaWidthRatio;
        float maxY = Screen.height * activationAreaHeightRatio;

        return screenPosition.x <= maxX && screenPosition.y <= maxY;
    }

    #endregion
}