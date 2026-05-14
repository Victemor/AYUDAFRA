using UnityEngine;

/// <summary>
/// Componente de depuración para probar power-ups coleccionables en Play Mode.
///
/// Solo útil en el Editor y builds de desarrollo. El Custom Editor asociado
/// (<c>CollectiblePowerUpDebuggerEditor</c>) dibuja botones por cada power-up
/// configurado que llaman directamente a <see cref="BallPowerUpController.Collect"/>.
///
/// Este componente no tiene lógica de gameplay propia y no debe incluirse
/// en builds de producción.
/// </summary>
public sealed class CollectiblePowerUpDebugger : MonoBehaviour
{
    #region Inspector

    [SerializeField]
    [Tooltip("Controlador de power-ups al que se envían los efectos de prueba.")]
    private BallPowerUpController controller;

    [SerializeField]
    [Tooltip("Lista de power-ups disponibles para probar desde el Inspector en Play Mode.")]
    private CollectiblePowerUpData[] testPowerUps;

    #endregion

    #region Properties

    /// <summary>Controlador de power-ups objetivo. Usado por el Custom Editor.</summary>
    public BallPowerUpController Controller => controller;

    /// <summary>Power-ups disponibles para prueba. Usado por el Custom Editor.</summary>
    public CollectiblePowerUpData[] TestPowerUps => testPowerUps;

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        controller = GetComponent<BallPowerUpController>();
    }

    #endregion
}