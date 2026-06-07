using UnityEngine;

public class ActiveRagdollMember : MonoBehaviour
{
    [SerializeField] Rigidbody _animatedRb;// to copy rotation from animated
    [SerializeField] Transform _animatedRoot;
    [SerializeField] Transform _physicalRoot;
    [SerializeField] bool _syncAnimation = false;
    Rigidbody _rb;
    ConfigurableJoint _joint;
    Quaternion _startLocalRotation; // keep track of starting rotation
    float _startSlerpPositionSpring = 0.0f;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _joint = GetComponent<ConfigurableJoint>();

        _startLocalRotation = transform.localRotation;
        _startSlerpPositionSpring = _joint.slerpDrive.positionSpring;
    }

    public void UpdateJointFromAnimation()
    {
        if (!_syncAnimation)
            return;

        // 1. Calculate how the animated bone is rotated relative to its main character root
        Quaternion animatedRootRelative = Quaternion.Inverse(_animatedRoot.rotation) * _animatedRb.transform.rotation;

        // 2. Convert that root-relative target back into the local space of this specific joint's parent
        // This completely bypasses intermediate missing spine bones!
        Quaternion targetLocalRotation = Quaternion.Inverse(transform.parent.rotation) * (_physicalRoot.rotation * animatedRootRelative);

        // 3. Set the joint's target rotation using your extension method
        ConfigurableJointExtensions.SetTargetRotationLocal(_joint, targetLocalRotation, _startLocalRotation);

        //ConfigurableJointExtensions.SetTargetRotationLocal(_joint, _animatedRb.transform.localRotation, _startLocalRotation);
    }

    public void MakeRagdoll()
    {
        JointDrive jointDrive = _joint.slerpDrive;
        jointDrive.positionSpring = 1;
        _joint.slerpDrive = jointDrive;
    }

    public void MakeActiveRagdoll()
    {
        JointDrive jointDrive = _joint.slerpDrive;
        jointDrive.positionSpring = _startSlerpPositionSpring;
        _joint.slerpDrive = jointDrive;
    }
}