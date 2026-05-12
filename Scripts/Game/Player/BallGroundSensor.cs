using UnityEngine;

/// <summary>
/// Sensor de suelo del jugador.
/// Usa SphereCast principal, SphereCast estrecho y Raycast central para mejorar detección sobre rieles.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public sealed class BallGroundSensor : MonoBehaviour
{
    #region Inspector

    [Header("Referencias")]

    [SerializeField]
    [Tooltip("Rigidbody asociado al jugador.")]
    private Rigidbody rb;

    [Header("Ground Detection")]

    [SerializeField]
    [Tooltip("Capas válidas que se consideran suelo.")]
    private LayerMask groundLayers = ~0;

    [SerializeField]
    [Tooltip("Radio del SphereCast principal. Funciona bien para superficies anchas.")]
    private float probeRadius = 0.45f;

    [SerializeField]
    [Tooltip("Distancia máxima del SphereCast para detectar suelo.")]
    private float probeDistance = 0.8f;

    [SerializeField]
    [Tooltip("Desplazamiento vertical adicional del origen de detección.")]
    private float probeOriginOffset = 0.1f;

    [SerializeField]
    [Tooltip("Ángulo máximo permitido para considerar una superficie como suelo caminable.")]
    private float maxGroundAngle = 75f;

    [Header("Narrow Surface Detection")]

    [SerializeField]
    [Tooltip("Radio del SphereCast secundario para superficies estrechas como rieles.")]
    private float narrowProbeRadius = 0.05f;

    [SerializeField]
    [Tooltip("Distancia extra añadida al narrowProbe para compensar el offset de origen.")]
    private float narrowProbeExtraDistance = 0.15f;

    [SerializeField]
    [Tooltip("Activa un Raycast central real como último respaldo para rieles o superficies delgadas.")]
    private bool useCentralRaycastFallback = true;

    [SerializeField]
    [Tooltip("Distancia extra del Raycast central respecto al probe principal.")]
    private float centralRayExtraDistance = 0.2f;

    [Header("Debug")]

    [SerializeField]
    [Tooltip("Dibuja gizmos del sensor de suelo en escena.")]
    private bool drawDebugGizmos;

    #endregion

    #region Runtime

    private bool isGrounded;
    private Vector3 groundNormal = Vector3.up;
    private float groundAngle;
    private RaycastHit lastHit;

    #endregion

    #region Properties

    /// <summary>Indica si el jugador está apoyado sobre un suelo válido.</summary>
    public bool IsGrounded => isGrounded;

    /// <summary>Normal del suelo actual.</summary>
    public Vector3 GroundNormal => groundNormal;

    /// <summary>Ángulo actual del suelo respecto a Vector3.up.</summary>
    public float GroundAngle => groundAngle;

    /// <summary>Último hit válido registrado por el sensor.</summary>
    public RaycastHit LastHit => lastHit;

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
    }

    #endregion

    #region Public API

    /// <summary>
    /// Actualiza el estado de contacto con el suelo.
    /// </summary>
    public void RefreshGroundState()
    {
        Vector3 origin = GetProbeOrigin();

        if (TryMainSphereCast(origin, out RaycastHit mainHit))
        {
            ApplyHit(mainHit);
            return;
        }

        if (TryNarrowSphereCast(origin, out RaycastHit narrowHit))
        {
            ApplyHit(narrowHit);
            return;
        }

        if (useCentralRaycastFallback && TryCentralRaycast(origin, out RaycastHit rayHit))
        {
            ApplyHit(rayHit);
            return;
        }

        ClearGround();
    }

    /// <summary>
    /// Devuelve la dirección forward proyectada sobre el plano del suelo.
    /// </summary>
    public Vector3 GetProjectedForward(Vector3 worldForward)
    {
        Vector3 resolved = worldForward.normalized;

        if (!isGrounded)
        {
            resolved.y = 0f;
            return resolved.sqrMagnitude < 0.0001f ? Vector3.forward : resolved.normalized;
        }

        Vector3 projected = Vector3.ProjectOnPlane(resolved, groundNormal);

        if (projected.sqrMagnitude < 0.0001f)
        {
            resolved.y = 0f;
            return resolved.sqrMagnitude < 0.0001f ? Vector3.forward : resolved.normalized;
        }

        return projected.normalized;
    }

    /// <summary>
    /// Fuerza una actualización del estado de suelo.
    /// </summary>
    public void RefreshGroundStateImmediate()
    {
        RefreshGroundState();
    }

    #endregion

    #region Detection

    private bool TryMainSphereCast(Vector3 origin, out RaycastHit hit)
    {
        bool hasHit = Physics.SphereCast(
            origin,
            probeRadius,
            Vector3.down,
            out hit,
            probeDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore);

        return hasHit && IsValidGround(hit);
    }

    private bool TryNarrowSphereCast(Vector3 origin, out RaycastHit hit)
    {
        bool hasHit = Physics.SphereCast(
            origin,
            narrowProbeRadius,
            Vector3.down,
            out hit,
            probeDistance + narrowProbeExtraDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore);

        return hasHit && IsValidGround(hit);
    }

    private bool TryCentralRaycast(Vector3 origin, out RaycastHit hit)
    {
        bool hasHit = Physics.Raycast(
            origin,
            Vector3.down,
            out hit,
            probeDistance + centralRayExtraDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore);

        return hasHit && IsValidGround(hit);
    }

    #endregion

    #region Helpers

    private bool IsValidGround(RaycastHit hit)
    {
        float angle = Vector3.Angle(hit.normal, Vector3.up);
        return angle <= maxGroundAngle;
    }

    private void ApplyHit(RaycastHit hit)
    {
        float angle = Vector3.Angle(hit.normal, Vector3.up);

        isGrounded = true;
        groundNormal = hit.normal.normalized;
        groundAngle = angle;
        lastHit = hit;
    }

    private void ClearGround()
    {
        isGrounded = false;
        groundNormal = Vector3.up;
        groundAngle = 0f;
        lastHit = default;
    }

    private Vector3 GetProbeOrigin()
    {
        Vector3 origin = rb != null ? rb.worldCenterOfMass : transform.position;
        origin += Vector3.up * probeOriginOffset;
        return origin;
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos)
        {
            return;
        }

        Vector3 origin = Application.isPlaying
            ? GetProbeOrigin()
            : transform.position + Vector3.up * probeOriginOffset;

        Vector3 mainEnd = origin + Vector3.down * probeDistance;
        Vector3 narrowEnd = origin + Vector3.down * (probeDistance + narrowProbeExtraDistance);
        Vector3 rayEnd = origin + Vector3.down * (probeDistance + centralRayExtraDistance);

        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(origin, probeRadius);
        Gizmos.DrawLine(origin, mainEnd);
        Gizmos.DrawWireSphere(mainEnd, probeRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, narrowProbeRadius);
        Gizmos.DrawLine(origin, narrowEnd);
        Gizmos.DrawWireSphere(narrowEnd, narrowProbeRadius);

        Gizmos.color = Color.white;
        Gizmos.DrawLine(origin, rayEnd);

        if (isGrounded)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(lastHit.point, groundNormal);
        }
    }

    #endregion
}