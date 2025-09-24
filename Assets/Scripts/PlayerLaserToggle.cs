using UnityEngine;
using UnityEngine.InputSystem; // Required for InputValue

/// <summary>
/// Toggles a laser pointer on and off. This script is designed to work with
/// the 'Player Input' component's "Send Messages" behavior. The 'OnToggleLaser'
/// method is automatically called by name.
/// </summary>
public class PlayerLaserToggle : MonoBehaviour
{
    [Tooltip("The LaserPointer script that this script will control.")]
    [SerializeField] private LaserPointer laserPointer;

    private bool _isLaserOn;

    void Start()
    {
        // Initial check to ensure the laser pointer is assigned.
        if (laserPointer == null)
        {
            Debug.LogError("ERROR: The LaserPointer has not been assigned in the Inspector on the Player object!", this);
            this.enabled = false; // Disable component to prevent further errors.
            return;
        }

        // Ensure the laser starts in the 'off' state.
        laserPointer.ToggleLaser(false);
    }

    //  matches the action's name (On + ActionName).
    private void OnToggleLaser(InputValue value)
    {
        // The value.isPressed check ensures this only triggers once per button press.
        if (value.isPressed)
        {
            // Invert the laser's state (on to off, or off to on).
            _isLaserOn = !_isLaserOn;

            // Apply the new state to the actual laser script.
            laserPointer.ToggleLaser(_isLaserOn);
        }
    }
}