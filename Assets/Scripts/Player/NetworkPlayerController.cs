using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using Fusion.Addons.Physics;
using Unity.Cinemachine;

/// <summary>
/// IPlayerLeft - can clean up player later when they leave
/// </summary>
public class NetworkPlayerController : NetworkBehaviour, IPlayerLeft
{
    public static NetworkPlayerController Local { get; set; }

    [Header("References")]
    [SerializeField] private Rigidbody _rb;
    [SerializeField] private ConfigurableJoint _mainJoint;
    [SerializeField] private Animator _animator;
    [SerializeField] NetworkRigidbody3D _networkRb3D;

    [Header("Movement Settings")]
    [SerializeField] private float _maxSpeed = 6f;
    [SerializeField] private float _movementForce = 30f;
    [SerializeField] private float _rotationSpeed = 300f;
    [Range(0.1f, 1.0f)]
    [SerializeField] private float _walkInputScale = 0.5f;

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

    //States
    private bool _isGrounded;
    private bool _isRunning;
    private bool _isKnockedOut = false; // == non-active ragdoll
    private bool _isGrabbingActive = false;

    //Slope handling
    [SerializeField] private float _maxSlopeAngle = 55f;
    [SerializeField] private float _slopeForceMultiplier = 1.5f;
    [SerializeField] private float _stickForce = 40f;
    [SerializeField] private float _brakeStrength = 18f;
    [SerializeField] private float _rideHeight = 0.75f; // ideal dist from player center to ground
    [SerializeField] private float _rideSpringStrength = 200f; // how forcefully it snaps back up to ride height
    [SerializeField] private float _rideSpringDampener = 20f; // prevents character from bouncing
    private Vector3 _groundNormal = Vector3.up;

    //Raycasts
    private readonly RaycastHit[] _raycastHits = new RaycastHit[10];
    private RaycastHit _groundHit;

    //Syncing of ragdoll parts
    ActiveRagdollMember[] _activeRagdollMembers;
    private Quaternion _initialJointRotation;

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

    // getters
    public bool IsKnockedOut => _isKnockedOut;
    public bool IsGrabbingActive => _isGrabbingActive;
    public NetworkRigidbody3D NetworkedRb => _networkRb3D;
    public Animator Animator => _animator;
    public Quaternion InitialJointRotation => _initialJointRotation;

    //Syncing client ragdolls
    // TODO: change to sending bytes instead of Quaternion and limite how much the joints can rotate
    [Networked, Capacity(30)] public NetworkArray<Quaternion> NetworkPhysicsSyncedRotation { get; }


    private void Awake()
    {
        _activeRagdollMembers = GetComponentsInChildren<ActiveRagdollMember>();
        _initialJointRotation = _mainJoint.transform.localRotation;
        _handGrabHandlers = GetComponentsInChildren<HandGrabHandler>();

        PlayerComponentRegistry registry = GetComponent<PlayerComponentRegistry>();
        if (registry != null)
        {
            _punchHandler = registry.Punch;
            _kickHandler = registry.Kick;
        }

        // save rbs and mass for ragdoll
        _allChildRigidbodies = GetComponentsInChildren<Rigidbody>();
        _originalMasses = new float[_allChildRigidbodies.Length];
        for (int i = 0; i < _allChildRigidbodies.Length; i++)
        {
            _originalMasses[i] = _allChildRigidbodies[i].mass;
        }
    }

    private void Start()
    {
        // store initial slerp drive spring to reset when exiting ragdoll
        _startSlerpPositionSpring = _mainJoint.slerpDrive.positionSpring;
    }

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

