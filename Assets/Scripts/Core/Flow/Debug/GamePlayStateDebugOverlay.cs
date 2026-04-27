using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Muestra en pantalla el estado actual del GamePlayStateController.
    /// Ideal para debugging en runtime sin depender de logs.
    /// </summary>
    public sealed class GamePlayStateDebugOverlay : MonoBehaviour
    {
        [Header("Config")]

        [SerializeField]
        [Tooltip("Activa o desactiva el overlay.")]
        private bool showOverlay = true;

        [SerializeField]
        [Tooltip("Posición del overlay en pantalla.")]
        private Vector2 screenOffset = new Vector2(10f, 10f);

        [SerializeField]
        [Tooltip("Tamaño de fuente.")]
        private int fontSize = 14;

        private GUIStyle style;

        private void Awake()
        {
            style = new GUIStyle
            {
                fontSize = fontSize,
                normal = { textColor = Color.white }
            };
        }

        private void OnGUI()
        {
            if (!showOverlay)
                return;

            if (GamePlayStateController.Instance == null)
                return;

            var snapshot = GamePlayStateController.Instance.GetSnapshot();

            string debugText =
                $"STATE: {snapshot.State}\n" +
                $"SUBSTATE: {snapshot.SubState}\n" +
                $"Exploration: {snapshot.IsExplorationAvailable}\n" +
                $"ActionFlow: {snapshot.IsInActionFlow}\n" +
                $"Cinematic: {snapshot.IsInCinematic}\n" +
                $"Dialogue: {snapshot.IsInDialogue}\n" +
                $"InputBlocked: {snapshot.IsInputBlocked}";

            GUI.Label(
                new Rect(screenOffset.x, screenOffset.y, 400f, 200f),
                debugText,
                style
            );
        }
    }
}