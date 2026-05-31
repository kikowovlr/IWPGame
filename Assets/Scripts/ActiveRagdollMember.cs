using UnityEngine;

public class ActiveRagdollMember : MonoBehaviour
{
    [SerializeField] Rigidbody _animatedRb;// to copy rotation from animated
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

        ConfigurableJointExtensions.SetTargetRotationLocal(_joint, _animatedRb.transform.localRotation, _startLocalRotation);
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