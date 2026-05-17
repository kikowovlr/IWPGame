using UnityEngine;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine.InputSystem;

public class NetworkPlayerController : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody _rb;
    [SerializeField] private ConfigurableJoint _mainJoint;
    [SerializeField] private Animator _animator;

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

    //Raycasts
    private readonly RaycastHit[] _raycastHits = new RaycastHit[10];

    //Syncing of ragdoll parts
    ActiveRagdollMember[] _activeRagdollMembers;
    private Quaternion _initialJointRotation;

    private const float InputThreshold = 0.01f;

    private void Awake()
    {
        _activeRagdollMembers = GetComponentsInChildren<ActiveRagdollMember>();
        _initialJointRotation = _mainJoint.transform.localRotation;
    }

    // runs before Start fn
    public override void OnStartClient()
    {
        base.OnStartClient();
        if (base.IsOwner)
        {
            PlayerRegistry.RegisterLocalPlayerTransform(_rb.transform);
        }
        else
        {
            // if not owner then disable player controller - dont control other players
            if (TryGetComponent<PlayerInput>(out var playerInput))
            {
                playerInput.enabled = false;
            }
            enabled = false;
        }
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

    private void FixedUpdate()
    {
        CheckForGround();
        ApplyGravity();

        // movement calculation
        // if not sprinting, clamp input
        Vector2 finalInput = _moveInputVector;
        if (!_isRunning && finalInput.magnitude > InputThreshold)
            finalInput = finalInput.normalized * _walkInputScale;

        float inputMagnitude = finalInput.magnitude;
        Vector3 localVelocityVsForward = transform.forward * Vector3.Dot(transform.forward, _rb.linearVelocity);
        float localForwardVelocity = localVelocityVsForward.magnitude;

        if (inputMagnitude > InputThreshold)
        {
            Vector3 moveDir = CalculateMoveDirection();

            HandleRotation(moveDir);

            if (localForwardVelocity < _maxSpeed * inputMagnitude)
            {
                // move character in the dir they're facing
                _rb.AddForce(moveDir * _movementForce);
            }
        }

        HandleJump();
        UpdateAnimations(localForwardVelocity);
    }

    private void CheckForGround()
    {
        // assume we are not grounded
        _isGrounded = false;

        // check if we are grounded
        int numOfHits = Physics.SphereCastNonAlloc(_rb.position, _groundCheckRadius, transform.up * -1, _raycastHits, _groundCheckDist);

        // check for valid results
        for (int i = 0; i < numOfHits; i++)
        {
            //ignore self hits
            if (_raycastHits[i].transform.root == transform)
                continue;

            _isGrounded = true;
            break;
        }
    }

    private void ApplyGravity()
    {
        // apply more gravity to make character less floaty
        if (!_isGrounded)
            _rb.AddForce(Vector3.down * _gravity);
    }

    private Vector3 CalculateMoveDirection()
    {
        // get cam dir vectors and flatten y
        Vector3 camForward = Camera.main.transform.forward;
        Vector3 camRight = Camera.main.transform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        // movement dir vector based on cam's POV
        return (camForward * _moveInputVector.y) + (camRight * _moveInputVector.x);
    }

    private void HandleRotation(Vector3 moveDir)
    {
        // based on camera dir
        Quaternion desiredWorldRotation = Quaternion.LookRotation(moveDir, transform.up);
        Quaternion jointSpaceRotation = Quaternion.Inverse(desiredWorldRotation) * _initialJointRotation;

        // rotate towards target dir
        _mainJoint.targetRotation = Quaternion.RotateTowards(_mainJoint.targetRotation, jointSpaceRotation, Time.fixedDeltaTime * _rotationSpeed);
    }

    private void HandleJump()
    {
        if (_isGrounded && _isJumpButtonPressed)
        {
            _rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
            _isJumpButtonPressed = false; //reset immediately
        }
    }

    private void UpdateAnimations(float forwardVelocity)
    {
        // calculate speed ratio
        //float dynamicAnimSpeed = forwardVelocity / _maxSpeed;
        //dynamicAnimSpeed = Mathf.Clamp(dynamicAnimSpeed, 0f, 1.5f);

        _animator.SetFloat("MovementSpeed", forwardVelocity / _maxSpeed);
        //_animator.SetFloat("MovementSpeed", forwardVelocity * _animationSpeedScale);

        // update joints rotation based on animation
        for (int i = 0; i < _activeRagdollMembers.Length; i++)
        {
            _activeRagdollMembers[i].UpdateJointFromAnimation();
        }
    }
}
