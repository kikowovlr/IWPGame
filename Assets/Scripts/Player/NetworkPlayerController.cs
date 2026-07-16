using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using Fusion.Addons.Physics;

// uses powers of 2 so they can be combined into a single binary value 
[System.Flags]
public enum InputRestrictions
{
    None = 0,
    BlockMovement = 1 << 0,  // 1
    BlockCombat = 1 << 1,  // 2
    BlockAbilities = 1 << 2,  // 4
    BlockRotation = 1 << 3, // 8
    BlockSeperateCameraMovement = 1 << 4,

    // quick-access group combinations
    BlockEverything = ~0       // links all flags together
}

public class NetworkPlayerController : NetworkBehaviour, IPlayerLeft, ICameraLockable
{
    public static NetworkPlayerController Local { get; set; }

    [Header("References")]
    [SerializeField] private Rigidbody _rb;
    [SerializeField] private Collider _mainCollider;
    [SerializeField] private ConfigurableJoint _mainJoint;
    [SerializeField] private Animator _animator;
    [SerializeField] NetworkRigidbody3D _networkRb3D;

    [Header("Movement Settings")]
    [SerializeField] private float _maxSpeed = 6f;
    [SerializeField] private float _movementForce = 30f;
    [SerializeField] private float _rotationSpeed = 300f;
    [Range(0.1f, 1.0f)]
    [SerializeField] private float _walkInputScale = 0.5f;
    [Networked] public float CurrentSpeedMultiplier { get; set; } = 1.0f;
    [Networked] public float TargetSpeedMultiplier { get; set; } = 1.0f;
    [SerializeField] private float _speedLerpRate = 10f;    

    [Header("Jump Settings")]
    [SerializeField] private float _jumpForce = 10f;
    [SerializeField] private float _gravity = 10f;
    [SerializeField] private float _jumpCooldown = 0.15f;
    [Networked] private float _lastJumpTime { get; set; }

    [Header("Ground Check Settings")]
    [SerializeField] private float _groundCheckRadius = 0.1f;
    [SerializeField] private float _groundCheckDist = 0.5f;

    [Header("Ragdoll Settings")]
    [SerializeField] private float _unconsciousMass = 0.2f;

    //Input
    Vector2 _moveInputVector = Vector2.zero;
    bool _isJumpButtonPressed = false;
    private bool _isHeadbuttButtonPressed = false;
    private bool _isRightClickButtonPressed = false;

    //States
    private bool _isGrounded;
    private bool _isRunning;
    private bool _isKnockedOut = false; // == non-active ragdoll
    private bool _isGrabbingActive = false;
    public bool IsGrounded => _isGrounded;

    //Slope handling
    [SerializeField] private float _maxSlopeAngle = 55f;
    [SerializeField] private float _slopeForceMultiplier = 1.5f;
    [SerializeField] private float _stickForce = 40f;
    [SerializeField] private float _brakeStrength = 18f;
    [SerializeField] private float _rideHeight = 0.75f; // ideal dist from player center to ground
    [SerializeField] private float _rideSpringStrength = 200f; // how forcefully it snaps back up to ride height
    [SerializeField] private float _rideSpringDampener = 20f; // prevents character from bouncing
    [SerializeField] private float _normalSmoothSpeed = 15f;
    [SerializeField] private float _idleAnchorSpringStrength = 100f;
    [SerializeField] private float _idleAnchorDampening = 20f;
    private Vector3 _groundNormal = Vector3.up;
    private Vector3 _smoothedGroundNormal = Vector3.up;
    private Vector3 _idleAnchorPosition;
    private bool _hasIdleAnchor = false;

    public Vector3 GroundNormal => _groundNormal;

    //Raycasts
    private readonly RaycastHit[] _raycastHits = new RaycastHit[10];
    private RaycastHit _groundHit;

    //Syncing of ragdoll parts
    ActiveRagdollMember[] _activeRagdollMembers;
    private Quaternion _initialJointRotation;

    // TODO: change to sending bytes instead of Quaternion and limite how much the joints can rotate
    [Networked, Capacity(30)] public NetworkArray<Quaternion> NetworkPhysicsSyncedRotation { get; }

    private const float InputThreshold = 0.01f;

    [SerializeField] private float _animationSpeedDamp = 10f; // how fast animation blends
    private float _smoothedInputSpeed = 0f;

    float _startSlerpPositionSpring = 0.0f;

    // Grabbing/Punching
    private float _pressStartTime;
    private bool _prevPunchOrGrabPressed;
    public float HoldThreshold = 0.2f; // how long before tap becomes a hold
    private HandGrabHandler[] _handGrabHandlers;
    private Rigidbody[] _allChildRigidbodies;
    private float[] _originalMasses;
    private PunchHandler _punchHandler;

    // kicking
    private KickHandler _kickHandler;

    // headbutt
    private HeadbuttHandler _headbuttHandler;

    // characters
    [Header("Character Visuals")]
    [SerializeField] private GameObject[] _characterPackages;
    [SerializeField] private int _defaultCharacterIndex = 0; // TEMP - default character
    [Networked, OnChangedRender(nameof(OnCharacterChanged))] public int CharacterIndex { get; set; }

    // abilities
    [Header("Ability Configuration")]
    private AbilitySO _equippedAbility;
    private bool _isAbilityPressed;
    private bool _isAbilityHeld;
    private bool _isAbilityReleased;
    private AbilityIndicatorController _activeAbilityIndicator;

