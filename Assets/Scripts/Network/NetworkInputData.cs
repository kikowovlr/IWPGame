using Fusion;
using UnityEngine;

public struct NetworkInputData : INetworkInput
{
    // TODO optimise by sending bytes instead of vector2
    public Vector2 _movementInput;
    public NetworkBool _isJumpPressed;
    public NetworkBool _isSprintPressed;
    public NetworkBool _isPunchOrGrabPressed;
    public NetworkBool _isThrowPressed;
    public NetworkBool _isRagdollPressed; // for testing ragdoll state sync
}
