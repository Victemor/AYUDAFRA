using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Runtime;
using Game.Data;

public class SceneLoader : MonoBehaviour
{
    public void LoadMemory(MemoryDefinition memory)
    {
        var runtime = GameStateRepository.Instance.GetMemory(memory);

        if (runtime.CurrentState == MemoryState.Locked)
        {
            Debug.Log("Memory locked");
            return;
        }

        SceneManager.LoadScene(memory.Id);
    }
}