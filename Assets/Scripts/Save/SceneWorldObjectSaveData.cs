using System;
using UnityEngine;

namespace Game.Save
{
    /// <summary>
    /// DTO persistente de un objeto físico de escena.
    /// Se usa para objetos que no forman parte del runtime narrativo.
    /// </summary>
    [Serializable]
    public class SceneWorldObjectSaveData
    {
        public string sceneName;
        public string worldObjectId;

        public bool hasVisible;
        public bool visible;

        public bool hasColliderEnabled;
        public bool colliderEnabled;

        public bool hasWorldPosition;
        public Vector3 worldPosition;
    }
}