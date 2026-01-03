using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class LaserPointer : MonoBehaviour
{
    [Header("Spread Settings")]
    [Tooltip("The minimum spread angle (a tight beam).")]
    [SerializeField] private float minAngle = 1.0f;

    [Tooltip("The maximum spread angle (a wide cone).")]
    [SerializeField] private float maxAngle = 35.0f;

    [Header("Control Settings")]
    [Tooltip("How many degrees the angle changes 'per scroll click'.")]
    [SerializeField] private float spreadAdjustmentStep = 2.0f;

    // --- Cached References ---
    private ParticleSystem laserParticleSystem;
    private ParticleSystem.ShapeModule shapeModule;

    // --- State ---
    private float currentAngle;

    void Awake()
    {
        laserParticleSystem = GetComponent<ParticleSystem>();
        shapeModule = laserParticleSystem.shape;
        currentAngle = shapeModule.angle;
    }

    // This is called by PlayerLaserToggle
    public void AdjustSpread(float scrollDirection)
    {
        currentAngle -= scrollDirection * spreadAdjustmentStep;

        // Clamp the value
        currentAngle = Mathf.Clamp(currentAngle, minAngle, maxAngle);

        // Apply the new angle
        shapeModule.angle = currentAngle;
    }
    
    public float GetCurrentAngle()
    {
        return currentAngle;
    }
    
    // This function is called by PlayerLaserToggle
    public void ToggleLaser(bool isEnabled)
    {
        if (isEnabled)
        {
            laserParticleSystem.Play();
        }
        else
        {
            // Stop emitting and clear all existing particles
            laserParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}