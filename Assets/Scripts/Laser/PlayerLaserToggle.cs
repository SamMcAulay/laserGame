using UnityEngine;
using UnityEngine.InputSystem;

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
            this.enabled = false; 
            return;
        }

        // Ensure the laser starts in the 'off' state.
        _isLaserOn = false;
        laserPointer.ToggleLaser(false);
    }

        
    private void OnToggleLaser(InputValue value)
    {
        if (laserPointer == null) return;

        // The value.isPressed check ensures this only triggers once per button press
        if (value.isPressed)
        {
            // Invert the laser's state
            _isLaserOn = !_isLaserOn;

            // Apply the new state to the actual laser script.
            laserPointer.ToggleLaser(_isLaserOn);
        }
    }

    // This function handles the scroll wheel input.
    private void OnAdjustLaser(InputValue value)
    {
        if (laserPointer == null) return;

        // Get the scroll wheel's Y-axis movement
        float scrollInput = value.Get<Vector2>().y;

        // Check if the user is scrolling
        if (scrollInput != 0f)
        {
            // Send the normalized direction to the laser script.
            laserPointer.AdjustSpread(Mathf.Sign(scrollInput));
        }
    }
}