using UnityEngine;
using Fusion;

public class HeadbuttHandler : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private DamageDealer _headDamageDealer;
    [SerializeField] private LayerMask _hitLayer;
    [SerializeField] private Rigidbody _headRb;
    private Animator _animator;
    private NetworkPlayerController _playerController;
    private Collider _headCollider;
    private Rigidbody _mainRootRb;

    [Header("Headbutt Settings")]
    [SerializeField] private float _damage = 10f;
    [SerializeField] private float _bodyForwardImpulse = 1.5f;
    [SerializeField] private float _headThrustImpulse = 6f;
    [SerializeField] private float _knockbackForce = 2f;
    [SerializeField] private float _attackWindowDuration = 0.2f;
    [SerializeField] private float _totalCycleTime = 0.45f; // attack + recovery
    [SerializeField] private float _cooldown = 1f;

    [Networked] private TickTimer _cooldownTimer { get; set; }

    private bool _isHeadbutting = false;
    private float _headbuttTimer = 0f;

    public bool IsHeadbutting => _isHeadbutting;
    public bool IsHeadbuttReady => _cooldownTimer.ExpiredOrNotRunning(Runner);

    private void Awake()
    {
        PlayerComponentRegistry registry = transform.root.GetComponent<PlayerComponentRegistry>();
        if (registry != null)
        {
            _playerController = registry.Controller;
            _animator = _playerController.Animator;
        }

        if (_headDamageDealer != null)
        {
            _headCollider = _headDamageDealer.GetComponent<Collider>();
            _headCollider.enabled = false;
            _headDamageDealer.SetAttackActive(false);
        }
    }

    public void TriggerHeadbutt()
    {
        if (_isHeadbutting || !IsHeadbuttReady || (_playerController != null && _playerController.IsKnockedOut)) return;

        _isHeadbutting = true;
        _headbuttTimer = 0f;

        if (_headDamageDealer != null)
        {
            _headDamageDealer.SetUpAttack(_damage, _knockbackForce, _hitLayer);
            SetHeadbuttAttack(active: true);
        }

        _animator.SetTrigger("HeadbuttTrigger");
        _cooldownTimer = TickTimer.CreateFromSeconds(Runner, _cooldown);
        ApplyHeadbuttForces();
    }

    private void ApplyHeadbuttForces()
    {
        Vector3 forwardDir = transform.forward;
        forwardDir.y = 0f;
        forwardDir.Normalize();

        if (_mainRootRb != null)
        {
            _mainRootRb.AddForce(forwardDir * _bodyForwardImpulse, ForceMode.VelocityChange);
        }

        if (_headRb != null)
        {
            Vector3 headForceDir = (forwardDir * 0.7f) + (Vector3.down * 0.3f); // downward and forward
            _headRb.AddForce(headForceDir.normalized * _headThrustImpulse, ForceMode.VelocityChange);
        }
    }

    public void UpdateHeadbuttState()
    {
        if (!_isHeadbutting) return;

        _headbuttTimer += Runner.DeltaTime;

        // active damage window
        if (_headbuttTimer >= _attackWindowDuration && _headDamageDealer != null)
        {
            SetHeadbuttAttack(false);
        }

        // total recovery time finishes
        if (_headbuttTimer >= _totalCycleTime)
        {
            _isHeadbutting = false;
        }
    }

    public override void FixedUpdateNetwork()
    {
        // in case getting knocked out mid attack
        if (_playerController != null && _playerController.IsKnockedOut && _isHeadbutting)
        {
            if (_headDamageDealer != null)
                SetHeadbuttAttack(false);

            _isHeadbutting = false;
        }
    }

private void SetHeadbuttAttack(bool active)
    {
        if (_headDamageDealer != null)
        {
            _headDamageDealer.SetAttackActive(active);
            _headCollider.enabled = active;
        }
    }
}
