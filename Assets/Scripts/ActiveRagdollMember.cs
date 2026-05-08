using FishNet.Object;
using UnityEngine;

public class ActiveRagdollMember : NetworkBehaviour
{
    [SerializeField] private Transform _targetLimb;
    [SerializeField] private float _muscleSpring = 1500f;
    [SerializeField] private float _muscleDamper = 50f;

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

        if (!IsOwner)
        {
            GetComponent<Rigidbody>().isKinematic = true;
        }
    }

    private void FixedUpdate()
    {
        // only owner of this character can run the physics
        if (!IsOwner) return;

        if (_targetLimb == null || _cj == null) return;

        _cj.SetTargetRotationLocal(_targetLimb.localRotation, _initialRotation);
    }
}
