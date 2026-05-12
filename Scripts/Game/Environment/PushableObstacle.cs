using UnityEngine;

/// <summary>
/// Configuración de impacto para obstáculos que pueden ser empujados por la pelota.
/// </summary>
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public sealed class PushableObstacle : MonoBehaviour
{
    #region Inspector

    [Header("Velocidad de la pelota")]

    [Tooltip("Multiplicador de velocidad lógica que conserva la pelota al impactar este objeto. Valores altos evitan que el impacto se sienta como una pared fija.")]
    [SerializeField, Range(0f, 1f)] private float speedMultiplier = 0.97f;

    [Tooltip("Tiempo breve durante el cual el motor deja de empujar tras el impacto. Debe ser bajo para que la pelota no se sienta frenada.")]
    [SerializeField, Min(0f)] private float driveSuppressionDuration = 0.015f;

    [Header("Impulso del objeto")]

    [Tooltip("Impulso base aplicado al rigidbody del objeto al recibir el golpe.")]
    [SerializeField, Min(0f)] private float pushImpulse = 3f;

    [Tooltip("Porcentaje de componente lateral adicional respecto a la dirección principal del impacto.")]
    [SerializeField, Range(0f, 1f)] private float lateralPushFactor = 0.2f;

    [Tooltip("Impulso vertical opcional. Mantener bajo para evitar que el objeto salga volando de forma artificial.")]
    [SerializeField, Min(0f)] private float upwardImpulse = 0.05f;

    [Tooltip("Torque adicional aplicado para que el objeto rote con más naturalidad al ser golpeado.")]
    [SerializeField, Min(0f)] private float torqueImpulse = 1.5f;

    [Header("Rigidbody Defaults")]

    [Tooltip("Si está activo, Reset configura el Rigidbody con valores recomendados para obstáculos empujables.")]
    [SerializeField] private bool configureRigidbodyOnReset = true;

    [Tooltip("Masa recomendada para que el objeto sea empujable sin sentirse como una pared.")]
    [SerializeField, Min(0.01f)] private float defaultMass = 0.65f;

    [Tooltip("Drag lineal recomendado para evitar que el objeto se desplace infinitamente.")]
    [SerializeField, Min(0f)] private float defaultDrag = 0.2f;

    [Tooltip("Drag angular recomendado para que la rotación se estabilice después del impacto.")]
    [SerializeField, Min(0f)] private float defaultAngularDrag = 0.35f;

    #endregion

    #region Properties

    public float SpeedMultiplier => speedMultiplier;
    public float DriveSuppressionDuration => driveSuppressionDuration;
    public float PushImpulse => pushImpulse;
    public float LateralPushFactor => lateralPushFactor;
    public float UpwardImpulse => upwardImpulse;
    public float TorqueImpulse => torqueImpulse;

    #endregion

    #region Unity Events

    private void Reset()
    {
        Collider ownCollider = GetComponent<Collider>();
        ownCollider.isTrigger = false;

        if (!configureRigidbodyOnReset)
        {
            return;
        }

        Rigidbody ownRigidbody = GetComponent<Rigidbody>();
        ownRigidbody.isKinematic = false;
        ownRigidbody.useGravity = true;
        ownRigidbody.mass = defaultMass;
        ownRigidbody.linearDamping = defaultDrag;
        ownRigidbody.angularDamping = defaultAngularDrag;
        ownRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        ownRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void OnValidate()
    {
        speedMultiplier = Mathf.Clamp01(speedMultiplier);
        driveSuppressionDuration = Mathf.Max(0f, driveSuppressionDuration);

        pushImpulse = Mathf.Max(0f, pushImpulse);
        lateralPushFactor = Mathf.Clamp01(lateralPushFactor);
        upwardImpulse = Mathf.Max(0f, upwardImpulse);
        torqueImpulse = Mathf.Max(0f, torqueImpulse);

        defaultMass = Mathf.Max(0.01f, defaultMass);
        defaultDrag = Mathf.Max(0f, defaultDrag);
        defaultAngularDrag = Mathf.Max(0f, defaultAngularDrag);
    }

    #endregion
}