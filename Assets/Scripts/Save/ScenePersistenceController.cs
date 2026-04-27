using UnityEngine;
using Game.Save;

/// <summary>
/// Orquestador local de persistencia para escenas jugables.
/// 
/// Responsabilidades:
/// - Cargar save al entrar en la escena.
/// - Guardar al salir de la escena.
/// - Asegurar que el estado esté restaurado antes del uso normal de gameplay.
/// </summary>
[DefaultExecutionOrder(-100000)]
public sealed class ScenePersistenceController : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Si está activo, carga el save automáticamente al iniciar la escena.")]
    private bool loadSaveOnStart = true;

    [SerializeField]
    [Tooltip("Si está activo, guarda el save al desactivar la escena.")]
    private bool saveOnSceneDisable = true;

    [SerializeField]
    [Tooltip("Activa logs de depuración del sistema de persistencia de escena.")]
    private bool debugLogs = true;

    private bool hasStarted;
    private bool isApplicationQuitting;

    private void Start()
    {
        if (loadSaveOnStart)
        {
            Log("LoadGame on scene start.");
            SaveSystem.LoadGame();
        }

        ApplyNarrativeBinders();
        hasStarted = true;
    }

    private void OnDisable()
    {
        if (!saveOnSceneDisable || !hasStarted || isApplicationQuitting)
        {
            return;
        }

        Log("SaveGame on scene disable.");
        SaveSystem.SaveGame();
    }

    private void OnApplicationQuit()
    {
        isApplicationQuitting = true;
    }

    private void ApplyNarrativeBinders()
    {
        NarrativeWorldObjectBinder[] binders = FindObjectsByType<NarrativeWorldObjectBinder>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < binders.Length; i++)
        {
            if (binders[i] != null)
            {
                binders[i].ApplyRuntimeState();
            }
        }
    }

    private void Log(string message)
    {
        if (!debugLogs)
        {
            return;
        }

        Debug.Log($"[ScenePersistenceController] {message}", this);
    }
}