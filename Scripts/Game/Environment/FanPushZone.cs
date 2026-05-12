using UnityEngine;

/// <summary>
/// Zona de empuje para ventiladores.
/// Mientras el jugador está dentro del trigger, aplica una fuerza constante.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class FanPushZone : MonoBehaviour
{
    #region Inspector

    [Header("Empuje")]
    [SerializeField]
    [Tooltip("Dirección local del empuje. Normalmente Forward.")]
    private Vector3 localPushDirection = Vector3.forward;

    [SerializeField]
    [Tooltip("Fuerza aplicada al Rigidbody del jugador mientras está dentro de la zona.")]
    private float pushForce = 18f;

    [SerializeField]
    [Tooltip("Modo de fuerza usado para empujar al jugador.")]
    private ForceMode forceMode = ForceMode.Acceleration;

    [Header("Filtro")]
    [SerializeField]
    [Tooltip("Capas que pueden ser afectadas por el ventilador.")]
    private LayerMask affectedLayers = ~0;

    [Header("Visual Opcional")]
    [SerializeField]
    [Tooltip("Transform visual de las aspas. Si se asigna, rotará constantemente.")]
    private Transform bladeVisual;

    [SerializeField]
    [Tooltip("Velocidad visual de giro de las aspas.")]
    private float bladeRotationSpeed = 720f;

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        Collider ownCollider = GetComponent<Collider>();
        ownCollider.isTrigger = true;
    }

    private void Update()
    {
        if (bladeVisual == null)
        {
            return;
        }

        bladeVisual.Rotate(Vector3.forward, bladeRotationSpeed * Time.deltaTime, Space.Self);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!IsInLayerMask(other.gameObject.layer, affectedLayers))
        {
            return;
        }

        Rigidbody targetRigidbody = other.attachedRigidbody;

        if (targetRigidbody == null)
        {
            targetRigidbody = other.GetComponentInParent<Rigidbody>();
        }

        if (targetRigidbody == null || targetRigidbody.isKinematic)
        {
            return;
        }

        Vector3 pushDirection = transform.TransformDirection(localPushDirection);

        if (pushDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        targetRigidbody.AddForce(pushDirection.normalized * pushForce, forceMode);
    }

    #endregion

    #region Helpers

    private static bool IsInLayerMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private void OnValidate()
    {
        if (localPushDirection.sqrMagnitude <= 0.0001f)
        {
            localPushDirection = Vector3.forward;
        }

        pushForce = Mathf.Max(0f, pushForce);
        bladeRotationSpeed = Mathf.Max(0f, bladeRotationSpeed);
    }

    #endregion
}