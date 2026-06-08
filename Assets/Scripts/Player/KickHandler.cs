using Fusion;
using UnityEngine;

public class KickHandler : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private DamageDealer _rightFootDamageDealer;
    [SerializeField] private LayerMask _hitLayer;
    private Animator _animator;
    private NetworkPlayerController _playerController;
    private Rigidbody _playerRb;
    private ConfigurableJoint _mainJoint;
    private Collider _damageCollider;

    [Header("Kick Settings")]
    [SerializeField] private float _kickDamage = 50f;
    [SerializeField] private float _kickKnockbackForce = 7f;
    [SerializeField] private float _baseForwardKickForce = 10f;
    [SerializeField] private float _sprintKickForceMultiplier = 1.5f;
    [SerializeField] private float _upwardKickForce = 3f;
    [SerializeField] private float _ragdollDelayDuration = 0.4f; // time before turning limp
    [SerializeField] private float _ragdollDuration = 1.5f; // ragdoll duration b4 standing back up

    [Networked] private float _kickStartTime { get; set; }
    [Networked] public bool IsKicking { get; private set; }

    private Quaternion _initialJointRotation;
    private const float InputThreshold = 0.01f;

    private void Awake()
    {
        PlayerComponentRegistry registry = transform.root.GetComponent<PlayerComponentRegistry>();
        if (registry != null)
        {
            _playerController = registry.Controller;
            _animator = _playerController.Animator;
            _playerRb = _playerController.NetworkedRb.Rigidbody;
        }

        if (_rightFootDamageDealer != null)
        {
            _damageCollider = _rightFootDamageDealer.GetComponent<Collider>();
            _damageCollider.enabled = false;
            _rightFootDamageDealer.SetAttackActive(false);
        }
    }

    private void Start()
    {
        if (_playerController != null)
        {
            _initialJointRotation = _playerController.InitialJointRotation;
        }
    }

    public void TriggerAirKick(Vector3 inputDirection, bool isSprinting)
    {
        Utils.DebugLog("Kick Triggered");

        // only obj w state authority can trigger
        if (!Object.HasStateAuthority || IsKicking) return;

        IsKicking = true;
        _kickStartTime = Runner.SimulationTime;

        // travel direction based on where player is travelling
        Vector3 kickDir = inputDirection;
        kickDir.y = 0f;

        // if player not moving, kick forward
        if (kickDir.magnitude < InputThreshold)
        {
            kickDir = _playerRb.transform.forward;
        }
        else
        {
            kickDir.Normalize();
        }

        //// 2. Instantly snap player's physical rotation to face the kick direction
        //Quaternion targetRot = Quaternion.LookRotation(kickDirection, Vector3.up);
        //_playerRb.MoveRotation(targetRot);
        //_mainJoint.targetRotation = Quaternion.Inverse(targetRot) * _initialJointRotation;

        // scale velocity dynamically based on sprint status
        float finalForwardForce = _baseForwardKickForce;
        if (isSprinting)
        {
            finalForwardForce = _baseForwardKickForce * _sprintKickForceMultiplier;
        }

        // reset vertical drops
        Vector3 currentVel = _playerRb.linearVelocity;
        _playerRb.linearVelocity = new Vector3(currentVel.x, Mathf.Max(0, currentVel.y), currentVel.z);

        // apply force
        Vector3 totalKickImpulse = (kickDir * finalForwardForce) + (Vector3.up * _upwardKickForce);
        _playerRb.AddForce(totalKickImpulse, ForceMode.Impulse);

        if (_rightFootDamageDealer != null)
        {
            _rightFootDamageDealer.SetUpAttack(_kickDamage, _kickKnockbackForce, _hitLayer);
            SetKickDamage(active: true);
        }

        // fire visual trigger
        _animator.SetTrigger("KickTrigger");
    }

    public override void FixedUpdateNetwork()
    {
        float timeSinceKick = Runner.SimulationTime - _kickStartTime;

        // if player gets knocked out mid kick, shut off foot damage
        if (_playerController != null && _playerController.IsKnockedOut && IsKicking)
        {
            if (timeSinceKick < _ragdollDelayDuration)
            {
                // deactivate kick
                SetKickDamage(active: false);
                IsKicking = false;
                return;
            }
        }

        // if not kicking, dont run the state timers
        if (!IsKicking) return;

        // attack window finished -> turn off damage and go ragdoll
        if (timeSinceKick >= _ragdollDelayDuration && !_playerController.IsKnockedOut)
        {
            SetKickDamage(false);
            _playerController.Knockout();
        }
        
        // total ragdoll has ended -> recover
        if (timeSinceKick >= (_ragdollDelayDuration + _ragdollDuration))
        {
            IsKicking = false;
            _playerController.Recover();
        }
    }

    private void SetKickDamage(bool active)
    {
        if (_rightFootDamageDealer != null)
        {
            _rightFootDamageDealer.SetAttackActive(active);
            _damageCollider.enabled = active;
        }
    }
}
