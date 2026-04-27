using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Referencia serializable a una escena desde el Inspector.
/// </summary>
[System.Serializable]
public class SceneReference
{
    [SerializeField] private Object sceneAsset;
    [SerializeField] private string sceneName;

    public string SceneName => sceneName;

#if UNITY_EDITOR
    public void Validate()
    {
        if (sceneAsset != null)
        {
            sceneName = sceneAsset.name;
        }
    }
#endif
}