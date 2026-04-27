// Game/Runtime/ActRuntimeData.cs

using System;

namespace Game.Runtime
{
    [Serializable]
    public class ActRuntimeData
    {
        public string actId;
        public bool hasExecuted;

        public ActRuntimeData(string id)
        {
            actId = id;
            hasExecuted = false;
        }
    }
}