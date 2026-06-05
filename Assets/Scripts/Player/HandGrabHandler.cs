using UnityEngine;

enum GrabState
{
    None, // grab not pressed
    Searching, // grab is pressed - searching for grab target
    Reaching, // grab target found, hands reaching
    Attached // hands attached to the target
}

public class HandGrabHandler : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    // fixed joint created on the fly to attach the hand to the target
    // -> cannot be enabled or disabled
    //FixedJoint _fixedJoint;
    ConfigurableJoint _grabJoint;
    Rigidbody _rb;
    Collider _handCollider;
    Collider _grabbedCollider;
    private float _originalHandMass;

    // references
    NetworkPlayerController _networkPlayer;

    private void Awake()
    {
        _networkPlayer = transform.root.GetComponent<NetworkPlayerController>();
        _rb = GetComponent<Rigidbody>();
        _handCollider = GetComponent<Collider>();

        _originalHandMass = _rb.mass;

        // change solver iterations to prevent joint from flexing too much
        _rb.solverIterations = 10;
    }

    public void UpdateState()
    {
        // check if grabbing is active
        if (_networkPlayer.IsGrabbingActive)
        {
            _animator.SetBool("IsGrabbing", true);
        }
        else
        {
            // grab is let go off
            // check if there is a joint to destroy
            if (_grabJoint != null)
            {
                // give connect rb a bit of force when we let go
                float forceMultiplier = 0.1f;

                if (_grabbedCollider != null)
                {
                    Physics.IgnoreCollision(_handCollider, _grabbedCollider, false);
                    _grabbedCollider = null;
                }

                if (_grabJoint.connectedBody != null)
                {
                    // get other player
                    if (_grabJoint.connectedBody.TryGetComponent(out NetworkPlayerController otherPlayer))
                    {
                        // check status of other player
                        if (otherPlayer.IsActiveRagdoll)
                            forceMultiplier = 10;
                        else
                            // easier to toss knocked out players
                            forceMultiplier = 15;
                    }

                    // toss object away b4 we remove joint
                    _grabJoint.connectedBody.AddForce((_networkPlayer.transform.forward + Vector3.up * 0.25f) * forceMultiplier, ForceMode.Impulse);
                }

                Destroy(_grabJoint);
                _rb.mass = _originalHandMass;
            }

            // change animation state
            _animator.SetBool("IsCarrying", false);
            _animator.SetBool("IsGrabbing", false);
        }
    }

    bool TryCarryObject(Collision other)
    {
        // check if we are even allowed to carry objects
        if (!_networkPlayer.Object.HasStateAuthority)
            return false;

        // check that we are not in active ragdoll mode
        if (!_networkPlayer.IsActiveRagdoll)
        {
            Utils.DebugLogWarning("not active");
            return false;
        }

        // check that we are trying to grab something
        if (!_networkPlayer.IsGrabbingActive)
            return false;

        // check if we are alrdy carrying another object
        if (_grabJoint != null)
            return false;

        // avoid trying to grab yourself
        if (other.transform.root == _networkPlayer.transform)
            return false;

        // get other rigidbodies if there is one (only able to grab objects with rigidbodies)
        if (!other.collider.TryGetComponent(out Rigidbody otherRb))
        {
            Utils.DebugLogWarning("no rb");
            return false;
        }

        Utils.DebugLog("grabbing");

        // temporarily increase mass of hand
        _rb.mass = otherRb.mass * 2.0f;
        _grabbedCollider = other.collider;
        Physics.IgnoreCollision(_handCollider, _grabbedCollider, true);

        // add fixed joint
        _grabJoint = transform.gameObject.AddComponent<ConfigurableJoint>();

        // connect joint to other rigidbody
        _grabJoint.connectedBody = otherRb;
        _grabJoint.xMotion = ConfigurableJointMotion.Locked;
        _grabJoint.yMotion = ConfigurableJointMotion.Locked;
        _grabJoint.zMotion = ConfigurableJointMotion.Locked;
        _grabJoint.angularXMotion = ConfigurableJointMotion.Locked;
        _grabJoint.angularYMotion = ConfigurableJointMotion.Locked;
        _grabJoint.angularZMotion = ConfigurableJointMotion.Locked;

        // take care of anchor point on our own
        _grabJoint.autoConfigureConnectedAnchor = false;

        // transform collision point from world to local space of the hand
        // -> InverseTransformPoint takes World Pos & converts to Local Pos of OTHER object
        _grabJoint.connectedAnchor = other.transform.InverseTransformPoint(other.GetContact(0).point);

        _grabJoint.projectionMode = JointProjectionMode.PositionAndRotation;
        _grabJoint.projectionDistance = 0.01f; // Snaps back if it stretches more than 1 centimeter
        _grabJoint.projectionAngle = 1f;

        // set animator to carrying
        _animator.SetBool("IsCarrying", true);
        return true;
    }

    private void OnCollisionEnter(Collision other)
    {
        // attempt to carry obj
        TryCarryObject(other);
    }
}