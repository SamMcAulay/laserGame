using UnityEngine;
using UnityEngine.InputSystem;
[RequireComponent(typeof(Light))]
public class LaserController : MonoBehaviour
{
    [Header("Laser Beam Settings")]
    [Tooltip("The narrowest angle of the spotlight cone (at max intensity).")]
    [SerializeField] private float minOuterAngle = 1f;

    [Tooltip("The widest angle of the spotlight cone (at min intensity).")]
    [SerializeField] private float maxOuterAngle = 80f;

    [Tooltip("The minimum intensity of the light when the cone is at its widest.")]
    [SerializeField] private float minIntensity = 1000f;

    [Tooltip("The maximum intensity of the light when the cone is at its narrowest.")]
    [SerializeField] private float maxIntensity = 1000000f;

    [Header("Control Settings")]
    [Tooltip("How sensitive the adjustment is to the mouse scroll wheel.")]
    [SerializeField] private float scrollSensitivity = 3f;

    
    private Light _spotLight;
    private float _currentBeamFactor = 0f; 
    
    void Awake()
    {
       
        _spotLight = GetComponent<Light>();

       
        if (_spotLight.type != LightType.Spot)
        {
            Debug.LogError("This script requires a 'Spot' type Light component.", this);
            this.enabled = false; 
        }
    }
    void Start()
    {
        UpdateLightProperties();
    }
    
    void Update()
    {
        
        if (Mouse.current == null)
        {
            return;
        }
        
        float scrollValue = Mouse.current.scroll.ReadValue().y;
        
        if (Mathf.Abs(scrollValue) > 0.01f)
        {
            
            _currentBeamFactor += (scrollValue / 120f) * scrollSensitivity;
            
            _currentBeamFactor = Mathf.Clamp01(_currentBeamFactor);
            
            UpdateLightProperties();
        }
    }
    
    private void UpdateLightProperties()
    {
        _spotLight.spotAngle = Mathf.Lerp(minOuterAngle, maxOuterAngle, _currentBeamFactor);

        // Linearly interpolate the intensity in the reverse direction.
        // As _currentBeamFactor goes from 0 to 1, the intensity goes from max to min.
        _spotLight.intensity = Mathf.Lerp(maxIntensity, minIntensity, _currentBeamFactor);
    }
}
