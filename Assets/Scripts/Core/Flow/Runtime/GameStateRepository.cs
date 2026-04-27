using System.Collections.Generic;
using UnityEngine;
using Game.Data;

namespace Game.Runtime
{
    /// <summary>
    /// Repositorio central del estado del juego.
    /// Responsable de gestionar memorias runtime y conexiones entre memorias.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameStateRepository : MonoBehaviour
    {
        public static GameStateRepository Instance { get; private set; }

        [Header("Configuration")]
        [Tooltip("Base de datos central de memorias del juego.")]
        [SerializeField] private MemoryDatabase database;

        private readonly Dictionary<string, MemoryRuntimeData> memories = new();
        private readonly Dictionary<string, HashSet<string>> connections = new();

        private bool hasInitializedAllMemories;

        /// <summary>
        /// Base de datos asociada al repositorio.
        /// </summary>
        public MemoryDatabase Database => database;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeAllMemories();
        }

        /// <summary>
        /// Inicializa todas las memorias del database en runtime.
        /// </summary>
        public void InitializeAllMemories()
        {
            if (hasInitializedAllMemories)
                return;

            if (database == null || database.Memories == null)
            {
                Debug.LogWarning("[GameStateRepository] MemoryDatabase no asignado o vacío.");
                hasInitializedAllMemories = true;
                return;
            }

            foreach (MemoryDefinition memory in database.Memories)
            {
                if (memory == null)
                    continue;

                GetMemory(memory);
            }

            hasInitializedAllMemories = true;
        }

