using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

public class RespawnHandler : NetworkBehaviour
{
    private PlayerComponentRegistry _registry;

    private void Awake()
    {
        _registry = transform.root.GetComponentInParent<PlayerComponentRegistry>();
    }


    public void TeleportToSpawnPoint(Vector3 position, Quaternion rotation)
    {
        // if the registry exposes the impact audio, suppress right after teleporting
        if (_registry != null && _registry.ImpactAudio != null)
            _registry.ImpactAudio.SuppressImpacts(0.3f);

        if (_registry != null)
            _registry.VisualsOverrider.EnableVisuals();

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