    // buffers for abilities
    public readonly Collider[] HitBuffer = new Collider[20];
    public readonly RaycastHit[] RaycastHitBuffer = new RaycastHit[20];

    [Networked] private ref AbilityState CurrentAbilityState => ref MakeRef<AbilityState>();
    public bool CanMove => !IsInputBlocked(InputRestrictions.BlockMovement) && !IsInPhysicsRecovery;
    public bool CanRotate => !IsInputBlocked(InputRestrictions.BlockRotation) && !IsInPhysicsRecovery;
    public AbilityIndicatorController ActiveAbilityIndicator => _activeAbilityIndicator;

    // time to recover after knockback, throw, etc
    [Header("Physics Recovery")]
    [Networked] private TickTimer _physicsControlLockTimer { get; set; }
    [SerializeField] private float _maxKnockbackControlLockDuration = 0.5f;
    public bool IsInPhysicsRecovery => !_physicsControlLockTimer.ExpiredOrNotRunning(Runner);

    // input blocking
    // this clears and rebuilds at the top of every network tick
    public InputRestrictions ActiveRestrictions { get; private set; } = InputRestrictions.None;

    [Header("Camera Target")]
    [SerializeField] private Transform _cameraTarget;
    public bool IsCameraRotationLocked
    {
        get
        {
            return IsInputBlocked(InputRestrictions.BlockSeperateCameraMovement);
        }
    }

    // getters
    public bool IsKnockedOut => _isKnockedOut;
    public bool IsGrabbingActive => _isGrabbingActive;
    public NetworkRigidbody3D NetworkedRb => _networkRb3D;
    public Animator Animator => _animator;
    public Quaternion InitialJointRotation => _initialJointRotation;
    public PlayerComponentRegistry Registry { get; private set; }
    public ref AbilityState AbilityStateRef => ref CurrentAbilityState;
    public AbilitySO EquippedAbility => _equippedAbility;

    private void Awake()
    {
        _initialJointRotation = _mainJoint.transform.localRotation;
        _rb.useGravity = false;

        Registry = GetComponent<PlayerComponentRegistry>();
        if (Registry != null)
        {
            _punchHandler = Registry.Punch;
            _kickHandler = Registry.Kick;
            _headbuttHandler = Registry.Headbutt;
        }
    }

    private void Start()
    {
        // store initial slerp drive spring to reset when exiting ragdoll
        _startSlerpPositionSpring = _mainJoint.slerpDrive.positionSpring;
    }

    #region Inputs
    public void OnMove(InputValue value)
    {
        _moveInputVector = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        if (value.isPressed) // triggered when key is down
        {
            _isJumpButtonPressed = true;
        }
    }

    public void OnSprint(InputValue value)
    {
        _isRunning = value.Get<float>() > 0f;
    }

    public void OnHeadbutt(InputValue value)
    {
        if (value.isPressed)
        {
            _isHeadbuttButtonPressed = true;
        }
    }

    public void OnRightClickAction(InputValue value)
    {
        if (value.isPressed)
        {
            _isRightClickButtonPressed = true;
        }
    }

    public void OnSkill(InputValue value)
    {
        if (value.isPressed)
        {
            _isAbilityPressed = true;
            _isAbilityHeld = true;
        }
        else
        {
            _isAbilityReleased = true;
            _isAbilityHeld = false;
        }
    }
    #endregion

