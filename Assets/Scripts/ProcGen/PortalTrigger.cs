using UnityEngine;

namespace ProcGen
{
    public class PortalTrigger : MonoBehaviour
    {
        private bool hasBeenTriggered = false;

        private void OnTriggerEnter(Collider other)
        {
            // Check if the player entered the trigger and it hasn't been used yet
            if (!hasBeenTriggered && other.CompareTag("Player"))
            {
                hasBeenTriggered = true; // Prevents triggering multiple times
            
                // Tell the FloorGenerator to advance
                if (FloorGenerator.Instance != null)
                {
                    FloorGenerator.Instance.GoToNextLevel();
                }
            }
        }
    }
}