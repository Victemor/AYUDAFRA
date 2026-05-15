using UnityEngine;

/// <summary>
/// Utilidad estática para convertir una dirección en espacio de pantalla a una
/// dirección en espacio mundo proyectada sobre el plano XZ.
///
/// Usado por <see cref="DirectionalJoystickController"/> y <see cref="SwipeDirectionController"/>
/// para transformar el ángulo del joystick/swipe en una dirección 3D relativa a la cámara.
/// Se extrae aquí para evitar duplicar la lógica en ambos controladores.
/// </summary>
public static class CameraInputProjector
{
    /// <summary>
    /// Convierte una dirección normalizada de pantalla (espacio 2D) en una dirección
    /// de mundo normalizada proyectada sobre el plano XZ, usando los ejes de la cámara
    /// como referencia de orientación.
    ///
    /// Convención de pantalla:
    /// - Y positivo (arriba en pantalla) → forward relativo a cámara
    /// - X positivo (derecha en pantalla) → right relativo a cámara
    ///
    /// Si la cámara es null, usa el plano XZ directo (Y de pantalla = Z de mundo).
    /// </summary>
    /// <param name="screenDir">Dirección normalizada en espacio de pantalla.</param>
    /// <param name="camera">Cámara de referencia para construir el frame de orientación.</param>
    /// <returns>Dirección normalizada en espacio mundo sobre el plano XZ.</returns>
    public static Vector3 ScreenToWorldDirection(Vector2 screenDir, Camera camera)
    {
        if (camera == null)
        {
            return new Vector3(screenDir.x, 0f, screenDir.y).normalized;
        }

        Vector3 camForward = camera.transform.forward;
        Vector3 camRight   = camera.transform.right;

        // Proyectar sobre el plano XZ eliminando la componente vertical.
        camForward.y = 0f;
        camRight.y   = 0f;

        if (camForward.sqrMagnitude < 0.0001f) camForward = Vector3.forward;
        if (camRight.sqrMagnitude   < 0.0001f) camRight   = Vector3.right;

        camForward.Normalize();
        camRight.Normalize();

        Vector3 worldDir = camRight * screenDir.x + camForward * screenDir.y;
        worldDir.y = 0f;

        if (worldDir.sqrMagnitude < 0.0001f) return Vector3.forward;
        return worldDir.normalized;
    }
}