    private void Update()
    {
        // TODO - TEMP DEBUG SWITCH: Runs locally in standard frame updates to catch key strokes perfectly
        if (Object != null && Object.HasStateAuthority)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
                ExecuteCharacterPackageSwap(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2))
                ExecuteCharacterPackageSwap(1);
            else if (Input.GetKeyDown(KeyCode.Alpha3))
                ExecuteCharacterPackageSwap(2);
        }
    }

    public override void FixedUpdateNetwork()
    {
        CheckForGround();

        // only host can do this
        if (Object.HasStateAuthority) // means we are controlling object
        {
            // lerp speed
            CurrentSpeedMultiplier = Mathf.MoveTowards(
                CurrentSpeedMultiplier,
                TargetSpeedMultiplier,
                _speedLerpRate * Runner.DeltaTime
            );

            TargetSpeedMultiplier = 1.0f; // reset each frame, status effects, etc, will continuously overwrite this during ticks


            if (!IsKnockedOut)
                ApplyGravity();
            else
                // apply downward gravity when unconscious so their dead weight falls naturally
                _rb.AddForce(Vector3.down * _gravity, ForceMode.Force);
        }

        // reset input restrictions
        ActiveRestrictions = InputRestrictions.None;

        // check game manager for global restrictions
        if (GameManager.Instance != null)
            ActiveRestrictions |= GameManager.Instance.GlobalRestrictions;

        // holds the target anim float
        float targetAnimSpeed = 0f;

        // always update active combat states
        // -> ensures that this is updated on clients since punch timer is not networked
        if (_punchHandler != null && _punchHandler.IsPunching)
        {
            _punchHandler.UpdatePunchState();
        }

        if (_headbuttHandler != null && _headbuttHandler.IsHeadbutting)
        {
            _headbuttHandler.UpdateHeadbuttState();
        }

        if (GetInput(out NetworkInputData networkInputData))
        {        
            UpdateAbility(networkInputData);

            if (!_isKnockedOut)
            {
                // movement calculation
                // if not sprinting, clamp input
                //float inputMagnitude = networkInputData._movementInput.magnitude;
                float inputMagnitude = networkInputData._cameraRelativeMoveDir.magnitude;

                ProcessGrabPunchLogic(networkInputData._isPunchOrGrabPressed, inputMagnitude, networkInputData._isSprintPressed);

                // DYNAMIC RECOVERY CLEANUP
                // if player has slowed down enough or hit a wall, give control back
                if (IsInPhysicsRecovery && _rb.linearVelocity.magnitude < 1.5f)
                {
                    _physicsControlLockTimer = TickTimer.None;
                }

                // MOVEMENT GATE
                bool abilityControllingVelocity = _equippedAbility != null && CurrentAbilityState._isDashing;

                if (abilityControllingVelocity)
                {
                    // ability owns velocity, dont apply idle brakes
                    _hasIdleAnchor = false;
                    targetAnimSpeed = 1.0f;
                }
                else if (IsInPhysicsRecovery)
                {
                    targetAnimSpeed = 0f;
                }
                // dont call ProcessInputMovement if cannot move
                else if (!CanMove)
                {
                    ApplyIdleBrakes();
                    targetAnimSpeed = 0f;
                }
                else if (inputMagnitude > InputThreshold)
                {
                    // calculate the animation speed should be based entirely on input
                    targetAnimSpeed = networkInputData._isSprintPressed ? 1.0f : _walkInputScale;
                    ProcessInputMovement(networkInputData, inputMagnitude);
                }
                else
                {
                    ApplyIdleBrakes();
                }

                // ROTATION GATE
                if (CanRotate)
                {
                    // repurpose WASD for rotating
                    if (!CanMove && networkInputData._abilityAimDirection.sqrMagnitude > 0.01f)
                    {
                        Vector3 lookTarget = new Vector3(networkInputData._abilityAimDirection.x, 0f, networkInputData._abilityAimDirection.y);
                        Quaternion targetRot = Quaternion.LookRotation(lookTarget);

                        // eliminate spin resistance
                        _rb.angularVelocity = Vector3.zero;

                        transform.rotation = Quaternion.RotateTowards(
                            transform.rotation,
                            targetRot,
                            _rotationSpeed * Runner.DeltaTime
                        );

                        // need to tell joint to follow rotation!
                        _mainJoint.targetRotation = Quaternion.Inverse(transform.rotation) * _initialJointRotation;
                    }  
                }

                ProcessKickInput(networkInputData);
                ProcessHeadbuttInput(networkInputData);

                if (_equippedAbility != null)
                {
                    // if current ability is requesting for movement
                    if (CurrentAbilityState._isDashing)
                    {
                        Vector3 finalVelocity = CurrentAbilityState._customVelocity;
                        // only fall back to gravity's vertical velocity if airborne (ability doesn't know about falling)
                        if (!_isGrounded)
                        {
                            finalVelocity.y = _rb.linearVelocity.y;
                        }
                        _rb.linearVelocity = finalVelocity;
                    }
                }

                HandleJump(networkInputData);
                _prevPunchOrGrabPressed = networkInputData._isPunchOrGrabPressed;
            }

            // pass right click input data
            if (Object.HasStateAuthority)
            {
                foreach (HandGrabHandler handGrabHandler in _handGrabHandlers)
                {
                    handGrabHandler.ProcessThrowInputUpdate(networkInputData._isThrowPressed);
                }
            }
        }

        if (Object.HasStateAuthority)
        {
            if (_isKnockedOut)
                targetAnimSpeed = 0f;

            _smoothedInputSpeed = Mathf.MoveTowards(_smoothedInputSpeed, targetAnimSpeed, Runner.DeltaTime * _animationSpeedDamp);

            UpdateAnimations(_smoothedInputSpeed);

            // TODO
            // check if it is falling too far below map
            if (transform.position.y < -10)
                _networkRb3D.Teleport(Vector3.zero, Quaternion.identity);

            foreach (HandGrabHandler handGrabHandler in _handGrabHandlers)
            {
                handGrabHandler.UpdateState();
            }
        }
    }

    private void ProcessInputMovement(NetworkInputData networkInputData, float inputMagnitude)
    {
        _hasIdleAnchor = false; // clear anchor while actively moving

        Vector3 moveDir = CalculateMoveDirection(networkInputData);
        HandleRotation(moveDir);

        // calculate max speed based on speed multiplier
        float dynamicMaxSpeed = _maxSpeed * CurrentSpeedMultiplier;

        // counter velocity that isnt aligned with where player is going
        // prevents diagonal carry-overs when input dir flips
        Vector3 horizontalVel = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
        Vector3 desiredFlatDir = new Vector3(moveDir.x, 0f, moveDir.z).normalized;
        Vector3 lateralVel = horizontalVel - Vector3.Project(horizontalVel, desiredFlatDir);
        _rb.AddForce(-lateralVel * _brakeStrength, ForceMode.Force);

        if (_rb.linearVelocity.magnitude < dynamicMaxSpeed * inputMagnitude)
        {
            // calculate how steep the current slope is
            float slopeAngle = Vector3.Angle(Vector3.up, _groundNormal);
            float finalForce = _movementForce * CurrentSpeedMultiplier;

            // scale forces when climbing up hills
            if (_isGrounded && slopeAngle > 5f && slopeAngle <= _maxSlopeAngle)
            {
                // as slope gets steeper, scale forces
                float slopeFactor = slopeAngle / _maxSlopeAngle;
                finalForce += _movementForce * CurrentSpeedMultiplier * slopeFactor * _slopeForceMultiplier;
            }

            // move character in the dir they're facing
            _rb.AddForce(moveDir * finalForce, ForceMode.Force);
            GlueToSlope(slopeAngle);
        }
    }

    private void ProcessGrabPunchLogic(bool isPressed, float inputMagnitude, bool isSprintPressed)
    {
        // detect press start
        if (isPressed && !_prevPunchOrGrabPressed)
        {
            _pressStartTime = Runner.SimulationTime;
        }

        // detect release
        if (!isPressed && _prevPunchOrGrabPressed)
        {
            float pressDuration = Runner.SimulationTime - _pressStartTime;

            // tap threshold
            if (pressDuration < HoldThreshold)
            {
                // evaluate movement context at the moment of tap release
                bool isSprinting = isSprintPressed && inputMagnitude > InputThreshold;

                // Handle tap (punch)
                if (isSprinting && !_punchHandler.IsStrongPunchReady)
                {
                    // strong punch on cooldown
                    _punchHandler.TriggerPunch(true);
                }
                else
                {
                    // handles both weak and strong punch
                    _punchHandler.TriggerPunch(isSprinting);
                }
            }
            else
            {
                // Handle hold release (stop grabbing)
                _isGrabbingActive = false;
            }
        }

        // detect continuous hold
        if (isPressed && (Runner.SimulationTime - _pressStartTime) >= HoldThreshold)
        {
            if (!_isGrabbingActive)
            {
                _isGrabbingActive = true;
            }
        }
    }

    private void ProcessKickInput(NetworkInputData networkInputData)
    {
        if (_kickHandler == null || _kickHandler.IsKicking) return;

        if (networkInputData._isKickPressed)
        {
            // calculate exact move dir of input
            Vector3 inputDir = CalculateMoveDirection(networkInputData);
            _kickHandler.TriggerAirKick(inputDir, networkInputData._isSprintPressed);
        }
    }

    private void ProcessHeadbuttInput(NetworkInputData networkInputData)
    {
        if (_headbuttHandler == null || _headbuttHandler.IsHeadbutting) return;

        if (networkInputData._isHeadbuttPressed)
        {
            _headbuttHandler.TriggerHeadbutt();
        }
    }

    private void GlueToSlope(float slopeAngle)
    {
        // if we are on a slope, add a downward force to keep us glued to it
        if (_isGrounded && slopeAngle > 5f && _rb.linearVelocity.y > 0.1f)
        {
            _rb.AddForce(Vector3.down * _stickForce, ForceMode.Force);
        }
    }

    private void ApplyIdleBrakes()
    {
        Vector3 currentVelocity = _rb.linearVelocity;

        if (_isGrounded)
        {
            float slopeAngle = Vector3.Angle(Vector3.up, _groundNormal);

            // on slope
            if (slopeAngle > 2f && slopeAngle <= _maxSlopeAngle)
            {
                // stop horizontal sliding on hills
                _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);

                // counteract gravity
                _rb.AddForce(Vector3.up * _gravity * _rb.mass, ForceMode.Force);

                // lock an anchor the moment we go idle on a slope
                if (!_hasIdleAnchor)
                {
                    _idleAnchorPosition = _rb.position;
                    _hasIdleAnchor = true;
                }

                // spring back toward anchor if there is residual drift
                Vector3 offset = _idleAnchorPosition - _rb.position;
                offset.y = 0f;

                Vector3 correctiveForce = (offset * _idleAnchorSpringStrength) - (currentVelocity * _idleAnchorDampening);
                _rb.AddForce(correctiveForce, ForceMode.Force);
            }
            else
            {
                _hasIdleAnchor = false;
                // deceleration on flat ground
                Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
                // brake
                horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, Vector3.zero, _brakeStrength * Runner.DeltaTime);
                _rb.linearVelocity = new Vector3(horizontalVelocity.x, currentVelocity.y, horizontalVelocity.z);
            }
        }
        else
        {
            _hasIdleAnchor = false;
        }
    }

    public override void Render()
    {
        // all clients run this code
        if (!Object.HasStateAuthority)
        {
            var interpolated = new NetworkBehaviourBufferInterpolator(this);

            // get networked physics objects from the host and update clients
            for (int i = 0; i < _activeRagdollMembers.Length; i++)
            {
                _activeRagdollMembers[i].transform.localRotation = Quaternion.Slerp(_activeRagdollMembers[i].transform.localRotation, NetworkPhysicsSyncedRotation.Get(i), interpolated.Alpha);
            }
        }

        if (Object.HasInputAuthority)
        {
            if (PlayerRegistry.SceneBrain != null && PlayerRegistry.SceneVirtualCamera != null)
            {
                PlayerRegistry.SceneBrain.ManualUpdate();
                PlayerRegistry.SceneVirtualCamera.UpdateCameraState(Vector3.up, Runner.LocalAlpha);
            }
        } 
    }

    private void CheckForGround()
    {
        // assume we are not grounded
        _isGrounded = false;
        Vector3 castOrigin = _rb.position + (Vector3.up * 0.2f);

        // check if we are grounded
        int numOfHits = Physics.SphereCastNonAlloc(castOrigin, _groundCheckRadius, transform.up * -1, _raycastHits, _groundCheckDist);

        float closestDistance = float.MaxValue;
        bool foundValidHit = false;

        // check for valid results
        for (int i = 0; i < numOfHits; i++)
        {
            //ignore self hits
            if (_raycastHits[i].transform.root == transform)
                continue;
            if (_raycastHits[i].transform.root.TryGetComponent(out NetworkPlayerController otherPlayer))
                continue;

            if (_raycastHits[i].distance < closestDistance)
            {
                closestDistance = _raycastHits[i].distance;
                _groundHit = _raycastHits[i];
                foundValidHit = true;
            }
        }

        if (foundValidHit)
        {
            _isGrounded = true;
            // smooth normal instead of micro bumps due to uneven floor
            _smoothedGroundNormal = Vector3.Slerp(_smoothedGroundNormal, _groundHit.normal, Runner.DeltaTime * _normalSmoothSpeed);
        }
        else
        {
            _smoothedGroundNormal = Vector3.Slerp(_smoothedGroundNormal, Vector3.up, Runner.DeltaTime * _normalSmoothSpeed);
        }

        _groundNormal = _smoothedGroundNormal;
    }

    private void ApplyGravity()
    {
        // just jumped, dont apply gravity
        if (Runner.SimulationTime - _lastJumpTime < 0.05f)
            return;

        // apply more gravity to make character less floaty
        if (!_isGrounded)
        {
            _rb.AddForce(Vector3.down * _gravity);
            return;
        }

        // ground floating spring
        RaycastHit hit = _groundHit;

        // calculate dir of velocity relative to world
        Vector3 vel = _rb.linearVelocity;
        Vector3 rayDir = transform.up * -1f;

        // calculate how much ray is compressed compared to our target height
        float rayDirVel = Vector3.Dot(rayDir, vel);
        float relVel = rayDirVel;

        float currentHeight = hit.distance;
        float x = currentHeight - _rideHeight;

        // Hooke's Law Spring Equation: Force = (Compression * Stiffness) - (Velocity * Dampening)
        float springForce = (x * _rideSpringStrength) - (relVel * _rideSpringDampener);

        if (springForce < 0f) springForce = 0f;

        _rb.AddForce(transform.up * springForce, ForceMode.Force);
    }

    private Vector3 CalculateMoveDirection(NetworkInputData networkInputData)
    {
        Vector3 rawMoveDir = networkInputData._cameraRelativeMoveDir;

        // if grounded, tilt dir to match slope of ground
        if (_isGrounded && rawMoveDir.sqrMagnitude > 0.01f)
        {
            float slopeAngle = Vector3.Angle(Vector3.up, _groundNormal);

            if (slopeAngle > 3f)
            {
                Quaternion slopeRotation = Quaternion.FromToRotation(Vector3.up, _groundNormal);
                return slopeRotation * rawMoveDir; // keep input scaling accurate
            }
        }

        return rawMoveDir;
    }

    private void HandleRotation(Vector3 moveDir)
    {
        // based on camera dir
        Quaternion desiredWorldRotation = Quaternion.LookRotation(moveDir, transform.up);
        Quaternion jointSpaceRotation = Quaternion.Inverse(desiredWorldRotation) * _initialJointRotation;

        // rotate towards target dir
        _mainJoint.targetRotation = Quaternion.RotateTowards(_mainJoint.targetRotation, jointSpaceRotation, Runner.DeltaTime * _rotationSpeed);
    }

    private void HandleJump(NetworkInputData networkInputData)
    {
        // prevent re-simulation steps from spamming multiple forces
        if (Runner.SimulationTime - _lastJumpTime < _jumpCooldown) return;

        if (_isGrounded && networkInputData._isJumpPressed)
        {
            _lastJumpTime = Runner.SimulationTime;

            Vector3 currentVel = _rb.linearVelocity;
            _rb.linearVelocity = new Vector3(currentVel.x, 0f, currentVel.z);

            _rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);

            _isJumpButtonPressed = false; //reset immediately
            _isGrounded = false;
        }
    }

    private void UpdateAnimations(float animationValue)
    {
        _animator.SetFloat("MovementSpeed", animationValue);
        _animator.SetBool("IsKnockedOut", _isKnockedOut);

        // update joints rotation based on animation
        for (int i = 0; i < _activeRagdollMembers.Length; i++)
        {
            _activeRagdollMembers[i].UpdateJointFromAnimation();
            NetworkPhysicsSyncedRotation.Set(i, _activeRagdollMembers[i].transform.localRotation);
        }
    }

    public void Knockout()
    {
        if (!Object.HasStateAuthority)
            return;

        _isKnockedOut = true;
        _isGrabbingActive = false;

        SetCharacterMass(true);

        // update main joint
        JointDrive jointDrive = _mainJoint.slerpDrive;
        jointDrive.positionSpring = 0f;
        _mainJoint.slerpDrive = jointDrive;

        // update joints rotation and send them to clients
        for (int i = 0; i < _activeRagdollMembers.Length; i++)
        {
            _activeRagdollMembers[i].MakeRagdoll();
        }
    }

    public void Recover(bool playAnim = true)
    {
        if (!Object.HasStateAuthority)
            return;

        // TODO play recovery anim
        if (playAnim)
        {

        }

        _isKnockedOut = false;
        _isGrabbingActive = false;

        SetCharacterMass(false);

        // update main joint
        JointDrive jointDrive = _mainJoint.slerpDrive;
        jointDrive.positionSpring = _startSlerpPositionSpring;
        _mainJoint.slerpDrive = jointDrive;

        // update joints rotation and send them to clients
        for (int i = 0; i < _activeRagdollMembers.Length; i++)
        {
            _activeRagdollMembers[i].MakeActiveRagdoll();
        }
    }

    void SetCharacterMass(bool isKnockedOut)
    {
        if (_allChildRigidbodies == null || _allChildRigidbodies.Length == 0) return;

        for (int i = 0; i < _allChildRigidbodies.Length; i++)
        {
            if (_allChildRigidbodies[i] != null)
            {
                // if knocked out, make them weightless
                _allChildRigidbodies[i].mass = isKnockedOut ? _unconsciousMass : _originalMasses[i];
            }
        }
    }

    /// <summary>
    /// checks if a specific Rigidbody is securely held by BOTH of player's hands
    /// </summary>
    public bool IsObjectHeldByBothHands(Rigidbody targetRb)
    {
        int holdCount = 0;
        foreach (HandGrabHandler handGrabHandler in _handGrabHandlers)
        {
            if (handGrabHandler.IsHoldingObject(targetRb))
            {
                holdCount++;
            }
        }
        return holdCount >= 2;
    }

    public bool IsCurrentlyGrabbingObject()
    {
        if (_handGrabHandlers == null) return false;

        foreach (HandGrabHandler handler in _handGrabHandlers)
        {
            if (handler != null && handler.IsGrabbingSomething)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateAbility(NetworkInputData inputData)
    {
        if (_equippedAbility != null)
            _equippedAbility.UpdateAbilityState(this, ref CurrentAbilityState);
        
        // only run on host, allow input authority so client can predict
        if (!Object.HasStateAuthority && !Object.HasInputAuthority) return;

        if (Object.HasStateAuthority)
        {
            if (CurrentAbilityState._cooldownTimer > 0)
                CurrentAbilityState._cooldownTimer -= Runner.DeltaTime;
        }


        // if ability on cooldown and is not being used currently, block input events completely
        bool canStartAbility = CurrentAbilityState._cooldownTimer <= 0
                           && !CurrentAbilityState._isCharging
                           && !CurrentAbilityState._isDashing;

        if (inputData._abilityPressed && canStartAbility)
            _equippedAbility.OnTickPressed(this, ref CurrentAbilityState, inputData._abilityAimDirection);
        else if (CurrentAbilityState._isCharging)
        {
            if (inputData._abilityReleased)
            {
                _equippedAbility.OnTickReleased(this, ref CurrentAbilityState, inputData._abilityAimDirection);

                // set CD
                CurrentAbilityState._cooldownTimer = _equippedAbility._baseCooldown;
            }
            else if (inputData._abilityHeld)
                _equippedAbility.OnTickHeld(this, ref CurrentAbilityState, inputData._abilityAimDirection);
        }
    }

    /// <summary>
    /// animation event method for abilities
    /// </summary>
    public void UnityEvent_OnAbilityImpact()
    {
        if (!Object.HasStateAuthority) return;

        if (_equippedAbility != null)
        {
            _equippedAbility.OnAnimationImpactTriggered(this);
        }
    }

    public void UnityEvent_OnAbilityEnd()
    {
        if (!Object.HasStateAuthority) return;

        if (_equippedAbility != null)
        {
            _equippedAbility.OnAnimationEndTriggered(this);
        }
    }

    private Vector2 CalculateMouseAimDirection()
    {
        if (Camera.main == null) return Vector2.zero;

        // project ray from camera lens through screen's cursor position
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        // create invisible plane flat on the ground whr player stands
        Plane groundPlane = new Plane(Vector3.up, transform.position);

        // find where ray intersects plane
        if (groundPlane.Raycast(ray, out float rayDistance))
        {
            Vector3 mouseWorldPosition = ray.GetPoint(rayDistance);

            Vector3 lookDirection3D = mouseWorldPosition - transform.position;
            lookDirection3D.y = 0f;

            if (lookDirection3D.sqrMagnitude > 0.001f)
            {
                return new Vector2(lookDirection3D.x, lookDirection3D.z).normalized;
            }
        }

        return Vector2.zero;
    }

    private void OnCharacterChanged()
    {
        ExecuteCharacterPackageSwap(CharacterIndex);
    }

    /// <summary>
    /// call this fn when swapping character
    /// </summary>
    /// <returns></returns>
    public void ExecuteCharacterPackageSwap(int targetVariantIndex)
    {
        // loop through all packages, turn on chosen one
        for (int i = 0; i < _characterPackages.Length; i++)
        {
            if (_characterPackages[i] != null)
            {
                // if matches target, set true
                _characterPackages[i].SetActive(i == targetVariantIndex);
            }
        }

        // extract references from new package
        GameObject activePackage = _characterPackages[targetVariantIndex];
        if (activePackage != null)
        {
            CharacterComponentLinker linker = activePackage.GetComponent<CharacterComponentLinker>();
            if (linker != null)
            {
                _equippedAbility = linker.ability;
                _activeAbilityIndicator = linker.abilityIndicator;

                if (_punchHandler != null)
                    _punchHandler.SetUpActiveLimbs(linker._leftHandDamageDealer, linker._rightHandDamageDealer, linker._leftHandRb, linker._rightHandRb);

                if (_kickHandler != null)
                    _kickHandler.SetUpActiveLimbs(linker._rightFootDamageDealer);

                if (_headbuttHandler != null)
                    _headbuttHandler.SetUpActiveLimbs(linker._headDamageDealer);

                _handGrabHandlers = linker._grabHandlers;
                 
                if (linker.characterAnimator != null)
                    _animator = linker.characterAnimator;

                Registry.vfxAnchors.SetUpAnchors(linker._head, linker._leftEye, linker._rightEye);

                Registry.VisualsOverrider.UpdateActiveCharacterVisualReference(linker.visualMeshRoot);

                Registry.UpdateActiveLinker(linker);

                _activeRagdollMembers = linker.physicsPackageRoot.GetComponentsInChildren<ActiveRagdollMember>(true);

                for (int i = 0; i < _activeRagdollMembers.Length; i++)
                {
                    if (_activeRagdollMembers[i] != null)
                    {
                        _activeRagdollMembers[i]._animatedRoot = linker.animatedModelRoot.transform;
                        _activeRagdollMembers[i]._physicalRoot = linker.physicsPackageRoot.transform;

                        _activeRagdollMembers[i].InitializeIfNotDone();
                        _activeRagdollMembers[i].ResetBoneToTarget();
                    }
                }

                // regather rigidbodies belonging to new character
                _allChildRigidbodies = linker.physicsPackageRoot.GetComponentsInChildren<Rigidbody>(true);
                _originalMasses = new float[_allChildRigidbodies.Length];
                for (int i = 0; i < _allChildRigidbodies.Length; i++)
                {
                    _originalMasses[i] = _allChildRigidbodies[i].mass;

                    _allChildRigidbodies[i].linearVelocity = Vector3.zero;
                    _allChildRigidbodies[i].angularVelocity = Vector3.zero;
                }
            }
        }
    }

    /// <summary>
    /// helper utility to check binary flags for input restrictions
    /// </summary>
    public bool IsInputBlocked(InputRestrictions restriction)
    {
        if (_isKnockedOut) return true; // if knocked out, restrict all

        // & compared 2 sets of binary numbers
        // if the result is equal to the restriction, then that means the restriction is active
        return (ActiveRestrictions & restriction) == restriction;
    }

    public void AddInputRestriction(InputRestrictions restriction)
    {
        ActiveRestrictions |= restriction;
    }

    private void ApplyInputMask(ref NetworkInputData inputData)
    {
        bool blockAll = _isKnockedOut || IsInputBlocked(InputRestrictions.BlockEverything);

        if (blockAll || IsInputBlocked(InputRestrictions.BlockCombat) || IsInputBlocked(InputRestrictions.BlockAbilities))
        {
            _isHeadbuttButtonPressed = false;
            _isRightClickButtonPressed = false;
            _isJumpButtonPressed = false;
            _isAbilityPressed = false;
            _isAbilityReleased = false;
        }

        if (blockAll || IsInputBlocked(InputRestrictions.BlockMovement))
        {
            inputData._cameraRelativeMoveDir = Vector2.zero;
            inputData._isJumpPressed = false;
            _isJumpButtonPressed = false;
            inputData._isSprintPressed = false;
            _isRunning = false;
        }

        if (blockAll || IsInputBlocked(InputRestrictions.BlockCombat))
        {
            inputData._isPunchOrGrabPressed = false;
            inputData._isThrowPressed = false;
            inputData._isKickPressed = false;
            inputData._isHeadbuttPressed = false;
        }

        if (blockAll || IsInputBlocked(InputRestrictions.BlockAbilities))
        {
            inputData._abilityPressed = false;
            inputData._abilityHeld = false;
            inputData._abilityReleased = false;
            _isAbilityHeld = false;
            inputData._abilityAimDirection = Vector2.zero;
        }
    }

    public void ClearActiveCastingState()
    {
        if (!Object.HasStateAuthority) return;

        // grab the networked struct instance copy
        var state = CurrentAbilityState;
        state._isCasting = false;

        // assign it back to update the network state
        CurrentAbilityState = state;
    }

    /// <summary>
    /// modular method to knock this player away over the network
    /// </summary>
    public void ApplyKnockback(Vector3 forceVector, ForceMode mode = ForceMode.Impulse)
    {
        if (!Object.HasStateAuthority) return;

        // lock player from moving/rotating for a max safety window
        _physicsControlLockTimer = TickTimer.CreateFromSeconds(Runner, _maxKnockbackControlLockDuration);

        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.AddForce(forceVector, mode);
        }
    }

    /// <summary>
    /// call when player transitions to spectator
    /// shuts down all colliders
    /// </summary>
    public void DisableAllRigidbodyColliders()
    {
        if (_mainCollider == null && _rb == null) return;

        _mainCollider.enabled = false;
        _rb.isKinematic = true;

        if (_allChildRigidbodies == null) return;

        for (int i = 0; i < _allChildRigidbodies.Length; i++)
        {
            if (_allChildRigidbodies[i] != null)
            {
                if (_allChildRigidbodies[i].TryGetComponent<Collider>(out var col))
                {
                    col.enabled = false;
                }

                _allChildRigidbodies[i].isKinematic = true;
            }
        }
    }

    public void EnableAllRagdollColliders()
    {
        if (_mainCollider == null && _rb == null) return;

        _mainCollider.enabled = true;
        _rb.isKinematic = false;

        if (_allChildRigidbodies == null) return;

        for (int i = 0; i < _allChildRigidbodies.Length; i++)
        {
            if (_allChildRigidbodies[i] != null)
            {
                if (_allChildRigidbodies[i].TryGetComponent<Collider>(out var col))
                {
                    col.enabled = true;
                }

                _allChildRigidbodies[i].isKinematic = false;

                if (_originalMasses != null && i < _originalMasses.Length)
                {
                    _allChildRigidbodies[i].mass = _originalMasses[i];
                }
            }
        }
    }

    // spawner calls this then transmit info to host
    public NetworkInputData GetNetworkInput()
    {
        NetworkInputData networkInputData = new NetworkInputData();

        // move data
        if (_isKnockedOut)
        {
            networkInputData._cameraRelativeMoveDir = Vector2.zero;
            networkInputData._isJumpPressed = false;
            networkInputData._isSprintPressed = false;
            networkInputData._isPunchOrGrabPressed = false;
            networkInputData._isThrowPressed = false;
            networkInputData._isKickPressed = false;
            networkInputData._isHeadbuttPressed = false;

            // consume and clear the buffered flags even while knocked out
            _isHeadbuttButtonPressed = false;
            _isRightClickButtonPressed = false;

            networkInputData._abilityPressed = false;
            networkInputData._abilityReleased = false;
            networkInputData._abilityHeld = false;
            _isAbilityPressed = false;
            _isAbilityReleased = false;
        }
        else
        {
            Vector3 calculatedDirection = Vector3.zero;

            // calculate direction based on local camera instance
            if (CameraManager.Instance != null && _moveInputVector.sqrMagnitude > 0.01f)
            {
                Vector3 camForward = CameraManager.Instance.GetCameraForward();
                Vector3 camRight = CameraManager.Instance.GetCameraRight();

                // based on cam POV
                calculatedDirection = (camForward * _moveInputVector.y) + (camRight * _moveInputVector.x);
            }

            networkInputData._cameraRelativeMoveDir = calculatedDirection.normalized;
            networkInputData._isJumpPressed = _isJumpButtonPressed;
            networkInputData._isSprintPressed = _isRunning;
            networkInputData._isPunchOrGrabPressed = Input.GetMouseButton(0);
            networkInputData._isHeadbuttPressed = _isHeadbuttButtonPressed;
            _isHeadbuttButtonPressed = false; // reset immediately after packing

            bool isRightClickPressed = _isRightClickButtonPressed;
            _isRightClickButtonPressed = false;

            networkInputData._abilityPressed = _isAbilityPressed;
            networkInputData._abilityHeld = _isAbilityHeld;
            networkInputData._abilityReleased = _isAbilityReleased;
            _isAbilityPressed = false;
            _isAbilityReleased = false;

            if (networkInputData._abilityReleased)
            {
                _isAbilityHeld = false;
            }

            if (_isAbilityHeld)
            {
                // while charging, use mouse for aim dir
                networkInputData._cameraRelativeMoveDir = Vector3.zero;
                networkInputData._abilityAimDirection = CalculateMouseAimDirection();
            }
            else
            {
                // otherwise, wasd for normal movement
                networkInputData._abilityAimDirection = Vector2.zero;
            }

            if (isRightClickPressed)
            {
                // holding object == throw
                if (IsCurrentlyGrabbingObject())
                {
                    networkInputData._isThrowPressed = true;
                    networkInputData._isKickPressed = false;
                }
                // not grounded and not holding anything == kick
                else if (!_isGrounded)
                {
                    networkInputData._isThrowPressed = false;
                    networkInputData._isKickPressed = true;
                }
                else
                {
                    // default
                    networkInputData._isThrowPressed = true;
                    networkInputData._isKickPressed = false;
                }
            }
            else
            {
                // No right click interaction this frame
                networkInputData._isThrowPressed = false;
                networkInputData._isKickPressed = false;
            }
        }

        ApplyInputMask(ref networkInputData);

        // reset jump button 
        _isJumpButtonPressed = false;

        return networkInputData;
    }

    public override void Spawned()
    {
        base.Spawned();
        DontDestroyOnLoad(gameObject);

        Transform finalCameraTarget = _cameraTarget != null ? _cameraTarget : this.transform;

        // link network id to this physical avatar root
        PlayerRegistry.SetAvatarTransform(Object.InputAuthority, finalCameraTarget);

        // check if this is the owner's player
        if (Object.HasInputAuthority)
        {
            Local = this;
            PlayerRegistry.RegisterLocalPlayerTransform(finalCameraTarget);
        }

        // make it easier to tell which player is which
        transform.name = $"P_{Object.Id}";

        GameObject startingPackage = _characterPackages[_defaultCharacterIndex];
        Animator startingAnimator = startingPackage.GetComponentInChildren<Animator>();
        if (startingAnimator != null)
        {
            startingAnimator.Update(0f);
        }

        ExecuteCharacterPackageSwap(_defaultCharacterIndex);
    }

    // prevent null ref when player dc/leave
    public override void Despawned(NetworkRunner runner, bool hasStateAuthority)
    {
        base.Despawned(runner, hasStateAuthority);

        // Clean up the local dictionary slot when this avatar is destroyed/despawned
        PlayerRegistry.SetAvatarTransform(Object.InputAuthority, null);
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (Object.InputAuthority == player)
            Runner.Despawn(Object);
    }

    private void OnDrawGizmos()
    {
        if (_equippedAbility != null && _equippedAbility is RamAbilitySO goatRam)
        {
            // Pass your runtime state reference structure inside to fetch active visual states
            goatRam.DrawAbilityGizmos(this, ref this.CurrentAbilityState);
        }
    }
}
