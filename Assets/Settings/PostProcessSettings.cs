using UnityEngine;

// This attribute allows you to create instances of this object from the Asset menu.
[CreateAssetMenu(fileName = "New PostProcess Settings", menuName = "Settings/Post Processing Profile")]
public class PostProcessSettings : ScriptableObject
{
    [Header("Bloom Settings")]
    public bool enableBloom = true;

    [Tooltip("Strength of the bloom effect.")]
    [Range(0f, 10f)]
    public float intensity = 1.0f;

    [Tooltip("Filters out pixels under this level of brightness.")]
    [Range(0f, 10f)]
    public float threshold = 0.5f;

    [Tooltip("Changes the bloom color tint.")]
    [ColorUsage(true, true)] // This attribute enables the HDR color picker.
    public Color tint = Color.white;

    [Tooltip("Higher values will create a wider, softer bloom.")]
    [Range(0f, 10f)]
    public float scatter = 0.7f;
}