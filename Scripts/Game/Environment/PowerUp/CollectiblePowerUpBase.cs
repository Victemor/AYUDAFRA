using UnityEngine;

/// <summary>
/// Clase base abstracta para todos los pickups de power-up coleccionables del escenario.
///
/// Responsabilidades compartidas:
/// - Detectar el contacto con la bola por componente (nunca por tag).
/// - Aplicar el efecto específico al <see cref="BallPowerUpController"/> mediante <see cref="ApplyEffect"/>.
/// - Destruirse completamente al ser recolectado, garantizando que no pueda volver a activarse.
///
/// Responsabilidad delegada a subclases:
/// - Definir <see cref="PowerUpType"/> para identificar el efecto en eventos y UI.
/// - Implementar <see cref="ApplyEffect"/> con los parámetros del efecto concreto.
///
/// Uso en el generador:
/// El <see cref="PowerUpGenerator"/> lee <see cref="SurfaceOffset"/> para alinear correctamente
/// la base del collider con la superficie del track tras instanciar el prefab.
///
/// Diseño de prefab:
/// Cada prefab de power-up debe tener un Collider marcado como IsTrigger.
/// Los datos del efecto (duración, radio, etc.) viven en el componente concreto que hereda
/// esta clase, no en ScriptableObjects. Esto permite configurar cada prefab de forma
/// independiente sin asset adicional.
/// </summary>
[RequireComponent(typeof(Collider))]
public abstract class CollectiblePowerUpBase : MonoBehaviour
{
    #region Inspector

    [Header("Generación")]
    [SerializeField]
    [Tooltip("Offset vertical en metros aplicado sobre la superficie del track al instanciar.\n" +
             "Permite ajustar la altura de spawn según la forma del prefab.\n" +
             "Rango típico: 0.3 – 1.0 m.")]
    private float surfaceOffset = 0.5f;

    #endregion

    #region Properties

    /// <summary>
    /// Offset vertical sobre la superficie del track usado por <see cref="PowerUpGenerator"/>
    /// al posicionar este pickup.
    /// </summary>
    public float SurfaceOffset => surfaceOffset;

    /// <summary>
    /// Tipo de efecto que aplica este power-up.
    /// Usado por los eventos de <see cref="BallPowerUpController"/> para notificar
    /// a sistemas de UI y audio qué power-up fue activado o expiró.
    /// </summary>
    public abstract CollectiblePowerUpType PowerUpType { get; }

    #endregion

    #region Abstract API

    /// <summary>
    /// Aplica el efecto de este power-up al controlador indicado.
    /// Implementado por cada subclase con sus propios parámetros serializados.
    /// También es llamado directamente por <see cref="CollectiblePowerUpDebugger"/>
    /// en Play Mode para pruebas de diseño.
    /// </summary>
    /// <param name="controller">Controlador de power-ups de la bola que recibirá el efecto.</param>
    public abstract void ApplyEffect(BallPowerUpController controller);

    #endregion

    #region Physics

    private void OnTriggerEnter(Collider other)
    {
        // Detectamos al jugador por componente, nunca por tag, para ser
        // consistentes con el resto del proyecto y no depender de strings.
        BallPowerUpController controller =
            other.GetComponent<BallPowerUpController>()
            ?? other.GetComponentInParent<BallPowerUpController>();

        if (controller == null) return;

        ApplyEffect(controller);

        // Destruir completamente el GameObject para que no exista en escena,
        // no sea visible y no pueda volver a ser recolectado bajo ninguna circunstancia.
        // Los pickups son generados frescos en cada nivel, así que Destroy es seguro
        // frente a SetActive(false), que podría permitir re-activación accidental.
        Destroy(gameObject);
    }

    #endregion
}