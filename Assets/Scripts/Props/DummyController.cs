using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

public class DummyController : NetworkBehaviour, IAffectedByStatusEffects
{
    [Header("References")]
    [SerializeField] private ConfigurableJoint _mainJoint;
    [SerializeField] private NetworkRigidbody3D _networkRb3D;
    [SerializeField] private float _unconsciousMass = 0.15f;

    [Header("Character Package (single, no swapping)")]
    [SerializeField] private CharacterComponentLinker _linker;

    [Header("Ground / Gravity")]
    [SerializeField] private float _gravity = 10f;
    [SerializeField] private float _rideHeight = 0.75f;
    [SerializeField] private float _rideSpringStrength = 200f;
    [SerializeField] private float _rideSpringDampener = 20f;
    [SerializeField] private float _groundCheckRadius = 0.1f;
    [SerializeField] private float _groundCheckDist = 0.5f;
    [SerializeField] private float _brakeStrength = 18f;
    [SerializeField] private PlayerCombatAudio _combatAudio;

    private readonly RaycastHit[] _raycastHits = new RaycastHit[10];
    private RaycastHit _groundHit;
    private bool _isGrounded;

    [Networked, OnChangedRender(nameof(OnKnockoutChanged))] public NetworkBool IsKnockedOut { get; private set; }

    private ActiveRagdollMember[] _activeRagdollMembers;
    private Rigidbody[] _allChildRigidbodies;
    private float[] _originalMasses;
    private float _startSlerpPositionSpring;
    private Animator _animator;
    private TickTimer _stunTimer;
    private bool _stunned;
    private Rigidbody _rb;

    public NetworkRigidbody3D NetworkedRb => _networkRb3D;
    public PlayerComponentRegistry Registry { get; private set; }

    private void Awake()
    {
        Registry = GetComponent<PlayerComponentRegistry>();
        _startSlerpPositionSpring = _mainJoint.slerpDrive.positionSpring;
        _rb = _networkRb3D != null ? _networkRb3D.Rigidbody : null;
    }

    public override void Spawned()
    {
        SetupRagdollFromLinker();
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || _rb == null) return;

        CheckForGround();

        if (_stunned && _stunTimer.Expired(Runner))
        {
            _stunned = false;
            _stunTimer = TickTimer.None;
            Recover();
        }

        if (IsKnockedOut)
        {
            // ragdolled: let it fall naturally, no gravity spring or braking
            _rb.AddForce(Vector3.down * _gravity, ForceMode.Force);
            return;
        }

