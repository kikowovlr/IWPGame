using UnityEngine;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine.InputSystem;

public class NetworkPlayerControllerBackup : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody _rb;
    [SerializeField] private ConfigurableJoint _mainJoint;

    [Header("Movement Settings")]
    [SerializeField] private float _speed = 150f;
    [SerializeField] private float _runSpeedMultiplier = 1.5f;
    [SerializeField] private Transform _leftFoot;
    [SerializeField] private Transform _rightFoot;
    [SerializeField] private float _footGroundCheckDist = 0.3f;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private float _jumpForce = 10f;
    [SerializeField] private float _airControlMultiplier = 0.3f;
    [SerializeField] private float _jumpTimeout = 0.15f; // time to ignore ground after jumping
    private float _jumpTimeoutDelta;


    [Header("Balancing")]
    [SerializeField] private float _uprightStiffness = 1000f; // strength of pull
    [SerializeField] private float _uprightDamping = 100f; // prevents wobbling

    [SerializeField] private float _cameraYOffset = 0.4f;
    private Camera _playerCamera;
    private bool _isGrounded;
    private Vector2 _inputVector;
    private bool _isRunning;
    private bool _jumpRequested;

    // runs before Start fn
    public override void OnStartClient()
    {
        base.OnStartClient();
        if (base.IsOwner)
        {
            _playerCamera = Camera.main;
            _playerCamera.transform.position = new Vector3(transform.position.x, transform.position.y + _cameraYOffset, transform.position.z);
            _playerCamera.transform.SetParent(transform);
        }
        else
        {
            // if not owner then disable player controller - dont control other players
            gameObject.GetComponent<NetworkPlayerControllerBackup>().enabled = false;
        }
    }

    // called by player input component (message: OnMove)
    public void OnMove(InputValue value)
    {
        _inputVector = value.Get<Vector2>();
    }

    public void OnSprint(InputValue value)
    {
        _isRunning = value.isPressed;
    }

    public void OnJump(InputValue value)
    {
        if (value.isPressed)
            _jumpRequested = true;
    }

    private void FixedUpdate()
    {
        CheckGrounded();
        Move();
        //ApplyUprightForce();

        if (_jumpRequested)
            HandleJump();
    }

    private void Move()
    {
        // calculate dir based on orientation
        Vector3 moveDir = transform.forward * _inputVector.y + transform.right * _inputVector.x;
        moveDir.Normalize();

        // determine final speed
        float currSpeed = _isRunning ? _speed * _runSpeedMultiplier : _speed;

        if (!_isGrounded)
        {
            currSpeed *= _airControlMultiplier;
        }

        // apply force to hips
        if (moveDir.magnitude > 0.1f)
        {
            _rb.AddForce(currSpeed * moveDir, ForceMode.Acceleration);
        }
    }

    private void ApplyUprightForce()
    {
        // determine what is upright
        Quaternion targetRotation = Quaternion.identity;

        // rotation diff
        Quaternion rotationError = targetRotation * Quaternion.Inverse(_rb.rotation); // basically subtracting but for rotation so need to multiply instead to find diff
        rotationError.ToAngleAxis(out float angle, out Vector3 axis);

        if (angle > 180f) angle -= 360f; // normalize to 180f to -180f

        // convert rotation error to torque
        if (Mathf.Abs(angle) > 0.01f)
        {
            // PID formula - (stiffness * error) - (damping * currVelocity)
            Vector3 torque = (axis * angle * _uprightStiffness) - (_rb.angularVelocity * _uprightDamping);
            _rb.AddTorque(torque, ForceMode.Acceleration);
        }
    }


    private void CheckGrounded()
    {
        if (_jumpTimeoutDelta > 0)
        {
            _jumpTimeoutDelta -= Time.fixedDeltaTime;
            _isGrounded = false;
            return;
        }

        // check if either leg is touching floor
        bool leftGrounded = Physics.Raycast(_leftFoot.position, Vector3.down, _footGroundCheckDist, _groundLayer);
        bool rightGrounded = Physics.Raycast(_rightFoot.position, Vector3.down, _footGroundCheckDist, _groundLayer);

        _isGrounded = leftGrounded || rightGrounded;
        Debug.DrawRay(_leftFoot.position, Vector3.down * _footGroundCheckDist, leftGrounded ? Color.green : Color.red);
        Debug.DrawRay(_rightFoot.position, Vector3.down * _footGroundCheckDist, rightGrounded ? Color.green : Color.red);
    }

    private void HandleJump()
    {
        if (!_isGrounded)
        {
            _jumpRequested = false;
            return;
        }

        _jumpTimeoutDelta = _jumpTimeout;

        // reset vertical velocity 
        _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);

        _rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
        _jumpRequested = false;
    }
}
