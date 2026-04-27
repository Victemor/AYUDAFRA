using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ConsciousnessSystem : MonoBehaviour
{
    public static event Action<ThoughtData> OnThoughtAdded;

    [SerializeField]
    [Tooltip("Cantidad máxima de pensamientos almacenados.")]
    private int maxThoughts = 50;

    public struct ThoughtData
    {
        public string Text;
        public float Timestamp;
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

    public void AddThought(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        ThoughtData thought = new ThoughtData
        {
            Text = text,
            Timestamp = Time.time
        };

        thoughts.Add(thought);
        EnforceLimit();

        OnThoughtAdded?.Invoke(thought);

        SaveConsciousnessSafe();
    }

    public IReadOnlyList<ThoughtData> GetAllThoughts()
    {
        return thoughts;
    }

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