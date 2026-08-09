using Fusion;
using UnityEngine;
using System.Collections;

public class DummyHealthHandler : NetworkBehaviour, IDamageable
{
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private float _recoverDelay = 3f;
    [SerializeField] private float _knockedDownDamageMultiplier = 0.3f;
    [SerializeField] private float _knockOutForceMultiplier = 1.8f;
    [SerializeField] private DummyController _dummy;
    [SerializeField] private PlayerCombatAudio _combatAudio;

    [Networked, OnChangedRender(nameof(OnHealthChanged))] public float CurrentHealth { get; private set; }
    [HideInInspector] [Networked] public float MaxHealth { get; private set; }

    [Networked, OnChangedRender(nameof(OnHitSoundChanged))] private byte _hitSoundTick { get; set; }
    [Networked] private SoundID _lastHitSound { get; set; }

    private Transform _characterRoot;
    public System.Action<float, float> OnHealthChangedEvent;

    private void Awake()
    {
        _characterRoot = transform.root;
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            MaxHealth = _maxHealth;
            CurrentHealth = _maxHealth;
        }
        OnHealthChanged();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void Rpc_TakeDamage(float damageAmount, Vector3 impactForce, Vector3 impactPoint, string hitBoneName, SoundID hitSound)
    {
        if (_dummy.IsKnockedOut)
        {
            CurrentHealth -= damageAmount * _knockedDownDamageMultiplier;
            ApplyForceToBone(impactForce, impactPoint, hitBoneName);
            return;
        }

        CurrentHealth -= damageAmount;

        if (CurrentHealth <= 0)
        {
            _dummy.Knockout(); // knockout SOUND handled by DummyController's IsKnockedOut OnChangedRender
            ApplyForceToBone(impactForce * _knockOutForceMultiplier, impactPoint, hitBoneName);
            StartCoroutine(RecoverRoutine());
        }
        else
        {
            ApplyForceToBone(impactForce, impactPoint, hitBoneName);
            // normal hit -> tell ALL clients to play the hit sound
            _lastHitSound = hitSound;
            _hitSoundTick++;  // change fires OnHitSoundChanged on every client
        }
    }

    // fires on ALL clients when a hit lands
    private void OnHitSoundChanged()
    {
        if (_combatAudio != null)
            _combatAudio.PlaySound(_lastHitSound);
    }

    private IEnumerator RecoverRoutine()
    {
        yield return new WaitForSeconds(_recoverDelay);
        _dummy.Recover();
        CurrentHealth = MaxHealth;   // reset — infinite punching bag, never eliminated
    }

    private void ApplyForceToBone(Vector3 force, Vector3 point, string boneName)
    {
        Rigidbody targetRb = null;
        if (!string.IsNullOrEmpty(boneName))
            targetRb = FindBoneInHierarchy(_characterRoot, boneName);

        if (targetRb != null)
            targetRb.AddForceAtPosition(force, point, ForceMode.Impulse);
        else if (_dummy.NetworkedRb != null && _dummy.NetworkedRb.Rigidbody != null)
            _dummy.NetworkedRb.Rigidbody.AddForceAtPosition(force, point, ForceMode.Impulse);
    }

    private Rigidbody FindBoneInHierarchy(Transform root, string targetName)
    {
        if (root.name == targetName) return root.GetComponent<Rigidbody>();
        foreach (Transform child in root)
        {
            Rigidbody found = FindBoneInHierarchy(child, targetName);
            if (found != null) return found;
        }
        return null;
    }

    private void OnHealthChanged() => OnHealthChangedEvent?.Invoke(CurrentHealth, MaxHealth);
}
