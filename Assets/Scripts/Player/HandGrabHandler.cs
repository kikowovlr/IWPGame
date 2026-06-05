using Fusion;
using UnityEngine;
using UnityEngine.Animations.Rigging;

enum GrabState
{
    None, // grab not pressed
    Searching, // grab is pressed - searching for grab target
    Reaching, // grab target found, hands reaching
    Attached // hands attached to the target
}

public enum HandSide
{
    Left,
    Right
}

public class HandGrabHandler : NetworkBehaviour
{
    [Header("IK Settings")]
    [SerializeField] private Animator _animator;
    [SerializeField] private HandSide _handSide;    
    [SerializeField] private float _enterSearchRadius = 2.5f; // enter grabbable obj detection range
    [SerializeField] private float _exitSearchRadius = 3f; // exit grabbable obj detection range (prevents jitter when on edge of radius)
    [SerializeField] private LayerMask _grabbableLayer;
    [SerializeField] private float _ikBlendSpeed = 8f; // how fast arm reaches out
    [SerializeField] private ConfigurableJoint _armJoint;
    [SerializeField] private float _relaxedArmSpring = 10f; // spring strength when trying to reach (allow for arm to reach out)
    private float _originalArmSpring;

    [Header("Animation Rigging Components")]
    [SerializeField] private TwoBoneIKConstraint _handIKConstraint;
    [SerializeField] private Transform _handIKTarget;

    [Header("IK Extension Limits")]
    [SerializeField] private Transform _shoulderPivot;
    [SerializeField] private float _maxArmReach = 1.4f;

    // grabbing
    ConfigurableJoint _grabJoint;
    Rigidbody _rb;
    Collider _handCollider;
    Collider _grabbedCollider;
    private float _originalHandMass;
    private Vector3 _targetIKPosition;
    private Collider _trackedTarget = null;

    // states
    private GrabState _currentGrabState = GrabState.None;

    // references
    NetworkPlayerController _networkPlayer;

    private void Awake()
    {
        _networkPlayer = transform.root.GetComponent<NetworkPlayerController>();
        _rb = GetComponent<Rigidbody>();
        _handCollider = GetComponent<Collider>();
        _originalHandMass = _rb.mass;

        if (_armJoint != null)
        {
            _originalArmSpring = _armJoint.slerpDrive.positionSpring;
        }
    }

