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
    [SerializeField] private float _knockOutForceMultiplier = 1.8f; // boost physical punch force on the KO blow
    [Networked, OnChangedRender(nameof(OnHealthChanged))] public float CurrentHealth {  get; private set; }
    [Networked] public float MaxHealth { get; private set; }

    // keep track of accumulated dmg for goat skill
    [Networked] private float AccumulatedDamageTaken { get; set; }

    private NetworkPlayerController _playerController;
    private Transform _characterRoot; // used to scan the rig hierarchy

    // events
    public System.Action<float, float> OnHealthChangedEvent;

    private void Awake()
    {
        PlayerComponentRegistry registry = transform.root.GetComponent<PlayerComponentRegistry>();
        if (registry != null)
        {
            _playerController = registry.Controller;
        }
        _characterRoot = transform.root;
    }

    public override void Spawned()
    {
        // init health on network when player spawns
        if (Object.HasStateAuthority)
        {
            MaxHealth = _maxHealth;
            CurrentHealth = _maxHealth;
        }

        OnHealthChanged(); // ensure Ui syncs for late joiners
    }

    /// <summary>
    /// use this fn for all damage taken
    /// - can be sent by all 
    /// - only sent to state authority over this obj
    /// - uses string of hitbonename rather than networkrigibody3d 
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void Rpc_TakeDamage(float damageAmount, Vector3 impactForce, Vector3 impactPoint, string hitBoneName)
    {
        float finalDamage = damageAmount;

        if (_playerController.IsKnockedOut)
        {
            // getting hit while knocked down deals damage (health goes into -ve) -> stay knocked out longer
            finalDamage = damageAmount * _knockedDownDamageMultiplier;
            CurrentHealth -= finalDamage;

            // apply force to hit point
            ApplyForceToBone(impactForce, impactForce, hitBoneName);
            return;
        }

        // get accumulated dmg if skill is charging rn
        ref AbilityState abilityState = ref _playerController.AbilityStateRef;
        if (abilityState._isCharging && _playerController.EquippedAbility != null)
        {
            finalDamage = _playerController.EquippedAbility.HandleIncomingDamageCheck(_playerController, ref abilityState, damageAmount);
            AccumulatedDamageTaken += finalDamage;
        }

        // reduce health
        CurrentHealth -= damageAmount;

        // check for knockout
        if (CurrentHealth <= 0)
        {
            Knockout();
            // amplified force to show knockout blow
            ApplyForceToBone(impactForce * _knockOutForceMultiplier, impactPoint, hitBoneName);
        }
        else
        {
            // standard hit reaction
            ApplyForceToBone(impactForce, impactPoint, hitBoneName);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void Rpc_TakeDamage(float damageAmount, Vector3 impactForce)
    {
        float finalDamage = damageAmount;

        if (_playerController.IsKnockedOut)
        {
            // getting hit while knocked down deals damage (health goes into -ve) -> stay knocked out longer
            finalDamage = damageAmount * _knockedDownDamageMultiplier;
            CurrentHealth -= finalDamage;

            // apply force to hit point
            _playerController.ApplyKnockback(impactForce, ForceMode.Impulse);
            return;
        }

        // get accumulated dmg if skill is charging rn
        ref AbilityState abilityState = ref _playerController.AbilityStateRef;
        if (abilityState._isCharging && _playerController.EquippedAbility != null)
        {
            finalDamage = _playerController.EquippedAbility.HandleIncomingDamageCheck(_playerController, ref abilityState, damageAmount);

            AccumulatedDamageTaken += finalDamage;
        }

        // reduce health
        CurrentHealth -= damageAmount;

        // check for knockout
        if (CurrentHealth <= 0)
        {
            Knockout();
            // amplified force to show knockout blow
            _playerController.ApplyKnockback(impactForce * _knockOutForceMultiplier, ForceMode.Impulse);
        }
        else
        {
            // standard hit reaction
            _playerController.ApplyKnockback(impactForce, ForceMode.Impulse);
        }
    }

    /// <summary>
    /// use string to tell client what bone to add force to - not desirable as hackers get hack into this but its an ok fix for now as a casual ragdoll game
    /// </summary>
    private void ApplyForceToBone(Vector3 force, Vector3 point, string boneName)
    {
        Rigidbody targetRb = null;

        // find matching local rb in player's hierarchy by name
        if (!string.IsNullOrEmpty(boneName))
        {
            targetRb = FindBoneInHierarchy(_characterRoot, boneName);
        }

        // if found, apply force to limb
        if (targetRb != null)
        {
            targetRb.AddForceAtPosition(force, point, ForceMode.Impulse);
            //Utils.DebugLog($"[PHYSICS NETWORK] Successfully synchronized blast to local bone: {boneName}");
        }
        else
        {
            // fallback to root networkrb if name lookup fails
            NetworkRigidbody3D rootNetworkRb = _playerController.NetworkedRb;
            if (rootNetworkRb != null && rootNetworkRb.Rigidbody != null)
            {
                rootNetworkRb.Rigidbody.AddForceAtPosition(force, point, ForceMode.Impulse);
                //Utils.DebugLog($"[PHYSICS NETWORK] Bone not found. Safely fell back to Root NetworkRigidbody3D.");
            }
        }
    }

    private Rigidbody FindBoneInHierarchy(Transform root, string targetName)
    {
        // curr transform matches name we are looking for
        if (root.name == targetName)
        {
            return root.GetComponent<Rigidbody>();  
        }

        // recursively find target
        foreach (Transform child in root)
        {
            // Pass the child down into the same function to search the next layer deeper
            Rigidbody foundRigidbody = FindBoneInHierarchy(child, targetName);

            if (foundRigidbody != null)
            {
                return foundRigidbody;
            }
        }

        return null;
    }

    private void Knockout()
    {
        // calculate dynamic duration -> negative health, longer knockedout time
        float overkill = Mathf.Abs(CurrentHealth);
        float extraTime = overkill * 0.05f; // adds 1 sec per 20 points of overkill
        float totalKnockoutTime = Mathf.Clamp(_baseKnockoutTime + extraTime, _baseKnockoutTime, _maxKnockoutTime);

        StartCoroutine(KnockoutRoutine(totalKnockoutTime));
    }

    private IEnumerator KnockoutRoutine(float duration)
    {
        _playerController.Knockout();

        yield return new WaitForSeconds(duration);

        _playerController.Recover();
        CurrentHealth = _maxHealth;
    }

    private void OnHealthChanged()
    {
        // runs on all clients when value updates
        OnHealthChangedEvent?.Invoke(CurrentHealth, MaxHealth);
    }

    public void ResetAccumulatedDamageCounter() => AccumulatedDamageTaken = 0f;
    public float GetAccumulatedDamage() => AccumulatedDamageTaken;
}
