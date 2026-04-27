using Game.Runtime;
using Game.Data;

namespace Game
{
    /// <summary>
    /// Contexto compartido para evaluación de condiciones y ejecución de acciones.
    /// </summary>
    public class RuntimeContext
    {
        public GameStateRepository Repository { get; }

        public RuntimeContext(GameStateRepository repository)
        {
            Repository = repository;
        }
    }
}