    public override void FixedUpdateNetwork()
    {
        // only host can do this
        if (Object.HasStateAuthority) // means we are controlling object
        {
            CheckForGround();

            if (!IsKnockedOut)
                ApplyGravity();
            else
                // apply downward gravity when unconscious so their dead weight falls naturally
                _rb.AddForce(Vector3.down * _gravity, ForceMode.Force);
        }

        // holds the target anim float
        float targetAnimSpeed = 0f;

        // always update active combat states
        // -> ensures that this is updated on clients since punch timer is not networked
        if (_punchHandler != null && _punchHandler.IsPunching)
        {
            _punchHandler.UpdatePunchState();
        }

        if (GetInput(out NetworkInputData networkInputData))
        {
            // testing
            if (networkInputData._isRagdollPressed)
            {
                if (!IsKnockedOut)
                    Knockout();
                else
                    Recover();
            }

            if (!_isKnockedOut)
            {
                // movement calculation
                // if not sprinting, clamp input
                float inputMagnitude = networkInputData._movementInput.magnitude;

                ProcessGrabPunchLogic(networkInputData._isPunchOrGrabPressed, inputMagnitude, networkInputData._isSprintPressed);

                if (inputMagnitude > InputThreshold)
                {
                    // calculate the animation speed should be based entirely on input
                    targetAnimSpeed = networkInputData._isSprintPressed ? 1.0f : _walkInputScale;
                    ProcessInputMovement(networkInputData, inputMagnitude);
                }
                else
                {
                    ApplyIdleBrakes();
                }

                ProcessKickInput(networkInputData);

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
        Vector3 moveDir = CalculateMoveDirection(networkInputData);
        HandleRotation(moveDir);

        if (_rb.linearVelocity.magnitude < _maxSpeed * inputMagnitude)
        {
            // calculate how steep the current slope is
            float slopeAngle = Vector3.Angle(Vector3.up, _groundNormal);
            float finalForce = _movementForce;

            // scale forces when climbing up hills
            if (_isGrounded && slopeAngle > 5f && slopeAngle <= _maxSlopeAngle)
            {
                // as slope gets steeper, scale forces
                float slopeFactor = slopeAngle / _maxSlopeAngle;
                finalForce += _movementForce * slopeFactor * _slopeForceMultiplier;
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
                _rb.angularVelocity = Vector3.zero;

                // counteract gravity
                _rb.AddForce(-Physics.gravity * _rb.mass, ForceMode.Force);
            }
            else
            {
                // deceleration on flat ground
                Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
                // brake
                horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, Vector3.zero, _brakeStrength * Runner.DeltaTime);
                _rb.linearVelocity = new Vector3(horizontalVelocity.x, currentVelocity.y, horizontalVelocity.z);
            }
        }

        _rb.angularVelocity = Vector3.MoveTowards(_rb.angularVelocity, Vector3.zero, _brakeStrength * Runner.DeltaTime);
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
        _groundNormal = Vector3.up;
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
            _groundNormal = _groundHit.normal;
        }
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
        // get cam dir vectors and flatten y
        Vector3 camForward = Camera.main.transform.forward;
        Vector3 camRight = Camera.main.transform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        // movement dir vector based on cam's POV
        Vector3 rawMoveDir = (camForward * networkInputData._movementInput.y) + (camRight * networkInputData._movementInput.x);

        // if grounded, tilt dir to match slope of ground
        if (_isGrounded)
        {
            Vector3 slopeMoveDir = Vector3.ProjectOnPlane(rawMoveDir, _groundNormal).normalized;
            return slopeMoveDir * rawMoveDir.magnitude; // keep input scaling accurate
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

    public void Recover()
    {
        if (!Object.HasStateAuthority)
            return;

        // TODO play recovery anim

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

    // spawner calls this then transmit info to host
    public NetworkInputData GetNetworkInput()
    {
        NetworkInputData networkInputData = new NetworkInputData();

        // move data
        if (_isKnockedOut)
        {
           networkInputData._movementInput = Vector2.zero;
            networkInputData._isJumpPressed = false;
            networkInputData._isSprintPressed = false;
            networkInputData._isPunchOrGrabPressed = false;
            networkInputData._isThrowPressed = false;
            networkInputData._isKickPressed = false;
        }
        else
        {
            networkInputData._movementInput = _moveInputVector;
            networkInputData._isJumpPressed = _isJumpButtonPressed;
            networkInputData._isSprintPressed = _isRunning;
            networkInputData._isPunchOrGrabPressed = Input.GetMouseButton(0);

            bool isRightClickPressed = Input.GetMouseButtonDown(1);

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

        networkInputData._isRagdollPressed = Input.GetKeyDown(KeyCode.R);

        // reset jump button 
        _isJumpButtonPressed = false;

        return networkInputData;
    }

    public override void Spawned()
    {
        // check if this is the owner's player
        if (Object.HasInputAuthority)
        {
            Local = this;
            PlayerRegistry.RegisterLocalPlayerTransform(_rb.transform);
            Utils.DebugLog("Spawned player with input authority");
        }
        else
            Utils.DebugLog("Spawned player without input authority");

        // make it easier to tell which player is which
        transform.name = $"P_{Object.Id}";
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (Object.InputAuthority == player)
            Runner.Despawn(Object);
    }
}
