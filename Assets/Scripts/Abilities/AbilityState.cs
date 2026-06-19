using Fusion;

/// <summary>
/// tracks data per player across network
/// holds live, changing numbers
/// </summary>
public struct AbilityState : INetworkStruct
{
    public float _cooldownTimer;
    public float _chargeTime;
    public NetworkBool _isCharging;
}