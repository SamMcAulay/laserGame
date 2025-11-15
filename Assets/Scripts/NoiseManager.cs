using System;
using UnityEngine;

/// <summary>
/// A static (global) class that allows any script to "make noise"
/// and any other script to "listen" for that noise.
/// </summary>
public static class NoiseManager
{
    // A C# "event" that takes two parameters:
    // 1. Vector3: The position where the sound was made.
    // 2. float: The radius (range) of the sound.
    public static event Action<Vector3, float> OnNoiseMade;

    /// <summary>
    /// Call this from any script to broadcast a sound.
    /// </summary>
    /// <param name="position">The world position of the sound's origin</param>
    /// <param name="radius">How far (in meters) the sound travels</param>
    public static void MakeNoise(Vector3 position, float radius)
    {
        // This "invokes" the event, sending the position and radius
        // to any script that is "subscribed" (like our EnemyAI).
        OnNoiseMade?.Invoke(position, radius);
    }
}