using FishNet.Object;
using UnityEngine;

public class CopyMotion : MonoBehaviour
{
    public Transform _targetLimb;
    private ConfigurableJoint _cj;
    private Quaternion _startingRotation;
    public NetworkObject _ownerNetworkObj;

    private void Start()
    {
        _cj = GetComponent<ConfigurableJoint>();
        _startingRotation = _targetLimb.localRotation;
    }

    private void FixedUpdate()
    {
        // only owner of this character can run the physics
        if (_ownerNetworkObj != null && !_ownerNetworkObj.IsOwner) return;

        _cj.targetRotation = CopyRotation();
    }

    private Quaternion CopyRotation()
    {
        return Quaternion.Inverse(_targetLimb.localRotation) * _startingRotation;
    }
}
