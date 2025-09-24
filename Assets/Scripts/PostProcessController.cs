using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Volume))]
public class PostProcessController : MonoBehaviour
{
    [Tooltip("Assign your PostProcess Settings ScriptableObject here.")]
    public PostProcessSettings settingsProfile;

    private Volume _postProcessVolume;
    private Bloom _bloomEffect;

    void Awake()
    {
        // Get the Volume component attached to this GameObject.
        _postProcessVolume = GetComponent<Volume>();

        // Try to get the Bloom effect from the volume's profile.
        // This requires you to have already added a Bloom override in the Inspector.
        if (_postProcessVolume.profile.TryGet(out _bloomEffect))
        {
            ApplySettings();
        }
        else
        {
            Debug.LogError("Bloom effect not found on the Volume Profile. Please add a Bloom override in the Inspector.");
        }
    }

    // This method can be called anytime you want to apply the settings.
    private void ApplySettings()
    {
        if (settingsProfile == null || _bloomEffect == null)
        {
            Debug.LogWarning("Settings Profile or Bloom effect is not assigned!");
            return;
        }

        // Apply all the values from the ScriptableObject to the Bloom effect.
        _bloomEffect.active = settingsProfile.enableBloom;
        _bloomEffect.intensity.value = settingsProfile.intensity;
        _bloomEffect.threshold.value = settingsProfile.threshold;
        _bloomEffect.tint.value = settingsProfile.tint;
        _bloomEffect.scatter.value = settingsProfile.scatter;

        Debug.Log("Applied Post-Process settings from: " + settingsProfile.name);
    }
}