    public void UpdateState()
    {
        // check if grabbing is active
        if (_networkPlayer.IsGrabbingActive)
        {
            if (_grabJoint != null)
            {
                // STATE: ATTACHED (holding something)
                _currentGrabState = GrabState.Attached;
                _animator.SetBool("IsGrabbing", true);
                // IsCarrying is handled in TryCarryObject when joint is created

                // keep IK target on hand when attached, so it doesn't interfere with animation
                //_handIKTarget.position = transform.position;

                // FIX: Dynamically clamp the IK target directly to the surface contact zone
                if (_grabbedCollider != null)
                {
                    Vector3 carryLookPoint = _grabbedCollider.ClosestPoint(transform.position);
                    _handIKTarget.position = ClampTargetToArmLength(carryLookPoint);
                }
                else
                {
                    _handIKTarget.position = ClampTargetToArmLength(transform.position);
                }

                // move IK weight to 1 so hand is on object
                _handIKConstraint.weight = Mathf.MoveTowards(_handIKConstraint.weight, 1f, Runner.DeltaTime * _ikBlendSpeed);
            }
            else
            {
                // STATE: SEARCHING/REACHING (grab button pressed but not holding anything)
                // check for grabbable objects in range
                UpdateTrackedTarget();

                if (_trackedTarget != null)
                {
                    // STATE: REACHING - target found, lock IK to it
                    _currentGrabState = GrabState.Reaching;
                    _animator.SetBool("IsGrabbing", true);
                    _animator.SetBool("IsCarrying", false);

                    // Calculate closest point on box and clamp it within arm length
                    Vector3 rawBoxPoint = _trackedTarget.ClosestPoint(transform.position);
                    _targetIKPosition = ClampTargetToArmLength(rawBoxPoint);
                    _handIKTarget.position = _targetIKPosition;
                    //_targetIKPosition = _trackedTarget.ClosestPoint(transform.position);
                    //_handIKTarget.position = _targetIKPosition;

                    // smoothly blend weight to 1
                    _handIKConstraint.weight = Mathf.MoveTowards(_handIKConstraint.weight, 1f, Runner.DeltaTime * _ikBlendSpeed);

                    // relax arm so reaching out is easier
                    RelaxArm(true);
                }
                else
                {
                    // STATE: SEARCHING - no target found, DONT REACH OUT
                    _currentGrabState = GrabState.Searching;
                    _animator.SetBool("IsGrabbing", false); // dont reach out if no target
                    _animator.SetBool("IsCarrying", false);
                    _handIKTarget.position = transform.position;
                    _handIKConstraint.weight = Mathf.MoveTowards(_handIKConstraint.weight, 0f, Runner.DeltaTime * _ikBlendSpeed);

                    RelaxArm(false);
                }
            }
        }
        else
        {
            // STATE: NONE  - grab button released
            _currentGrabState = GrabState.None;
            _handIKConstraint.weight = Mathf.MoveTowards(_handIKConstraint.weight, 0f, Runner.DeltaTime * _ikBlendSpeed);
            RelaxArm(false);

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
            return false;

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
            return false;

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

    private void UpdateTrackedTarget()
    {
        float currentDistance = _trackedTarget != null ? Vector3.Distance(transform.position, _trackedTarget.transform.position) : float.MaxValue;

        // if we are currently tracking a target, check if it has escaped our EXIT radius, if so - stop tracking it
        if (_trackedTarget != null)
        {
            if (currentDistance > _exitSearchRadius)
            {
                _trackedTarget = null; // target too far, stop tracking
            }
            // obj not on our side, stop tracking
            else if (!IsObjectOnMySide(_trackedTarget.transform.position))
            {
                _trackedTarget = null;
            }
        }

        // if we are not tracking a target, check for new targets within our ENTER radius and start tracking the closest one
        if (_trackedTarget == null)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, _enterSearchRadius, _grabbableLayer);
            Collider closest = null;
            float shortestDistance = float.MaxValue;

            foreach (Collider hit in hits)
            {
                if (hit.transform.root == transform.root) 
                    continue;
                if (!hit.TryGetComponent(out Rigidbody _)) 
                    continue;

                // skip this obj entirely if it forces the hand to cross the body to reach it
                if (!IsObjectOnMySide(hit.transform.position)) 
                    continue;

                float distance = Vector3.Distance(transform.position, hit.transform.position);
                if (distance < shortestDistance)
                {
                    shortestDistance = distance;
                    closest = hit;
                }
            }
            _trackedTarget = closest; // start tracking closest target (if any)
        }
    }

    // check if object is on the same side of the player as the hand (prevents grabbing objects on the other side when reaching across body)
    private bool IsObjectOnMySide(Vector3 objPos)
    {
        // get direction from center of player to obj
        Vector3 dirToObj = (objPos - _networkPlayer.transform.position).normalized;

        // calculate dot product relative to player's right vector
        // +ve = object is on the right side, -ve = object is on the left side
        float sideDot = Vector3.Dot(_networkPlayer.transform.right, dirToObj);

        if (_handSide == HandSide.Right)
        {
            // right hand can reach anything on the right and slightly left
            return sideDot >= -0.2f;
        }
        else
        {
            // left hand can reach anyt on the left and slightly right
            return sideDot <= 0.2f;
        }
    }

    private Vector3 ClampTargetToArmLength(Vector3 desiredPosition)
    {
        Vector3 originPoint = _shoulderPivot != null ? _shoulderPivot.position : _networkPlayer.transform.position + (Vector3.up * 1f);
        Vector3 offset = desiredPosition - originPoint;

        if (offset.magnitude > _maxArmReach)
        {
            offset = offset.normalized * _maxArmReach;
        }

        return originPoint + offset;
    }

    private void RelaxArm(bool relax)
    {
        if (_armJoint == null)
            return;

        JointDrive drive = _armJoint.slerpDrive;
        drive.positionSpring = relax ? _relaxedArmSpring : _originalArmSpring;
        _armJoint.slerpDrive = drive;
    }

    private void OnCollisionEnter(Collision other)
    {
        // attempt to carry obj
        TryCarryObject(other);
    }

    private void OnDrawGizmos()
    {
        // visualize grab radius
        //Gizmos.color = Color.green;
        //Gizmos.DrawWireSphere(transform.position, _enterSearchRadius);
        //Gizmos.color = Color.red;
        //Gizmos.DrawWireSphere(transform.position, _exitSearchRadius);

        // visualize IK target when reaching
        if (_currentGrabState == GrabState.Reaching)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(_targetIKPosition, 0.05f);
        }
    }
}