using FishNet.Object;
using UnityEngine;

public class ActiveRagdollMember : NetworkBehaviour
{
    [SerializeField] private Transform _targetLimb;
    [SerializeField] private float _muscleSpring = 1500f;
    [SerializeField] private float _muscleDamper = 50f;
    [SerializeField] private Vector3 rotationOffset = new Vector3(0, 90f, 0);
    private ConfigurableJoint _cj;
    private Quaternion _initialRotation;

    public override void OnStartClient()
    {
        base.OnStartClient();

        _cj = GetComponent<ConfigurableJoint>();
        // setup joint settings automatically
        _cj.SetupAsCharacterJoint();

        var drive = _cj.slerpDrive;
        drive.positionSpring = _muscleSpring;
        drive.positionDamper = _muscleDamper;
        _cj.slerpDrive = drive;

        // cache the T-pose rotation
        _initialRotation = transform.localRotation;

        //if (!IsOwner)
        //{
        //    GetComponent<Rigidbody>().isKinematic = true;
        //}
    }

    private void FixedUpdate()
    {
        // only owner of this character can run the physics
        if (!IsOwner) return;

        if (_targetLimb == null || _cj == null) return;

        //_cj.SetTargetRotationLocal(_targetLimb.localRotation, _initialRotation);

        Quaternion correction = Quaternion.Euler(rotationOffset);
        Quaternion finalTarget = _targetLimb.localRotation * correction;

        _cj.SetTargetRotationLocal(finalTarget, _initialRotation);
    }

    private void OnDrawGizmos()
    {
        if (_targetLimb == null) return;

        Quaternion modelRotationOffset = Quaternion.Euler(rotationOffset);

        // Red line: Where the physical limb is currently pointing
        Gizmos.color = Color.red;
        Vector3 physicalFaceDirection = transform.rotation * modelRotationOffset * Vector3.forward;
        Gizmos.DrawRay(transform.position, physicalFaceDirection * 0.5f);

        // Green line: Where the code is telling the limb to go (the animation)
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, _targetLimb.forward * 0.5f);
    }
}