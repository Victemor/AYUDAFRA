using UnityEngine;

/// <summary>
/// Pickup de power-up coleccionable en el mundo.
///
/// Al entrar en contacto con la bola notifica al <see cref="BallPowerUpController"/>
/// y se desactiva. El <see cref="PowerUpGenerator"/> instancia este prefab y asigna
/// el <see cref="CollectiblePowerUpData"/> correspondiente.
///
/// El prefab debe tener un Collider marcado como IsTrigger.
/// No depende del tag Player: detecta al jugador por componente.
/// </summary>
public sealed class CollectiblePowerUp : MonoBehaviour
{
    #region Inspector

    [SerializeField]
    [Tooltip("Configuración del efecto que aplica este pickup. " +
             "Asignado por PowerUpGenerator al instanciarlo.")]
    private CollectiblePowerUpData data;

    #endregion

    #region Properties

    /// <summary>Datos del efecto asociado a este pickup.</summary>
    public CollectiblePowerUpData Data => data;

    #endregion

    #region Public API

    /// <summary>
    /// Asigna los datos del efecto. Llamado por <see cref="PowerUpGenerator"/> tras instanciar.
    /// </summary>
    public void Initialize(CollectiblePowerUpData powerUpData)
    {
        data = powerUpData;
    }

    #endregion

    #region Physics

    private void OnTriggerEnter(Collider other)
    {
        if (data == null) return;

        BallPowerUpController controller =
            other.GetComponent<BallPowerUpController>()
            ?? other.GetComponentInParent<BallPowerUpController>();

        if (controller == null) return;

        controller.Collect(data);
        gameObject.SetActive(false);
    }

    #endregion
}