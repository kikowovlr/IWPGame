using System;
using UnityEngine;

public static class PlayerRegistry
{
    public static event Action<Transform> OnLocalPlayerSpawned;

    public static void RegisterLocalPlayerTransform(Transform transform)
    {
        OnLocalPlayerSpawned?.Invoke(transform);
    }
}
