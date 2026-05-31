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

    [Header("Ground Check Settings")]
    [SerializeField] private float _groundCheckRadius = 0.1f;
    [SerializeField] private float _groundCheckDist = 0.5f;

    //Input
    Vector2 _moveInputVector = Vector2.zero;
    bool _isJumpButtonPressed = false;

    //States
    private bool _isGrounded;
    private bool _isRunning;
    private bool _isActiveRagdoll;

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

    //Syncing client ragdolls
    // TODO: change to sending bytes instead of Quaternion and limite how much the joints can rotate
    [Networked, Capacity(30)] public NetworkArray<Quaternion> NetworkPhysicsSyncedRotation { get; }

    private const float InputThreshold = 0.01f;

    [SerializeField] private float _animationSpeedDamp = 10f; // how fast animation blends
    private float _smoothedInputSpeed = 0f;

    float _startSlerpPositionSpring = 0.0f;

    private void Awake()
    {
        _activeRagdollMembers = GetComponentsInChildren<ActiveRagdollMember>();
        _initialJointRotation = _mainJoint.transform.localRotation;
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
            ApplyGravity();
        }


        // holds the target anim float
        float targetAnimSpeed = 0f;

        if (GetInput(out NetworkInputData networkInputData))
        {
            // movement calculation
            // if not sprinting, clamp input
            float inputMagnitude = networkInputData._movementInput.magnitude;

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

            HandleJump(networkInputData);
        }

        if (Object.HasStateAuthority)
        {
            _smoothedInputSpeed = Mathf.MoveTowards(_smoothedInputSpeed, targetAnimSpeed, Runner.DeltaTime * _animationSpeedDamp);

            UpdateAnimations(_smoothedInputSpeed);

            // TODO
            // check if it is falling too far below map
            if (transform.position.y < -10)
                _networkRb3D.Teleport(Vector3.zero, Quaternion.identity);
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
        // apply more gravity to make character less floaty
        if (!_isGrounded)
        {
            _rb.AddForce(Vector3.down * _gravity);
            return;
        }

        // ground floating spring
        RaycastHit hit = _raycastHits[0];

        // calculate dir of velocity relative to world
        Vector3 vel = _rb.linearVelocity;
        Vector3 rayDir = transform.up * -1f;

        // calculate how much ray is compressed compared to our target height
        float rayDirVel = Vector3.Dot(rayDir, vel);
        float relVel = rayDirVel;

        float currentHeight = Vector3.Distance(_rb.position, hit.point);
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
        if (_isGrounded && networkInputData._isJumpPressed)
        {
            _rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
            _isJumpButtonPressed = false; //reset immediately
        }
    }

    private void UpdateAnimations(float animationValue)
    {
        _animator.SetFloat("MovementSpeed", animationValue);

        // update joints rotation based on animation
        for (int i = 0; i < _activeRagdollMembers.Length; i++)
        {
            _activeRagdollMembers[i].UpdateJointFromAnimation();
            NetworkPhysicsSyncedRotation.Set(i, _activeRagdollMembers[i].transform.localRotation);
        }
    }

    //void OnCollisionEnter(Collision other)
    //{
    //    MakeRagdoll();
    //}

    void MakeRagdoll()
    {
        if (!Object.HasStateAuthority)
            return;

        // update main joint
        JointDrive jointDrive = _mainJoint.slerpDrive;
        jointDrive.positionSpring = 0f;
        _mainJoint.slerpDrive = jointDrive;

        // update joints rotation and send them to clients
        for (int i = 0; i < _activeRagdollMembers.Length; i++)
        {
            _activeRagdollMembers[i].MakeRagdoll();
        }

        _isActiveRagdoll = false;
    }

    void MakeActiveRagdoll()
    {
        if (!Object.HasStateAuthority)
            return;

        // update main joint
        JointDrive jointDrive = _mainJoint.slerpDrive;
        jointDrive.positionSpring = _startSlerpPositionSpring;
        _mainJoint.slerpDrive = jointDrive;

        // update joints rotation and send them to clients
        for (int i = 0; i < _activeRagdollMembers.Length; i++)
        {
            _activeRagdollMembers[i].MakeActiveRagdoll();
        }

        _isActiveRagdoll = true;
    }

    // spawner calls this then transmit info to host
    public NetworkInputData GetNetworkInput()
    {
        NetworkInputData networkInputData = new NetworkInputData();

        // move data
        networkInputData._movementInput = _moveInputVector;
        networkInputData._isJumpPressed = _isJumpButtonPressed;
        networkInputData._isSprintPressed = _isRunning;

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