        /// <summary>
        /// Obtiene o crea el runtime de una memoria.
        /// </summary>
        public MemoryRuntimeData GetMemory(MemoryDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.Id))
                return null;

            if (!memories.TryGetValue(definition.Id, out MemoryRuntimeData runtime))
            {
                runtime = new MemoryRuntimeData(definition, this);
                memories.Add(definition.Id, runtime);
            }

            return runtime;
        }

        /// <summary>
        /// Devuelve todas las memorias activas en runtime.
        /// </summary>
        public IEnumerable<MemoryRuntimeData> GetAllMemories()
        {
            return memories.Values;
        }

        /// <summary>
        /// Conecta dos memorias de forma bidireccional.
        /// </summary>
        public void ConnectMemories(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return;

            if (a == b)
                return;

            bool addedA = AddConnection(a, b);
            bool addedB = AddConnection(b, a);

            if (addedA || addedB)
            {
                GameEvents.RaiseMemoryConnectionChanged(a, b);
            }
        }

        /// <summary>
        /// Desconecta dos memorias de forma bidireccional.
        /// </summary>
        public void DisconnectMemories(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return;

            bool removedA = RemoveConnection(a, b);
            bool removedB = RemoveConnection(b, a);

            if (removedA || removedB)
            {
                GameEvents.RaiseMemoryConnectionChanged(a, b);
            }
        }

        /// <summary>
        /// Verifica si dos memorias están conectadas directamente.
        /// </summary>
        public bool AreConnected(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return false;

            if (!connections.TryGetValue(a, out HashSet<string> list))
                return false;

            return list.Contains(b);
        }

        /// <summary>
        /// Obtiene todas las memorias conectadas a una memoria específica.
        /// </summary>
        public IEnumerable<string> GetConnections(string memoryId)
        {
            if (!connections.TryGetValue(memoryId, out HashSet<string> list))
                yield break;

            foreach (string id in list)
            {
                yield return id;
            }
        }

        /// <summary>
        /// Evalúa si un conjunto de memorias está completamente conectado entre sí.
        /// </summary>
        public bool AreMemoriesConnected(List<MemoryDefinition> required)
        {
            if (required == null || required.Count < 2)
                return false;

            for (int i = 0; i < required.Count - 1; i++)
            {
                if (required[i] == null)
                    return false;

                for (int j = i + 1; j < required.Count; j++)
                {
                    if (required[j] == null)
                        return false;

                    if (!AreConnected(required[i].Id, required[j].Id))
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Evalúa si un conjunto de memorias está completamente conectado entre sí
        /// y si ninguna de ellas tiene conexiones hacia memorias externas al conjunto.
        /// </summary>
        public bool AreMemoriesConnectedExclusive(List<MemoryDefinition> required)
        {
            HashSet<string> requiredIds = BuildRequiredIdSet(required);
            if (requiredIds == null || requiredIds.Count < 2)
                return false;

            foreach (string memoryId in requiredIds)
            {
                if (!connections.TryGetValue(memoryId, out HashSet<string> neighbors))
                    return false;

                if (neighbors.Count != requiredIds.Count - 1)
                    return false;

                foreach (string neighbor in neighbors)
                {
                    if (!requiredIds.Contains(neighbor))
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Evalúa si todas las memorias requeridas pertenecen al mismo subgrafo
        /// conectado por camino. El orden de la lista no importa.
        /// </summary>
        public bool AreMemoriesReachable(List<MemoryDefinition> required)
        {
            HashSet<string> requiredIds = BuildRequiredIdSet(required);
            if (requiredIds == null || requiredIds.Count < 2)
                return false;

            string startId = GetAnyId(requiredIds);
            if (string.IsNullOrWhiteSpace(startId))
                return false;

            HashSet<string> reachableIds = GetReachableMemoryIds(startId);

            foreach (string requiredId in requiredIds)
            {
                if (!reachableIds.Contains(requiredId))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Evalúa si todas las memorias requeridas pertenecen al mismo subgrafo
        /// conectado por camino y si ese subgrafo contiene exclusivamente
        /// esas memorias.
        /// </summary>
        public bool AreMemoriesReachableExclusive(List<MemoryDefinition> required)
        {
            HashSet<string> requiredIds = BuildRequiredIdSet(required);
            if (requiredIds == null || requiredIds.Count < 2)
                return false;

            string startId = GetAnyId(requiredIds);
            if (string.IsNullOrWhiteSpace(startId))
                return false;

            HashSet<string> reachableIds = GetReachableMemoryIds(startId);

            if (reachableIds.Count != requiredIds.Count)
                return false;

            foreach (string reachableId in reachableIds)
            {
                if (!requiredIds.Contains(reachableId))
                    return false;
            }

            return true;
        }

        private HashSet<string> BuildRequiredIdSet(List<MemoryDefinition> required)
        {
            if (required == null || required.Count < 2)
                return null;

            HashSet<string> requiredIds = new HashSet<string>();

            for (int i = 0; i < required.Count; i++)
            {
                if (required[i] == null || string.IsNullOrWhiteSpace(required[i].Id))
                    return null;

                requiredIds.Add(required[i].Id);
            }

            return requiredIds;
        }

        private string GetAnyId(HashSet<string> ids)
        {
            foreach (string id in ids)
            {
                return id;
            }

            return null;
        }

        private HashSet<string> GetReachableMemoryIds(string startId)
        {
            HashSet<string> visited = new HashSet<string>();
            Queue<string> queue = new Queue<string>();

            visited.Add(startId);
            queue.Enqueue(startId);

            while (queue.Count > 0)
            {
                string current = queue.Dequeue();

                if (!connections.TryGetValue(current, out HashSet<string> neighbors))
                    continue;

                foreach (string neighbor in neighbors)
                {
                    if (visited.Add(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return visited;
        }

        private bool AddConnection(string from, string to)
        {
            if (!connections.TryGetValue(from, out HashSet<string> list))
            {
                list = new HashSet<string>();
                connections[from] = list;
            }

            return list.Add(to);
        }

        private bool RemoveConnection(string from, string to)
        {
            if (!connections.TryGetValue(from, out HashSet<string> list))
                return false;

            bool removed = list.Remove(to);

            if (list.Count == 0)
            {
                connections.Remove(from);
            }

            return removed;
        }

        public List<string> GetAllConnectionKeys()
        {
            HashSet<string> result = new();

            foreach (KeyValuePair<string, HashSet<string>> kvp in connections)
            {
                string from = kvp.Key;

                foreach (string to in kvp.Value)
                {
                    string key = GetKey(from, to);
                    result.Add(key);
                }
            }

            return new List<string>(result);
        }

        public void ClearConnections()
        {
            connections.Clear();
        }

        public void RestoreConnection(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            string[] split = key.Split('|');
            if (split.Length != 2)
            {
                Debug.LogError($"[SAVE] Invalid connection key: {key}");
                return;
            }

            ConnectMemories(split[0], split[1]);
        }

        private string GetKey(string a, string b)
        {
            return string.CompareOrdinal(a, b) < 0
                ? $"{a}|{b}"
                : $"{b}|{a}";
        }

        public void ClearMemories()
        {
            memories.Clear();
            hasInitializedAllMemories = false;
        }
    }
}