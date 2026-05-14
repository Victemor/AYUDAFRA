using UnityEngine;

/// <summary>
/// Configuración de un obstáculo dinámico que puede ser empujado por la pelota.
///
/// Diseño de colisión:
/// Al impactar, la pelota NO pierde velocidad, NO cambia de dirección y NO siente el golpe.
/// Solo este objeto recibe fuerzas y sale expulsado. Este comportamiento se logra en
/// <see cref="BallCollisionResponder"/> restaurando la velocidad pre-física de la bola
/// después de aplicar las fuerzas a este Rigidbody, cancelando el impulso de reacción
/// que Unity habría aplicado a la bola por la Tercera Ley de Newton.
///
/// Los campos <c>SpeedMultiplier</c> y <c>DriveSuppressionDuration</c> fueron eliminados:
/// ya no se aplica ningún efecto sobre el motor de la pelota.
/// </summary>
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public sealed class PushableObstacle : MonoBehaviour
{
    #region Inspector

    [Header("Impulso al Objeto")]

    [SerializeField]
    [Tooltip("Impulso base en N·s aplicado al Rigidbody del objeto al recibir el golpe.\n" +
             "Valores más altos producen una expulsión más violenta.")]
    [Min(0f)]
    private float pushImpulse = 3f;

    [SerializeField]
    [Tooltip("Factor de componente lateral adicional respecto a la dirección principal del impacto.\n" +
             "0 = solo la dirección frontal. 0.2 = algo de desvío lateral según el ángulo del golpe.")]
    [Range(0f, 1f)]
    private float lateralPushFactor = 0.2f;

    [SerializeField]
    [Tooltip("Impulso vertical adicional aplicado al objeto.\n" +
             "Mantener bajo (0.05–0.15) para evitar que salga volando de forma artificial.")]
    [Min(0f)]
    private float upwardImpulse = 0.05f;

    [SerializeField]
    [Tooltip("Torque aplicado al objeto para que rote de forma natural al ser golpeado.")]
    [Min(0f)]
    private float torqueImpulse = 1.5f;

    [Header("Rigidbody por Defecto")]

    [SerializeField]
    [Tooltip("Si está activo, Reset configura el Rigidbody con los valores recomendados\n" +
             "para que el objeto sea empujable sin sentirse como una pared fija.")]
    private bool configureRigidbodyOnReset = true;

    [SerializeField]
    [Tooltip("Masa del Rigidbody. Valores bajos (0.3–1.0) hacen el objeto más reactivo al golpe.")]
    [Min(0.01f)]
    private float defaultMass = 0.65f;

    [SerializeField]
    [Tooltip("Drag lineal. Evita que el objeto se desplace indefinidamente tras el impacto.")]
    [Min(0f)]
    private float defaultDrag = 0.2f;

    [SerializeField]
    [Tooltip("Drag angular. Estabiliza la rotación después del impacto.")]
    [Min(0f)]
    private float defaultAngularDrag = 0.35f;

    #endregion

    #region Properties

    /// <summary>Impulso base aplicado al Rigidbody del objeto al ser golpeado.</summary>
    public float PushImpulse       => pushImpulse;

    /// <summary>Factor lateral del impulso respecto a la dirección principal del golpe.</summary>
    public float LateralPushFactor => lateralPushFactor;

    /// <summary>Componente vertical del impulso aplicado al objeto.</summary>
    public float UpwardImpulse     => upwardImpulse;

    /// <summary>Torque aplicado al objeto para rotación natural tras el golpe.</summary>
    public float TorqueImpulse     => torqueImpulse;

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
        ownRigidbody.isKinematic        = false;
        ownRigidbody.useGravity         = true;
        ownRigidbody.mass               = defaultMass;
        ownRigidbody.linearDamping      = defaultDrag;
        ownRigidbody.angularDamping     = defaultAngularDrag;
        ownRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        ownRigidbody.interpolation      = RigidbodyInterpolation.Interpolate;
    }

    private void OnValidate()
    {
        pushImpulse       = Mathf.Max(0f, pushImpulse);
        lateralPushFactor = Mathf.Clamp01(lateralPushFactor);
        upwardImpulse     = Mathf.Max(0f, upwardImpulse);
        torqueImpulse     = Mathf.Max(0f, torqueImpulse);

        defaultMass        = Mathf.Max(0.01f, defaultMass);
        defaultDrag        = Mathf.Max(0f, defaultDrag);
        defaultAngularDrag = Mathf.Max(0f, defaultAngularDrag);
    }

    #endregion
}