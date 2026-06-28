using Fusion;

/// <summary>
/// tracks data per player across network
/// holds live, changing numbers
/// </summary>
public struct AbilityState : INetworkStruct
{
    public float _cooldownTimer;
    public float _chargeTime;
    public int _activationId; // tracks current cast number

    // for goat
    public NetworkBool _isCharging;
    public NetworkBool _isDashing;
    public float _dashDurationTimer;
}