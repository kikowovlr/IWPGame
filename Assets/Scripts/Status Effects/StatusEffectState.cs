using Fusion;
using UnityEngine;

public struct StatusEffectState : INetworkStruct
{
    public StatusEffectType _type;
    public TickTimer _remainingTime;

    // helper to check if effect is still active
    public bool IsActive(NetworkRunner runner) => !_remainingTime.Expired(runner);
}
