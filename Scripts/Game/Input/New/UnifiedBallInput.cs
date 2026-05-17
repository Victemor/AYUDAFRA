using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Emite eventos táctiles crudos de un solo dedo.
/// No clasifica gestos ni valida longitudes: eso lo hace el consumidor.
///
/// Tres eventos:
///   <see cref="OnTouchBegan"/>   → dedo presiona la pantalla.
///   <see cref="OnTouchMoved"/>   → dedo se desplaza (emitido cada frame con el delta acumulado).
///   <see cref="OnTouchEnded"/>   → dedo levantado.
/// </summary>
public sealed class UnifiedBallInput : MonoBehaviour
{
    #region Types

    private enum InputReadMode
    {
        Auto      = 0,
        TouchOnly = 1,
        MouseOnly = 2,
    }

    #endregion

    #region Events

    /// <summary>Dedo presionado. Posición en píxeles de pantalla.</summary>
    public event Action<Vector2> OnTouchBegan;

    /// <summary>
    /// Dedo en movimiento. Emitido cada frame mientras el dedo está presionado.
    /// <paramref name="screenPos"/> = posición actual.
    /// <paramref name="screenDelta"/> = desplazamiento desde el frame anterior.
    /// </summary>
    public event Action<Vector2, Vector2> OnTouchMoved;

    /// <summary>Dedo levantado. Última posición conocida.</summary>
    public event Action<Vector2> OnTouchEnded;

    #endregion

    #region Inspector

    [SerializeField]
    [Tooltip("Auto: Touchscreen primero, Mouse como fallback en Editor.\n" +
             "TouchOnly: solo Touchscreen (builds de dispositivo).\n" +
             "MouseOnly: solo Mouse (testing en Editor).")]
    private InputReadMode inputReadMode = InputReadMode.Auto;

    [SerializeField]
    [Tooltip("Muestra en consola begin, move y end de cada toque.")]
    private bool debugInput;

    #endregion

    #region Runtime

    private bool    isTracking;
    private Vector2 previousPosition;

    #endregion

    #region Properties

    public bool IsTracking => isTracking;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        switch (inputReadMode)
        {
            case InputReadMode.TouchOnly: PollTouch(); break;
            case InputReadMode.MouseOnly: PollMouse(); break;
            default:                      PollAuto();  break;
        }
    }

    private void OnDisable()
    {
        if (isTracking) EndTracking(previousPosition);
    }

    #endregion

    #region Polling

    private void PollAuto()
    {
        if (TryPollTouch()) return;
#if UNITY_EDITOR || UNITY_STANDALONE
        PollMouse();
#else
        if (isTracking) EndTracking(previousPosition);
#endif
    }

    private void PollTouch()
    {
        if (!TryPollTouch() && isTracking) EndTracking(previousPosition);
    }

    private bool TryPollTouch()
    {
        if (Touchscreen.current == null) return false;
        var touch = Touchscreen.current.primaryTouch;

        if (touch.press.wasPressedThisFrame)  { BeginTracking(touch.position.ReadValue()); return true; }
        if (touch.press.isPressed && isTracking) { ContinueTracking(touch.position.ReadValue()); return true; }
        if (touch.press.wasReleasedThisFrame && isTracking) { EndTracking(touch.position.ReadValue()); return true; }

        return false;
    }

    private void PollMouse()
    {
        if (Mouse.current == null) { if (isTracking) EndTracking(previousPosition); return; }

        if (Mouse.current.leftButton.wasPressedThisFrame)           { BeginTracking(Mouse.current.position.ReadValue()); return; }
        if (Mouse.current.leftButton.isPressed && isTracking)        { ContinueTracking(Mouse.current.position.ReadValue()); return; }
        if (Mouse.current.leftButton.wasReleasedThisFrame && isTracking) { EndTracking(Mouse.current.position.ReadValue()); }
    }

    #endregion

    #region Tracking

    private void BeginTracking(Vector2 position)
    {
        isTracking       = true;
        previousPosition = position;

        if (debugInput) Debug.Log($"[UnifiedBallInput] Begin: {position}");
        OnTouchBegan?.Invoke(position);
    }

    private void ContinueTracking(Vector2 position)
    {
        Vector2 delta    = position - previousPosition;
        previousPosition = position;

        if (debugInput && delta.sqrMagnitude > 0.01f)
            Debug.Log($"[UnifiedBallInput] Move: {position} Δ:{delta}");

        OnTouchMoved?.Invoke(position, delta);
    }

    private void EndTracking(Vector2 position)
    {
        isTracking = false;

        if (debugInput) Debug.Log($"[UnifiedBallInput] End: {position}");
        OnTouchEnded?.Invoke(position);
    }

    #endregion
}