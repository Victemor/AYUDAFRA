using TamarilloGames.Core.GameFramework;
using UnityEngine;

public class SceneController_MainMenu : SceneController
{
    [SerializeField] private string gameplayScene = "Scene_Gameplay";
    public void OnUIPlayButtonClicked()
    {
        Debug.Log($"[SceneController_MainMenu] User clicked play button. Requesting scene change to '{gameplayScene}'.");
        RequestNewActiveScene(new SceneLoadRequest(gameplayScene));
    }
}