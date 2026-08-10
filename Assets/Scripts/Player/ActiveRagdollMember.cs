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
    [Header("Rig Alignment")]
    [Tooltip("If this rig's physical bones weren't authored aligned to the animated bones, " +
         "enable this to snap the physical bone to the animated bone at setup so the offset is identity.")]
    [SerializeField] private bool _forceAlignToAnimated = false;

    [ContextMenu("Capture Pristine Bind-Pose (CLICK ME)")]
    public void SetupBaselineInEditor()
    {
        _joint = GetComponent<ConfigurableJoint>();
        if (_joint == null) return;

        CaptureBaseline();
        _isSetupInEditor = true;

        Debug.Log($"[Ragdoll] Captured bind-pose for {gameObject.name}: " +
                  $"offset={_worldRotationOffset.eulerAngles}, aligned={_forceAlignToAnimated}");
    }

    private void CaptureBaseline()
    {
        if (_joint == null) return;

        // if the physical bone wasn't authored aligned to the animated bone,
        // snap it into alignment once so the offset comes out identity —
        // same starting condition as a correctly-authored rig.
        if (_forceAlignToAnimated && _animatedRb != null)
        {
            transform.rotation = _animatedRb.transform.rotation;
        }

        Transform physicalAnchor = _joint.connectedBody != null ? _joint.connectedBody.transform : transform.parent;
        _startLocalRotation = Quaternion.Inverse(physicalAnchor.rotation) * transform.rotation;

        if (_animatedRb != null)
            _worldRotationOffset = Quaternion.Inverse(_animatedRb.transform.rotation) * transform.rotation;
        else
            _worldRotationOffset = Quaternion.identity;

        _startSlerpPositionSpring = _joint.slerpDrive.positionSpring;
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

        if (!_isSetupInEditor && _joint != null)
        {
            CaptureBaseline();
            _isSetupInEditor = true;
        }
    }

    public void UpdateJointFromAnimation()
    {
        if (!_syncAnimation || _joint == null)
            return;

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

    public void LogWorldRotationOffset()
    {
        Vector3 euler = _worldRotationOffset.eulerAngles;
        // normalize to -180..180 so a "backwards" bone reads as ~±180 instead of ~180/360 noise
        euler.x = Mathf.DeltaAngle(0f, euler.x);
        euler.y = Mathf.DeltaAngle(0f, euler.y);
        euler.z = Mathf.DeltaAngle(0f, euler.z);
        Debug.Log($"[RagdollOffset] {gameObject.name}: worldRotationOffset={euler} (setupInEditor={_isSetupInEditor})");
    }
}