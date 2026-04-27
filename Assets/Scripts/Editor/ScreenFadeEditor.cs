using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ScreenFade))]
public class ScreenFadeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ScreenFade fade = (ScreenFade)target;

        GUILayout.Space(10);
        GUILayout.Label("Test Transitions", EditorStyles.boldLabel);

        if (GUILayout.Button("Fade In (negro)"))
        {
            fade.FadeIn(this.name);
        }

        if (GUILayout.Button("Fade Out (transparente)"))
        {
            fade.FadeOut();
        }

        if (GUILayout.Button("Blink (parpadeo)"))
        {
            fade.Blink();
        }
    }
}
