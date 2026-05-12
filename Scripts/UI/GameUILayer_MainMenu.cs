using TamarilloGames.Core.GameFramework;
using TamarilloGames.Core.UI;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameUILayer_MainMenu : GameUILayer_Canvas
{
    [SerializeField] private Button playButton;

    void OnEnable()
    {
        playButton.onClick.AddListener(OnPlayButtonClicked);
    }

    void OnDisable()
    {
        playButton.onClick.RemoveListener(OnPlayButtonClicked);
    }

    private void OnPlayButtonClicked()
    {
        // Notify the scene controller that the user has clicked the play button
        SceneController.GetInstance<SceneController_MainMenu>().OnUIPlayButtonClicked();
    }
}