using UnityEngine;
using UnityEngine.Serialization;

namespace ProcGen
{
    public class Room : MonoBehaviour
    { 
        [Header("Door Markers")] [Tooltip("Door Markers")]
        public GameObject Door_North;
        public GameObject Door_South;
        public GameObject Door_East;
        public GameObject Door_West;

    }
}
