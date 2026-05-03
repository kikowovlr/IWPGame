using UnityEngine;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine.InputSystem;

public class PlayerController : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _orientationPivot;
    [SerializeField] private Rigidbody _hips;

    [Header("Movement Settings")]
    [SerializeField] private float _speed = 150f; // forward + back
    [SerializeField] private float _strafeSpeed = 100f; // left + right
    [SerializeField] private float _runSpeedMultiplier = 1.5f;
    [SerializeField] private float _jumpForce;

    [SerializeField] private float _cameraYOffset = 0.4f;
    private Camera _playerCamera;
    private bool _isGrounded;
    private Vector2 _inputVector;
    private bool _isRunning;

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
            gameObject.GetComponent<PlayerController>().enabled = false;
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

    private void FixedUpdate()
    {
        Move();
    }

    private void Move()
    {
        // calculate dir based on orientation
        Vector3 moveDir = _orientationPivot.forward * _inputVector.y + _orientationPivot.right * _inputVector.x;
        moveDir.Normalize();

        // determine final speed
        float currSpeed = _isRunning ? _speed * _runSpeedMultiplier : _speed;

        // apply force to hips
        if (moveDir.magnitude > 0.1f)
        {
            _hips.AddForce(currSpeed * moveDir, ForceMode.Acceleration);
        }
    }
}
