using Fusion;
using UnityEngine;

public class PunchHandler : NetworkBehaviour
{
    [Header("Punch Settings")]
    [SerializeField] private DamageDealer _leftHandDamageDealer;
    [SerializeField] private DamageDealer _rightHandDamageDealer;
    [SerializeField] private LayerMask _hitLayer;
    private Animator _animator;

    [Header("Weak Punch Settings")]
    [SerializeField] private float _weakDamage = 12f;
    [SerializeField] private float _weakForce = 10f;
    [SerializeField] private float _weakWindowDuration = 0.1f; // damage window
    [SerializeField] private float _weakTotalCycleTime = 0.2f; // damage is turned off but character is frozen in recovery stance (cannot run, walk..)

    [Header("Strong Punch Settings")]
    [SerializeField] private float _strongDamage = 45f;
    [SerializeField] private float _strongForce = 38f;
    [SerializeField] private float _strongWindowDuration = 0.2f;
    [SerializeField] private float _strongTotalCycleTime = 0.6f;
    [SerializeField] private float _strongPunchCooldown = 1.5f; // time until can strong punch again
    [SerializeField] private float _strongPunchDashImpulse = 6f;
    [SerializeField] private float _strongHandThrustImpulse = 4f;

    [Header("Ragdoll Physics Setup")]
    [SerializeField] private Rigidbody _leftHandRb;   
    [SerializeField] private Rigidbody _rightHandRb;
    private Rigidbody _masterRootRb;

    private NetworkPlayerController _playerController;
    private DamageDealer _activeDamageDealer;

    private bool _isPunching = false;
    private float _punchTimer = 0f;
    private float _activeWindowDuration = 0f;
    private float _activeTotalCycleTime = 0f;

    private int _nextHandIndex = 0; // decides which weak hand punch the animation uses, 0 = left, 1 = right

    [Networked] private TickTimer _strongPunchCooldownTimer { get; set; }

    public bool IsPunching => _isPunching;
    public bool IsStrongPunchReady => _strongPunchCooldownTimer.ExpiredOrNotRunning(Runner);

    private void Awake()
    {
        PlayerComponentRegistry registry = transform.root.GetComponent<PlayerComponentRegistry>();
        if (registry != null )
        {
            _playerController = registry.Controller;
            _animator = _playerController.Animator;
            _masterRootRb = _playerController.NetworkedRb.Rigidbody;
        }

        if (_leftHandDamageDealer != null)
        {
            _leftHandDamageDealer.GetComponent<Collider>().enabled = false;
            _leftHandDamageDealer.SetAttackActive(false);
        }

        if (_rightHandDamageDealer != null)
        {
            _rightHandDamageDealer.GetComponent<Collider>().enabled = false;
            _rightHandDamageDealer.SetAttackActive(false);
        }
    }

    /// <summary>
    /// decides what kind of punch and triggers it
    /// </summary>
    /// <param name="isSprinting"></param>
    public void TriggerPunch(bool isSprinting)
    {
        // ignore if player is unconscious or is alrdy punching
        if (_isPunching || (_playerController != null && _playerController.IsKnockedOut)) return;

        // check for strong punch
        if (isSprinting)
        {
            if (!IsStrongPunchReady)
                return;

            _isPunching = true;
            _punchTimer = 0f;

            _activeWindowDuration = _strongWindowDuration;
            _activeTotalCycleTime = _strongTotalCycleTime;
            _activeDamageDealer = _rightHandDamageDealer;

            _activeDamageDealer.SetUpAttack(_strongDamage, _strongForce, _hitLayer);
            _animator.SetTrigger("StrongPunchTrigger");
            _strongPunchCooldownTimer = TickTimer.CreateFromSeconds(Runner, _strongPunchCooldown);

            AddStrongPunchImpulse();
        }
        else
        {
            _isPunching = true;
            _punchTimer = 0f;

            // weak punch
            _activeWindowDuration = _weakWindowDuration;
            _activeTotalCycleTime = _weakTotalCycleTime;

            _activeDamageDealer = (_nextHandIndex == 0) ? _leftHandDamageDealer : _rightHandDamageDealer;
            _activeDamageDealer.SetUpAttack(_weakDamage, _weakForce, _hitLayer);

            _animator.SetInteger("PunchHand", _nextHandIndex);
            _animator.SetTrigger("WeakPunchTrigger");

            _nextHandIndex = (_nextHandIndex == 0) ? 1 : 0; // sets next hand as opposite of curr hand
        }

        // set the collider to be enabled
        if (_activeDamageDealer != null)
        {
            _activeDamageDealer.GetComponent<Collider>().enabled = true;
            _activeDamageDealer.SetAttackActive(true);
        }
    }

    private void AddStrongPunchImpulse()
    {
        Vector3 punchDir = transform.forward;
        punchDir.y = 0f;
        punchDir.Normalize();

        // add force to character and arm
        if (_masterRootRb != null)
        {
            _masterRootRb.AddForce(punchDir * _strongPunchDashImpulse, ForceMode.VelocityChange);
        }

        if (_rightHandRb != null)
        {
            _rightHandRb.AddForce(punchDir * _strongHandThrustImpulse, ForceMode.VelocityChange);
        }
    }

    /// <summary>
    /// tracks internal time clocks to manage active frames of punching
    /// </summary>
    public void UpdatePunchState()
    {
        if (!_isPunching) return;

        _punchTimer += Runner.DeltaTime;

        // punch active window expired
        if (_punchTimer >= _activeWindowDuration && _activeDamageDealer != null)
        {
            _activeDamageDealer.SetAttackActive(false);
            _activeDamageDealer.GetComponent<Collider>().enabled = false; // after active window set back to inactive collider
        }
        
        // punch is finished == active window + recovery window
        if (_punchTimer >= _activeTotalCycleTime)
        {
            _isPunching = false;
        }
    }

    /// <summary>
    /// safety fallback to check player's state to see if we need to deactivate punch early or not
    /// </summary>
    public override void FixedUpdateNetwork()
    {
        // if player gets knocked out mid punch
        if (_playerController != null && _playerController.IsKnockedOut && _isPunching)
        {
            // deactivate punch
            if (_activeDamageDealer != null) 
                _activeDamageDealer.SetAttackActive(false);

            _isPunching = false;
        }
    }
}
