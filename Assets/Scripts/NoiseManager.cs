using System;
using UnityEngine;
public static class NoiseManager
{
    public static event Action<Vector3, float> OnNoiseMade;
    public static void MakeNoise(Vector3 position, float radius)
    {
        
        // to any script that is subscribed This invokes the event, sending the position and radius
        OnNoiseMade?.Invoke(position, radius);
    }
}