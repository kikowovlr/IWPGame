using Fusion.Addons.Physics;
using UnityEngine;

/// <summary>
/// put this on hand, head, etc, colliders for diff attacks
/// </summary>
public class DamageDealer : MonoBehaviour
{
    [Header("Attack Settings")]
    [SerializeField] private float _damage = 25f;
    [SerializeField] private float _knockbackForce = 20f;
    [SerializeField] private LayerMask _hitLayer;

    private NetworkPlayerController _ownerController;
    private bool _isAttackActive = false;

    private void Awake()
    {
        // find player who owns this limb
        PlayerComponentRegistry registry = transform.root.GetComponent<PlayerComponentRegistry>();
        if (registry != null)
            _ownerController = registry.Controller;
    }

    // action scripts to call this fn to turn damage window on and off
    public void SetAttackActive(bool active)
    {
        _isAttackActive = active;
    }

    private void OnCollisionEnter(Collision other)
    {
        if (!_isAttackActive) return;
        if (((1 << other.gameObject.layer) & _hitLayer) == 0) return;
        if (other.transform.root == transform.root) return;

        // find victim's health pool
        PlayerComponentRegistry victimRegistry = other.transform.root.GetComponent<PlayerComponentRegistry>();
        if (victimRegistry != null)
        {
            // get exact rigidbody we struck 
            NetworkRigidbody3D victimLimb = other.gameObject.GetComponent<NetworkRigidbody3D>();

            // calculate intended force
            Vector3 forceDirection = (_ownerController.transform.forward + Vector3.up * 0.2f).normalized;
            Vector3 intendedForce = forceDirection * _knockbackForce;

            // send data to health script 
            victimRegistry.Health.Rpc_TakeDamage(_damage, intendedForce, other.GetContact(0).point, victimLimb);

            // TODO - idk if shld js deactivate it here, maybe use coroutine instead to deactivate
            _isAttackActive = false;
        }
    }
}
