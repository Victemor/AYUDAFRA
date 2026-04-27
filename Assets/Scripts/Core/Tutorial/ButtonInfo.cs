using UnityEngine;

public class ButtonInfo : MonoBehaviour
{
    [SerializeField] private DialogController dialogController;

    public void ShowInfo()
    {
        dialogController.ShowDialog(@"-Memory fragments appear as droplets. Click on them to view them.

-Left-click to open their menu, rename them, and connect memories to one another. You can also move them by dragging them.

-Move the mouse and use zoom to explore the environment.

-Click on interactive objects to discover more details about each memory.

-When you interact, wait for the scene to finish before continuing. You can read the consciousness window as you go.

-At certain moments, you will need to interpret what you see: use the slider and choose an emotion according to your perception.

-Your choices will change the meaning of the memories.");
    }
}