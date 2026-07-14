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

        if (!Object.HasStateAuthority) return;

        // force photon fusion network layers to snap immediately
        NetworkRigidbody3D rb = _registry.Controller.NetworkedRb;

        if (rb != null)
        {
            rb.Teleport(position, rotation);
        }
        else
        {
            transform.position = position;
            transform.rotation = rotation;
        }
    }
}
