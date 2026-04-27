using UnityEngine;
using Game.Save;
using Game.Data;
using Game.Runtime;

#if UNITY_EDITOR
using UnityEngine.SceneManagement;
#endif

/// <summary>
/// GameManager global del proyecto.
/// Su responsabilidad es bootstrap, save/load y sincronización
/// entre runtime narrativo y estructuras de UI persistente.
/// </summary>
[DefaultExecutionOrder(0)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    /// <summary>
    /// Contenedor persistente de datos del menú y UI.
    /// </summary>
    public FragmentProgressData FragmentProgress { get; private set; }

    [Header("Data")]
    [Tooltip("Base de datos principal de memorias del sistema narrativo.")]
    [SerializeField] private MemoryDatabase memoryDatabase;

    private void Awake()
    {
        InitializeSingleton();

        FragmentProgress = new FragmentProgressData();

        LoadGame();
    }

    /// <summary>
    /// Reinicia la data persistente del menú y la reconstruye desde runtime.
    /// </summary>
    public void ResetProgressData()
    {
        FragmentProgress = new FragmentProgressData();
        RebuildProgressFromRuntime();
    }

    /// <summary>
    /// Carga el juego desde save y sincroniza la UI con el runtime actual.
    /// </summary>
    private void LoadGame()
    {
        if (FragmentProgress == null)
        {
            FragmentProgress = new FragmentProgressData();
        }

        SaveSystem.LoadGame();
        RebuildProgressFromRuntime();

        if (FragmentProgress.drops.Count == 0)
        {
            SaveProgress();
        }
    }

    /// <summary>
    /// Guarda el estado actual del juego.
    /// </summary>
    public void SaveProgress()
    {
        SaveSystem.SaveGame();
    }

    /// <summary>
    /// Reconstruye y sincroniza la data del menú a partir del runtime narrativo.
    /// </summary>
    public void RebuildProgressFromRuntime()
    {
        if (memoryDatabase == null)
        {
            Debug.LogError("[GameManager] MemoryDatabase no asignado.");
            return;
        }

        if (GameStateRepository.Instance == null)
        {
            Debug.LogError("[GameManager] GameStateRepository.Instance no disponible.");
            return;
        }

        GameStateRepository.Instance.InitializeAllMemories();

        foreach (MemoryDefinition memory in memoryDatabase.Memories)
        {
            if (memory == null)
                continue;

            MemoryRuntimeData runtime = GameStateRepository.Instance.GetMemory(memory);
            if (runtime == null)
                continue;

            SyncFragmentState(memory, runtime);
            SyncDropData(memory, runtime);
        }

        SyncAllDropConnectionsFromRepository();
    }

    /// <summary>
    /// Obtiene o crea un estado de fragmento UI y lo sincroniza con runtime.
    /// </summary>
    private void SyncFragmentState(MemoryDefinition memory, MemoryRuntimeData runtime)
    {
        FragmentStateData state = FragmentProgress.GetFragmentState(memory.Id);

        if (state == null)
        {
            state = new FragmentStateData(memory.Id);
            FragmentProgress.fragmentStates.Add(state);
        }

        state.WasUnlocked = runtime.CurrentState != MemoryState.Locked;
        state.WasVisited = runtime.CurrentState >= MemoryState.Seen;
        state.WasCompleted = runtime.CurrentState == MemoryState.Completed;
    }

    /// <summary>
    /// Obtiene o crea la data visual de una gota y la sincroniza con runtime.
    /// </summary>
    private void SyncDropData(MemoryDefinition memory, MemoryRuntimeData runtime)
    {
        DropData drop = FragmentProgress.GetDropData(memory.Id);

        if (drop == null)
        {
            drop = new DropData(memory.Id, Vector2.zero);
            FragmentProgress.drops.Add(drop);
        }

        drop.WasVisited = runtime.CurrentState >= MemoryState.Seen;
    }

    /// <summary>
    /// Sincroniza las conexiones visuales de los drops desde el repositorio runtime.
    /// </summary>
    public void SyncAllDropConnectionsFromRepository()
    {
        if (FragmentProgress == null || GameStateRepository.Instance == null)
            return;

        foreach (DropData drop in FragmentProgress.drops)
        {
            if (drop == null)
                continue;

            drop.ClearConnections();

            foreach (string connectedId in GameStateRepository.Instance.GetConnections(drop.FragmentName))
            {
                drop.ConnectTo(connectedId);
            }
        }
    }

    #region Legacy compatibility

    /// <summary>
    /// Marca una memoria como visitada en la proyección UI legacy.
    /// </summary>
    public void MarkFragmentAsVisited(string fragmentName)
    {
        DropData drop = FragmentProgress.GetDropData(fragmentName);
        if (drop != null)
        {
            drop.WasVisited = true;
        }

        FragmentStateData fragmentState = FragmentProgress.GetFragmentState(fragmentName);
        if (fragmentState != null)
        {
            fragmentState.WasVisited = true;
        }
    }

    #endregion

#if UNITY_EDITOR

    [ContextMenu("🔴 RESET GAME (DELETE SAVE + RESTART)")]
    private void Editor_ResetGame()
    {
        SaveSystem.ResetGame();
        Debug.Log("[EDITOR] Game Reset");
        SceneManager.LoadScene(0);
    }

    [ContextMenu("💾 FORCE SAVE")]
    private void Editor_Save()
    {
        SaveSystem.SaveGame();
    }

    [ContextMenu("📂 FORCE LOAD")]
    private void Editor_Load()
    {
        SaveSystem.LoadGame();
        RebuildProgressFromRuntime();
    }

#endif

    /// <summary>
    /// Inicializa el singleton persistente.
    /// </summary>
    private void InitializeSingleton()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}