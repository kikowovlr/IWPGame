using FishNet.Object;
using UnityEngine;

public class ActiveRagdollMember : MonoBehaviour
{
    [SerializeField] Rigidbody _animatedRb;// to copy rotation from animated
    [SerializeField] bool _syncAnimation = false;
    Rigidbody _rb;
    ConfigurableJoint _joint;
    Quaternion _startLocalRotation; // keep track of starting rotation

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _joint = GetComponent<ConfigurableJoint>();


        _startLocalRotation = transform.localRotation;
    }

    public void UpdateJointFromAnimation()
    {
        if (!_syncAnimation)
            return;

        ConfigurableJointExtensions.SetTargetRotationLocal(_joint, _animatedRb.transform.localRotation, _startLocalRotation);
    }
}