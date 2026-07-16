using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

public class RespawnHandler : NetworkBehaviour
{
    private PlayerComponentRegistry _registry;

    private void Awake()
    {
        _registry = GetComponentInParent<PlayerComponentRegistry>();
    }


    public void TeleportToSpawnPoint(Vector3 position, Quaternion rotation)
    {
        if (_registry != null)
            _registry.VisualsOverrider.EnableVisuals();

        transform.position = position;
        transform.rotation = rotation;

        if (Object.HasStateAuthority)
        {
            NetworkRigidbody3D rb = _registry.Controller.NetworkedRb;
            if (rb != null)
            {
                rb.Teleport(position, rotation);
            }
        }
    }
}
