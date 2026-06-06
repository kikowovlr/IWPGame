using Fusion;
using Fusion.Addons.Physics;
using System.Collections;
using UnityEngine;

public class PlayerHealthHandler : NetworkBehaviour
{
    [SerializeField] private float _maxHealth = 100f;

    [Header("Knockout Settings")]
    [SerializeField] private float _knockedDownDamageMultiplier = 0.3f;
    [SerializeField] private float _baseKnockoutTime = 3f;
    [SerializeField] private float _maxKnockoutTime = 8f;
    [Networked] public float CurrentHealth {  get; private set; }

    private NetworkPlayerController _playerController;

    private void Awake()
    {
        PlayerComponentRegistry registry = transform.root.GetComponent<PlayerComponentRegistry>();
        if (registry != null)
        {
            _playerController = registry.Controller;
        }    
    }

    public override void Spawned()
    {
        // init health on network when player spawns
        if (Object.HasStateAuthority)
        {
            CurrentHealth = _maxHealth;
        }
    }

    /// <summary>
    /// use this fn for all damage taken
    /// - can be sent by all 
    /// - only sent to state authority over this obj
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void Rpc_TakeDamage(float damageAmount, Vector3 impactForce, Vector3 impactPoint, NetworkRigidbody3D hitLimb)
    {
        Utils.DebugLog("Damage taken: " + damageAmount);

        if (_playerController.IsKnockedOut)
        {
            // getting hit while knocked down deals damage (health goes into -ve) -> stay knocked out longer
            CurrentHealth -= damageAmount * _knockedDownDamageMultiplier;

            // apply force to hit point
            if (hitLimb != null)
                hitLimb.Rigidbody.AddForceAtPosition(impactForce, impactPoint, ForceMode.Impulse);

            return;
        }

        // reduce health
        CurrentHealth -= damageAmount;

        // apply force to hit point
        if (hitLimb != null)
            hitLimb.Rigidbody.AddForceAtPosition(impactForce, impactPoint, ForceMode.Impulse);

        // check for knockout
        if (CurrentHealth <= 0)
        {
            Knockout();
        }
    }

    private void Knockout()
    {
        // calculate dynamic duration -> negative health, longer knockedout time
        float overkill = Mathf.Abs(CurrentHealth);
        float extraTime = overkill * 0.05f; // adds 1 sec per 20 points of overkill
        float totalKnockoutTime = Mathf.Clamp(_baseKnockoutTime + extraTime, _baseKnockoutTime, _maxKnockoutTime);

        Utils.DebugLog("knocked out!");
        StartCoroutine(KnockoutRoutine(totalKnockoutTime));
    }

    private IEnumerator KnockoutRoutine(float duration)
    {
        _playerController.Knockout();

        yield return new WaitForSeconds(duration);

        _playerController.Recover();
        CurrentHealth = _maxHealth;
    }
}
