using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

/// <summary>
/// Sistema singleton que almacena y notifica el historial de pensamientos del jugador.
///
/// Módulo 3 — Persistencia Intro → Menú:
/// El atributo <c>[DefaultExecutionOrder(-200)]</c> garantiza que este Awake()
/// corre ANTES que GameManager (orden 0), que llama SaveSystem.LoadGame() →
/// RestoreConsciousness(). Sin este orden, Instance sería null durante la
/// restauración y los pensamientos guardados se perderían silenciosamente.
///
/// El objeto se crea en la escena Menú y persiste en todas las escenas
/// posteriores via DontDestroyOnLoad. No debe moverse a la escena Intro.
/// </summary>
[DefaultExecutionOrder(-200)]
public sealed class ConsciousnessSystem : MonoBehaviour
{
    public static event Action<ThoughtData> OnThoughtAdded;

    [SerializeField]
    [Tooltip("Cantidad máxima de pensamientos almacenados.")]
    private int maxThoughts = 50;

    /// <summary>
    /// Datos de un pensamiento. Soporta referencia por nombre de clave (Key),
    /// por ID numérico (KeyId) o texto raw sin localización.
    /// </summary>
    public struct ThoughtData
    {
        /// <summary>Nombre de la tabla de localización.</summary>
        public string TableName;

        /// <summary>Clave string de la entrada. Puede estar vacío si KeyId > 0.</summary>
        public string Key;

        /// <summary>ID numérico de la entrada. Usado cuando Key está vacío.</summary>
        public long KeyId;

        /// <summary>Texto plano sin localización.</summary>
        public string RawText;

        public float Timestamp;

        /// <summary>True si tiene referencia de localización válida.</summary>
        public bool IsLocalized =>
            !string.IsNullOrWhiteSpace(TableName) &&
            (!string.IsNullOrWhiteSpace(Key) || KeyId > 0);

        /// <summary>
        /// Construye el LocalizedString correcto según el tipo de referencia.
        /// Solo llamar cuando IsLocalized es true.
        /// </summary>
        public LocalizedString ToLocalizedString()
        {
            if (!string.IsNullOrWhiteSpace(Key))
            {
                return new LocalizedString(TableName, Key);
            }

            return new LocalizedString(TableName, KeyId);
        }
    }

    private readonly List<ThoughtData> thoughts = new();

    public static ConsciousnessSystem Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Registra un pensamiento pasando el LocalizedString directamente.
    /// Maneja referencias por nombre de clave y por ID numérico.
    /// </summary>
    public void AddThought(LocalizedString localizedString)
    {
        if (localizedString == null || localizedString.IsEmpty)
        {
            Debug.LogWarning("[ConsciousnessSystem] LocalizedString nulo o vacío.");
            return;
        }

        string tableName = localizedString.TableReference.TableCollectionName;
        string key       = localizedString.TableEntryReference.Key;
        long   keyId     = localizedString.TableEntryReference.KeyId;

        if (string.IsNullOrWhiteSpace(tableName))
        {
            Debug.LogWarning("[ConsciousnessSystem] TableName vacío en LocalizedString.");
            return;
        }

        if (string.IsNullOrWhiteSpace(key) && keyId <= 0)
        {
            Debug.LogWarning("[ConsciousnessSystem] Ni Key ni KeyId son válidos.");
            return;
        }

        AddThoughtInternal(new ThoughtData
        {
            TableName = tableName,
            Key       = key,
            KeyId     = keyId,
            Timestamp = Time.time
        });
    }

    /// <summary>
    /// Registra un pensamiento usando tabla + clave string explícitos.
    /// </summary>
    public void AddThought(string tableName, string key)
    {
        if (string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(key))
        {
            Debug.LogWarning("[ConsciousnessSystem] Referencia de localización inválida.");
            return;
        }

        AddThoughtInternal(new ThoughtData
        {
            TableName = tableName,
            Key       = key,
            Timestamp = Time.time
        });
    }

    /// <summary>
    /// Registra un pensamiento con texto plano sin localización.
    /// </summary>
    public void AddThoughtRaw(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            Debug.LogWarning("[ConsciousnessSystem] Texto raw vacío.");
            return;
        }

        AddThoughtInternal(new ThoughtData
        {
            RawText   = rawText,
            Timestamp = Time.time
        });
    }

    public IReadOnlyList<ThoughtData> GetAllThoughts() => thoughts;

    public void Clear()
    {
        thoughts.Clear();
        SaveConsciousnessSafe();
    }

    public void RestoreThoughts(IEnumerable<ThoughtData> restoredThoughts, bool notifyListeners)
    {
        thoughts.Clear();

        if (restoredThoughts == null)
        {
            return;
        }

        foreach (ThoughtData thought in restoredThoughts)
        {
            thoughts.Add(thought);
        }

        EnforceLimit();

        if (notifyListeners)
        {
            foreach (ThoughtData thought in thoughts)
            {
                OnThoughtAdded?.Invoke(thought);
            }
        }
    }

    private void AddThoughtInternal(ThoughtData thought)
    {
        thoughts.Add(thought);
        EnforceLimit();
        OnThoughtAdded?.Invoke(thought);
        SaveConsciousnessSafe();
    }

    private void EnforceLimit()
    {
        if (thoughts.Count <= maxThoughts)
            return;

        int overflow = thoughts.Count - maxThoughts;
        thoughts.RemoveRange(0, overflow);
    }

    private void SaveConsciousnessSafe()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SaveProgress();
        }
    }
}