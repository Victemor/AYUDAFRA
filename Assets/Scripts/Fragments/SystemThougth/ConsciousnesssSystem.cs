using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

/// <summary>
/// Sistema singleton que almacena y notifica el historial de pensamientos del jugador.
///
/// Soporta dos rutas para agregar pensamientos:
/// - <b>Localizada</b>: <see cref="AddThought(string, string)"/> — recibe tabla + clave.
///   El texto se resuelve al idioma activo en el momento de mostrarse, lo que permite
///   cambiar de idioma sin invalidar el historial.
/// - <b>Raw (fallback)</b>: <see cref="AddThoughtRaw"/> — recibe texto plano directamente.
///   Usado por contenido aún no migrado a la tabla de localización.
///   El texto NO cambia al cambiar de idioma.
/// </summary>
public sealed class ConsciousnessSystem : MonoBehaviour
{
    public static event Action<ThoughtData> OnThoughtAdded;

    [SerializeField]
    [Tooltip("Cantidad máxima de pensamientos almacenados.")]
    private int maxThoughts = 50;

    /// <summary>
    /// Datos de un pensamiento. Admite ruta localizada (TableName + Key)
    /// o ruta raw (RawText) como fallback para contenido no migrado.
    /// </summary>
    public struct ThoughtData
    {
        /// <summary>Nombre de la tabla de localización. Vacío si es raw.</summary>
        public string TableName;

        /// <summary>Clave en la tabla. Vacía si es raw.</summary>
        public string Key;

        /// <summary>
        /// Texto plano para pensamientos no localizados (fallback).
        /// Vacío si el pensamiento usa la ruta localizada.
        /// </summary>
        public string RawText;

        public float Timestamp;

        /// <summary>
        /// True si este pensamiento tiene referencia de localización válida.
        /// False implica que debe usarse <see cref="RawText"/> directamente.
        /// </summary>
        public bool IsLocalized =>
            !string.IsNullOrWhiteSpace(TableName) &&
            !string.IsNullOrWhiteSpace(Key);

        /// <summary>
        /// Crea el <see cref="LocalizedString"/> a partir de la referencia almacenada.
        /// Solo llamar cuando <see cref="IsLocalized"/> es true.
        /// </summary>
        public LocalizedString ToLocalizedString() => new LocalizedString(TableName, Key);
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
    /// Registra un pensamiento localizado usando tabla + clave.
    /// El texto se resuelve al idioma activo cada vez que se muestra.
    /// </summary>
    public void AddThought(string tableName, string key)
    {
        if (string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(key))
        {
            Debug.LogWarning("[ConsciousnessSystem] Referencia de localización inválida (tabla o clave vacía).");
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
    /// Registra un pensamiento con texto plano (fallback para contenido no localizado).
    /// Usar mientras el contenido no haya sido migrado a la tabla de localización.
    /// </summary>
    public void AddThoughtRaw(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            Debug.LogWarning("[ConsciousnessSystem] Se intentó agregar un pensamiento raw vacío.");
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