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
    public NetworkBool _isKickPressed;
    public NetworkBool _isHeadbuttPressed;
    public NetworkBool _isRagdollPressed; // for testing ragdoll state sync

    // ability inputs
    public NetworkBool _abilityPressed; // true on exact tick when ability btn is pressed
    public NetworkBool _abilityHeld;
    public NetworkBool _abilityReleased; // true on exact tick when ability btn is let go off
    public Vector2 _abilityAimDirection; // for aiming skill independent of movement
}
