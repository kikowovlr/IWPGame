using UnityEngine;

public class ActiveRagdollMember : MonoBehaviour
{
    public Rigidbody _animatedRb;// to copy rotation from animated
    public Transform _animatedRoot;
    public Transform _physicalRoot;
    [SerializeField] bool _syncAnimation = false;
    Rigidbody _rb;
    ConfigurableJoint _joint;

    [SerializeField] Quaternion _startLocalRotation; // keep track of starting rotation
    [SerializeField] float _startSlerpPositionSpring = 0.0f;
    [SerializeField] bool _isSetupInEditor = false;

    [SerializeField, HideInInspector] Quaternion _worldRotationOffset; // saves the exact difference between the animated bone and physical bone

    [ContextMenu("Capture Pristine Bind-Pose (CLICK ME)")]
    //public void SetupBaselineInEditor()
    //{
    //    _joint = GetComponent<ConfigurableJoint>();
    //    if (_joint != null)
    //    {
    //        _startLocalRotation = transform.localRotation;
    //        _startSlerpPositionSpring = _joint.slerpDrive.positionSpring;
    //        _isSetupInEditor = true;
    //        Debug.Log($"[Ragdoll] Manually captured pristine bind-pose for {gameObject.name}: {_startLocalRotation.eulerAngles}");
    //    }
    //}
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
        // Don't initialize instantly! 
        // Wait until the first frame where the animation has actually played.
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
        //if (!_isSetupInEditor && _joint != null)
        //{
        //    _startLocalRotation = transform.localRotation;
        //    _startSlerpPositionSpring = _joint.slerpDrive.positionSpring;
        //    _isSetupInEditor = true;
        //    Debug.LogWarning($"[Ragdoll] Warning: {gameObject.name} captured baseline dynamically at runtime! Current rotation captured: {_startLocalRotation.eulerAngles}");
        //}
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

        //// 1. Calculate how the animated bone is rotated relative to its main character root
        //Quaternion animatedRootRelative = Quaternion.Inverse(_animatedRoot.rotation) * _animatedRb.transform.rotation;

        //// 2. CRITICAL FIX: Find the actual physical anchor. 
        //// If the joint has a connected Rigidbody (like the Chest), use THAT rotation. 
        //// Only fall back to transform.parent if there is no connected body.
        //Transform physicalAnchor = _joint.connectedBody != null ? _joint.connectedBody.transform : transform.parent;

        //// 3. Calculate target rotation relative to the true physics anchor, bypassing the empty Clavicle!
        //Quaternion targetLocalRotation = Quaternion.Inverse(physicalAnchor.rotation) * (_physicalRoot.rotation * animatedRootRelative);

        ////// 2. Convert that root-relative target back into the local space of this specific joint's parent
        ////// This completely bypasses intermediate missing spine bones!
        ////Quaternion targetLocalRotation = Quaternion.Inverse(transform.parent.rotation) * (_physicalRoot.rotation * animatedRootRelative);

        //// 3. Set the joint's target rotation using your extension method
        //ConfigurableJointExtensions.SetTargetRotationLocal(_joint, targetLocalRotation, _startLocalRotation);



        //// CRITICAL FIX: We must apply the backwards offset to the continuous math!
        //// Instead of targeting the animated bone directly, target the animated bone PLUS the structural offset.
        //Quaternion targetWorldRotation = _animatedRb.transform.rotation * _worldRotationOffset;

        //// 1. Calculate how that offset target is rotated relative to its main character root
        //Quaternion animatedRootRelative = Quaternion.Inverse(_animatedRoot.rotation) * targetWorldRotation;

        //// 2. Find the actual physical anchor (Chest)
        //Transform physicalAnchor = _joint.connectedBody != null ? _joint.connectedBody.transform : transform.parent;

        //// 3. Calculate target rotation relative to the true physics anchor
        //Quaternion targetLocalRotation = Quaternion.Inverse(physicalAnchor.rotation) * (_physicalRoot.rotation * animatedRootRelative);

        //// 4. Set the joint's target rotation using your extension method
        //ConfigurableJointExtensions.SetTargetRotationLocal(_joint, targetLocalRotation, _startLocalRotation);



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
}