using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

/// <summary>
/// put on diff body parts to check for collision when attack is active
/// </summary>
public class DamageDealer : NetworkBehaviour
{
    [Header("Attack Settings")]
    [SerializeField] private Transform _hitCheckPoint;
    [SerializeField] private float _hitCheckRadius = 0.3f;
    private float _currentDamage;
    private float _currentKnockbackForce;
    private LayerMask _hitLayer;

    private NetworkPlayerController _ownerController;
    private bool _isAttackActive = false;

    private void Awake()
    {
        // find player who owns this limb
        PlayerComponentRegistry registry = transform.root.GetComponent<PlayerComponentRegistry>();
        if (registry != null)
            _ownerController = registry.Controller;
    }

    // use this to set up attack in action scripts
    public void SetUpAttack(float damage, float force, LayerMask hitLayer)
    {
        _currentDamage = damage;
        _currentKnockbackForce = force;
        _hitLayer = hitLayer;
    }

    // action scripts to call this fn to turn damage window on and off
    public void SetAttackActive(bool active)
    {
        _isAttackActive = active;
    }

    public override void FixedUpdateNetwork()
    {
        if (!_isAttackActive) return;
        Collider[] hitColliders = new Collider[10];
        int numHits = Runner.GetPhysicsScene().OverlapSphere(_hitCheckPoint.position, _hitCheckRadius, hitColliders, _hitLayer, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < numHits; i++)
        {
            Collider other = hitColliders[i];

            // Filter out hitting yourself
            if (other.transform.root == transform.root) continue;
            if (((1 << other.gameObject.layer) & _hitLayer) == 0) return;

            // find victim's health pool
            PlayerComponentRegistry victimRegistry = other.transform.root.GetComponent<PlayerComponentRegistry>();
            if (victimRegistry != null)
            {
                Utils.DebugLog("Dealing damage");

                // get exact rigidbody we struck 
                NetworkRigidbody3D victimLimb = other.gameObject.GetComponent<NetworkRigidbody3D>();

                // calculate intended force
                Vector3 forceDirection = (_ownerController.transform.forward + Vector3.up * 0.2f).normalized;
                Vector3 intendedForce = forceDirection * _currentKnockbackForce;

                // send data to health script 
                Vector3 contactPoint = other.ClosestPoint(_hitCheckPoint.position);
                victimRegistry.Health.Rpc_TakeDamage(_currentDamage, intendedForce, contactPoint, victimLimb);

                // TODO - idk if shld js deactivate it here, maybe use coroutine instead to deactivate
                _isAttackActive = false;
                break;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        //if (_punchCheckPoint != null)
        //{
        //    Gizmos.color = Color.red;
        //    Gizmos.DrawWireSphere(_punchCheckPoint.position, _hitCheckRadius);
        //}
    }
}