        // conscious & standing: plant with ride-spring + kill residual sliding
        ApplyGravity();
        ApplyIdleBrakes();
    }

    private void CheckForGround()
    {
        _isGrounded = false;
        Vector3 castOrigin = _rb.position + (Vector3.up * 0.2f);
        int numHits = Physics.SphereCastNonAlloc(castOrigin, _groundCheckRadius, Vector3.down, _raycastHits, _groundCheckDist);

        float closest = float.MaxValue;
        bool found = false;
        for (int i = 0; i < numHits; i++)
        {
            if (_raycastHits[i].transform.root == transform.root) continue;   // ignore self
            if (_raycastHits[i].distance < closest)
            {
                closest = _raycastHits[i].distance;
                _groundHit = _raycastHits[i];
                found = true;
            }
        }
        _isGrounded = found;
    }


    private void ApplyGravity()
    {
        if (!_isGrounded)
        {
            _rb.AddForce(Vector3.down * _gravity);
            return;
        }

        Vector3 vel = _rb.linearVelocity;
        Vector3 rayDir = Vector3.down;
        float rayDirVel = Vector3.Dot(rayDir, vel);
        float currentHeight = _groundHit.distance;
        float x = currentHeight - _rideHeight;

        float springForce = (x * _rideSpringStrength) - (rayDirVel * _rideSpringDampener);
        if (springForce < 0f) springForce = 0f;

        _rb.AddForce(Vector3.up * springForce, ForceMode.Force);
    }

    private void ApplyIdleBrakes()
    {
        if (!_isGrounded) return;

        Vector3 v = _rb.linearVelocity;
        Vector3 horizontal = new Vector3(v.x, 0f, v.z);
        horizontal = Vector3.MoveTowards(horizontal, Vector3.zero, _brakeStrength * Runner.DeltaTime);
        _rb.linearVelocity = new Vector3(horizontal.x, v.y, horizontal.z);
    }

    /// <summary>
    /// the ragdoll-relevant half of ExecuteCharacterPackageSwap, for a single fixed package.
    /// </summary>
    private void SetupRagdollFromLinker()
    {
        if (_linker == null) return;

        if (_linker.characterAnimator != null)
        {
            _animator = _linker.characterAnimator;
            _animator.SetBool("IsGrounded", true);   // hold the standing idle
            _animator.Update(0f);
        }

        // VFX anchors (for stun stars, etc.)
        if (Registry != null && Registry.vfxAnchors != null)
            Registry.vfxAnchors.SetUpAnchors(_linker._head, _linker._leftEye, _linker._rightEye);

        if (Registry != null)
        {
            Registry.VisualsOverrider?.UpdateActiveCharacterVisualReference(_linker.visualMeshRoot);
            Registry.UpdateActiveLinker(_linker);
        }

        // active ragdoll members
        _activeRagdollMembers = _linker.physicsPackageRoot.GetComponentsInChildren<ActiveRagdollMember>(true);
        for (int i = 0; i < _activeRagdollMembers.Length; i++)
        {
            if (_activeRagdollMembers[i] != null)
            {
                _activeRagdollMembers[i]._animatedRoot = _linker.animatedModelRoot.transform;
                _activeRagdollMembers[i]._physicalRoot = _linker.physicsPackageRoot.transform;
                _activeRagdollMembers[i].InitializeIfNotDone();
                _activeRagdollMembers[i].ResetBoneToTarget();
            }
        }

        // rigidbodies + masses (for knockout mass toggle)
        _allChildRigidbodies = _linker.physicsPackageRoot.GetComponentsInChildren<Rigidbody>(true);
        _originalMasses = new float[_allChildRigidbodies.Length];
        for (int i = 0; i < _allChildRigidbodies.Length; i++)
        {
            _originalMasses[i] = _allChildRigidbodies[i].mass;
            _allChildRigidbodies[i].linearVelocity = Vector3.zero;
            _allChildRigidbodies[i].angularVelocity = Vector3.zero;
        }
    }

    public void Knockout()
    {
        if (!Object.HasStateAuthority) return;
        IsKnockedOut = true;
        SetCharacterMass(true);

        JointDrive d = _mainJoint.slerpDrive;
        d.positionSpring = 0f;
        _mainJoint.slerpDrive = d;

        if (_activeRagdollMembers != null)
            for (int i = 0; i < _activeRagdollMembers.Length; i++)
                _activeRagdollMembers[i].MakeRagdoll();
    }

    public void Recover()
    {
        if (!Object.HasStateAuthority) return;
        IsKnockedOut = false;
        SetCharacterMass(false);

        JointDrive d = _mainJoint.slerpDrive;
        d.positionSpring = _startSlerpPositionSpring;
        _mainJoint.slerpDrive = d;

        if (_activeRagdollMembers != null)
            for (int i = 0; i < _activeRagdollMembers.Length; i++)
                _activeRagdollMembers[i].MakeActiveRagdoll();
    }

    private void SetCharacterMass(bool knockedOut)
    {
        if (_allChildRigidbodies == null) return;
        for (int i = 0; i < _allChildRigidbodies.Length; i++)
            if (_allChildRigidbodies[i] != null)
                _allChildRigidbodies[i].mass = knockedOut ? _unconsciousMass : _originalMasses[i];
    }

    public void ApplyKnockback(Vector3 force, ForceMode mode = ForceMode.Impulse)
    {
        if (!Object.HasStateAuthority || _networkRb3D == null) return;
        _networkRb3D.Rigidbody.AddForce(force, mode);
    }

    public void InflictStatus(StatusEffectType type, float duration)
    {
        if (!Object.HasStateAuthority) return;

        // dummy only cares about Stunned -> knockout for the duration
        if (type == StatusEffectType.Stunned)
        {
            Knockout();
            _stunned = true;
            _stunTimer = TickTimer.CreateFromSeconds(Runner, duration);
        }
    }

    private void OnKnockoutChanged()
    {
        if (IsKnockedOut && _combatAudio != null)
        {
            _combatAudio.PlaySound(SoundID.Knockout);
            _combatAudio.PlaySound(SoundID.Oof);
        }
    }
}
