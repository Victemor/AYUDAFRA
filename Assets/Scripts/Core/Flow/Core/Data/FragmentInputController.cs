using UnityEngine;
using Game.Data;
/// <summary>
/// Controlador centralizado de inputs para el sistema de acciones.
/// Permite desacoplar la detección de input del flujo de acciones.
/// </summary>
public class FragmentInputController : MonoBehaviour
{
    public static FragmentInputController Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    /// <summary>
    /// Evalúa si el input solicitado fue activado.
    /// </summary>
    public bool IsInputTriggered(InputType type, KeyCode key)
    {
        switch (type)
        {
            case InputType.AnyKey:
                return Input.anyKeyDown;

            case InputType.SpecificKey:

                return Input.GetKeyDown(key);

            case InputType.MouseClick:
                return Input.GetMouseButtonDown(0);
        }

        return false;
    }
}