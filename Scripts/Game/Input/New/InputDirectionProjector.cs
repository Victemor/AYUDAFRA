using UnityEngine;

/// <summary>
/// Utilidad estática para convertir una dirección de pantalla en una dirección mundo
/// usando la cara actual de la pelota como marco de referencia.
///
/// Paradigma:
/// El eje Y de pantalla (arriba) siempre equivale a "donde apunta la cara de la bola"
/// (su forward). El eje X de pantalla (derecha) equivale a "la derecha de la cara".
///
/// Esto produce un joystick cara-relativo: girar hacia la izquierda es siempre 90° a la
/// izquierda de la cara actual, independientemente de hacia dónde apunta la cámara.
/// La cámara no interviene en el cálculo de dirección.
///
/// Ejemplo:
/// - Cara = Norte, screenDir = (-1,0) → worldDir = Oeste  ✓
/// - Cara = Oeste, screenDir = (0,1)  → worldDir = Oeste  (seguir recto) ✓
/// - Cara = Oeste, screenDir = (-1,0) → worldDir = Sur    (giro a la izquierda) ✓
/// </summary>
public static class InputDirectionProjector
{
    /// <summary>
    /// Convierte una dirección normalizada de pantalla en una dirección mundo planar (Y=0)
    /// usando <paramref name="faceForward"/> como eje "arriba" (Y+) del joystick.
    ///
    /// La componente Y de <paramref name="faceForward"/> se ignora automáticamente.
    /// Si el vector resultante es cero, devuelve <paramref name="faceForward"/>.
    /// </summary>
    /// <param name="screenDir">Dirección normalizada de pantalla. Y+ = adelante, X+ = derecha.</param>
    /// <param name="faceForward">Dirección cara de la bola en espacio mundo (Y ignorada).</param>
    public static Vector3 FaceRelative(Vector2 screenDir, Vector3 faceForward)
    {
        faceForward.y = 0f;
        if (faceForward.sqrMagnitude < 0.0001f) faceForward = Vector3.forward;
        faceForward.Normalize();

        // La derecha de la cara: perpendicular a faceForward en el plano XZ.
        Vector3 faceRight = Vector3.Cross(Vector3.up, faceForward);
        if (faceRight.sqrMagnitude < 0.0001f) faceRight = Vector3.right;
        faceRight.Normalize();

        // Proyectar: X de pantalla → eje derecha de la cara, Y de pantalla → eje cara.
        Vector3 worldDir = faceRight * screenDir.x + faceForward * screenDir.y;
        worldDir.y = 0f;

        if (worldDir.sqrMagnitude < 0.0001f) return faceForward;
        return worldDir.normalized;
    }
}