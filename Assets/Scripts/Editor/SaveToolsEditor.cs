using UnityEditor;
using UnityEngine;
using Game.Save; // ajusta si tu namespace es diferente

public static class SaveToolsEditor
{
    /// <summary>
    /// Borra el save actual (JSON) desde el menú de Unity.
    /// </summary>
    [MenuItem("Tools/Save/Reset Save File")]
    public static void ResetSave()
    {
        if (EditorUtility.DisplayDialog(
            "Reset Save",
            "¿Estás seguro de que quieres borrar el save?",
            "Sí",
            "Cancelar"))
        {
            SaveSystem.DeleteSave();

            Debug.Log("[EDITOR] Save eliminado correctamente.");
        }
    }

    /// <summary>
    /// Resetea todo el estado runtime del juego.
    /// </summary>
    [MenuItem("Tools/Save/Reset Full Game")]
    public static void ResetFullGame()
    {
        if (EditorUtility.DisplayDialog(
            "Reset Game",
            "¿Seguro que quieres resetear TODO el estado del juego?",
            "Sí",
            "Cancelar"))
        {
            SaveSystem.ResetGame();

            Debug.Log("[EDITOR] Juego reseteado completamente.");
        }
    }
}