using UnityEngine;

public class ActiveRagdollMember : MonoBehaviour
{
    public Rigidbody _animatedRb;// to copy rotation from animated
    public Transform _animatedRoot;
    public Transform _physicalRoot;
    [SerializeField] bool _syncAnimation = false;
    Rigidbody _rb;
    ConfigurableJoint _joint;
    private JointDrive _cachedSlerpDrive;
    private bool _hasCachedDrive;

    [SerializeField] Quaternion _startLocalRotation; // keep track of starting rotation
    [SerializeField] float _startSlerpPositionSpring = 0.0f;
    [SerializeField] bool _isSetupInEditor = false;

    [SerializeField, HideInInspector] Quaternion _worldRotationOffset; // saves the exact difference between the animated bone and physical bone

    [ContextMenu("Capture Pristine Bind-Pose (CLICK ME)")]
    public void SetupBaselineInEditor()
    {
        _joint = GetComponent<ConfigurableJoint>();
        if (_joint != null)
        {
            // CRITICAL FIX: Calculate the starting rotation relative to the physics anchor (Chest)
            // instead of the raw hierarchy parent (Clavicle)!
            Transform physicalAnchor = _joint.connectedBody != null ? _joint.connectedBody.transform : transform.parent;
            _startLocalRotation = Quaternion.Inverse(physicalAnchor.rotation) * transform.rotation;

            if (_animatedRb != null)
                _worldRotationOffset = Quaternion.Inverse(_animatedRb.transform.rotation) * transform.rotation;
            else
                _worldRotationOffset = Quaternion.identity;

            _startSlerpPositionSpring = _joint.slerpDrive.positionSpring;
            _isSetupInEditor = true;
            Debug.Log($"[Ragdoll] Manually captured pristine bind-pose for {gameObject.name} relative to {physicalAnchor.name}: {_startLocalRotation.eulerAngles}");
        }
    }

    private void Awake()
    {
        StartCoroutine(DelayedInitialization());
    }

    private System.Collections.IEnumerator DelayedInitialization()
    {
        // Wait one frame to ensure the Animator has processed the pose
        yield return null;
        InitializeIfNotDone();
    }

    public void InitializeIfNotDone()
    {
        _rb = GetComponent<Rigidbody>();
        _joint = GetComponent<ConfigurableJoint>();

        // Only capture at runtime if we forgot to do it in the Editor!
        if (!_isSetupInEditor && _joint != null)
        {
            Transform physicalAnchor = _joint.connectedBody != null ? _joint.connectedBody.transform : transform.parent;
            _startLocalRotation = Quaternion.Inverse(physicalAnchor.rotation) * transform.rotation;

            if (_animatedRb != null)
                _worldRotationOffset = Quaternion.Inverse(_animatedRb.transform.rotation) * transform.rotation;
            else
                _worldRotationOffset = Quaternion.identity;

            _startSlerpPositionSpring = _joint.slerpDrive.positionSpring;
            _isSetupInEditor = true;
        }
    }

    public void UpdateJointFromAnimation()
    {
        if (!_syncAnimation || _joint == null) return;

        // 1. Where the animated arm wants to be
        Quaternion targetWorldRotation = _animatedRb.transform.rotation * _worldRotationOffset;

        // 2. Where the Chest (the anchor) is currently
        Transform physicalAnchor = _joint.connectedBody != null ? _joint.connectedBody.transform : transform.parent;

        // 3. THE FIX: Target rotation is simply the world target relative to the Chest's world rotation.
        // This removes the root entirely, which is likely causing your floating point errors.
        Quaternion targetLocalRotation = Quaternion.Inverse(physicalAnchor.rotation) * targetWorldRotation;

        // 4. Apply to the joint
        ConfigurableJointExtensions.SetTargetRotationLocal(_joint, targetLocalRotation, _startLocalRotation);
    }

    public void MakeRagdoll()
    {
        if (_joint == null) return;
        JointDrive jointDrive = _joint.slerpDrive;
        jointDrive.positionSpring = 1;
        _joint.slerpDrive = jointDrive;
    }

    public void MakeActiveRagdoll()
    {
        if (_joint == null) return;
        JointDrive jointDrive = _joint.slerpDrive;
        jointDrive.positionSpring = _startSlerpPositionSpring;
        _joint.slerpDrive = jointDrive;
    }

    /// <summary>
    /// called when swapping characters to ensure rotations are reset
    /// </summary>
    public void ResetBoneToTarget()
    {
        if (_animatedRb == null || _joint == null) return;

        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        Rigidbody cachedConnectedBody = _joint.connectedBody;
        _joint.connectedBody = null;

        transform.position = _animatedRb.transform.position;
        transform.rotation = _animatedRb.transform.rotation * _worldRotationOffset; // Snap rotation using the animated rig's rotation PLUS our backwards offset!

        UpdateJointFromAnimation();

        _joint.connectedBody = cachedConnectedBody;
    }

    public void SetHighDamping(bool high)
    {
        if (_joint == null) return;

        if (high)
        {
            if (!_hasCachedDrive)
            {
                _cachedSlerpDrive = _joint.slerpDrive;   // remember the gameplay drive
                _hasCachedDrive = true;
            }

            JointDrive drive = _joint.slerpDrive;
            // keep the spring so bones still follow the idle animation,
            // but add heavy damping so they don't overshoot / ring
            drive.positionDamper = Mathf.Max(drive.positionDamper, _cachedSlerpDrive.positionSpring * 0.3f);
            _joint.slerpDrive = drive;
        }
        else
        {
            if (_hasCachedDrive)
            {
                _joint.slerpDrive = _cachedSlerpDrive;   // restore exact gameplay drive
                _hasCachedDrive = false;
            }
        }
    }
}