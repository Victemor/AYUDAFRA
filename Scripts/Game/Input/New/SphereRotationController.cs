using UnityEngine;

/// <summary>
/// Controla la dirección frontal lógica y visual de la pelota.
/// La pelota solo puede cambiar orientación en Y; las rotaciones físicas en X/Z se bloquean
/// para evitar que las colisiones desalineen la cara frontal.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public sealed class SphereRotationController : MonoBehaviour
{
    #region Inspector

    [Header("Referencias")]

    [SerializeField]
    [Tooltip("Rigidbody de la pelota.")]
    private Rigidbody rb;

    [Header("Rotación")]

    [SerializeField]
    [Tooltip("Velocidad de rotación en grados por segundo con input a tope.")]
    private float maxRotationDegreesPerSecond = 120f;

    [SerializeField]
    [Tooltip("Magnitud mínima del eje horizontal para aplicar rotación.")]
    [Range(0f, 0.3f)]
    private float rotationDeadzone = 0.05f;

    [SerializeField]
    [Tooltip("Si está activo, el transform de la pelota se alinea visualmente con el forward actual.")]
    private bool alignVisualYawToForward = true;

    [SerializeField]
    [Tooltip("Exponente de la curva de sensibilidad del giro. " +
            "1 = lineal. 2 = cuadrático (giro suave en centro, agresivo en extremos). " +
            "3 = cúbico (aún más pronunciado).")]
    [Range(1f, 4f)]
    private float rotationCurveExponent = 2f;

    [Header("Debug")]

    [SerializeField]
    [Tooltip("Muestra el forward actual en consola.")]
    private bool debugForward;

    #endregion

    #region Runtime

    private Vector3 currentForward = Vector3.forward;
    private float horizontalInput;

    #endregion

    #region Properties

    /// <summary>
    /// Dirección frontal horizontal actual de la pelota.
    /// </summary>
    public Vector3 CurrentForward => currentForward;

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Awake()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        rb.constraints |= RigidbodyConstraints.FreezeRotationX;
        rb.constraints |= RigidbodyConstraints.FreezeRotationY;
        rb.constraints |= RigidbodyConstraints.FreezeRotationZ;

        Vector3 initialForward = transform.forward;
        initialForward.y = 0f;

        currentForward = initialForward.sqrMagnitude > 0.0001f
            ? initialForward.normalized
            : Vector3.forward;

        ApplyVisualRotation();
    }

    private void FixedUpdate()
    {
        ClearPhysicalAngularVelocity();
        ApplyInputRotation();
        ApplyVisualRotation();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Recibe el eje horizontal del input unificado.
    /// Rango esperado: -1 a 1.
    /// </summary>
    public void SetRotationInput(float horizontal)
    {
        horizontalInput = Mathf.Clamp(horizontal, -1f, 1f);
    }

    /// <summary>
    /// Fuerza la dirección frontal desde una rotación absoluta.
    /// </summary>
    public void SnapToRotation(Quaternion rotation)
    {
        Vector3 forward = rotation * Vector3.forward;
        SetForward(forward);
        horizontalInput = 0f;
    }

    /// <summary>
    /// Fuerza la dirección frontal de la pelota hacia una dirección horizontal.
    /// </summary>
    public void SetForward(Vector3 worldForward)
    {
        worldForward.y = 0f;

        if (worldForward.sqrMagnitude < 0.0001f)
        {
            return;
        }

        currentForward = worldForward.normalized;
        ApplyVisualRotation();
    }

    #endregion

    #region Private

    private void ApplyInputRotation()
    {
        if (Mathf.Abs(horizontalInput) <= rotationDeadzone)
            return;

        // Aplicar curva de respuesta no lineal:
        // valores cercanos al centro se reducen, valores al extremo se mantienen
        float curved = Mathf.Pow(Mathf.Abs(horizontalInput), rotationCurveExponent)
                    * Mathf.Sign(horizontalInput);

        float degrees = curved * maxRotationDegreesPerSecond * Time.fixedDeltaTime;

        currentForward = Quaternion.Euler(0f, degrees, 0f) * currentForward;
        currentForward.y = 0f;

        currentForward = currentForward.sqrMagnitude < 0.0001f
            ? Vector3.forward
            : currentForward.normalized;

        if (debugForward)
            Debug.Log($"[SphereRotationController] Forward: {currentForward}");
    }

    private void ApplyVisualRotation()
    {
        if (!alignVisualYawToForward || rb == null)
            return;

        if (currentForward.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(currentForward, Vector3.up);
        rb.rotation = targetRotation; // ← era rb.MoveRotation(targetRotation)
    }

    private void ClearPhysicalAngularVelocity()
    {
        if (rb == null)
        {
            return;
        }

        rb.angularVelocity = Vector3.zero;
    }

    #endregion
}