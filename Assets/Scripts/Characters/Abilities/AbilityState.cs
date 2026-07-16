using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// tracks data per player across network
/// holds live, changing numbers
/// </summary>
public struct AbilityState : INetworkStruct
{
    public float _cooldownTimer;
    public float _chargeTime;
    public int _activationId; // tracks current cast number
    public Vector3 _customVelocity; // to send physics calculations to the player controller script
    public NetworkBool _isCasting; // for one shot skills

    // for visuals
    public float _visualTime;
    public bool _isVisualShown;

    // for goat
    public NetworkBool _isCharging;
    public NetworkBool _isDashing;
    public float _dashDurationTimer;
    public int _noFloorTickCount;

    // to keep track of hit targets
    // fixed capacity array to avoid heap garbage, will only store the last 8 hit targets
    [Networked, Capacity(8)] public NetworkArray<NetworkId> _abilityHitHistory => default;
    public int _hitCount;
}