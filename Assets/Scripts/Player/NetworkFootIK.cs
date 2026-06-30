using UnityEngine;

public class NetworkFootIK : MonoBehaviour
{
    [Header("IK Targets")]
    [SerializeField] private Transform _leftFootTarget;
    [SerializeField] private Transform _rightFootTarget;

    [Header("Original Bones on Animated Model")]
    [SerializeField] private Transform _leftFootBone;
    [SerializeField] private Transform _rightFootBone;

    [Header("Settings")]
    [SerializeField] private LayerMask _environmentLayer;
    [SerializeField] private float _raycastDist = 1f;
    [SerializeField] private float _footHeightOffset = 0.05f; // offset to stop clipping into ground
    [SerializeField] private float _ikSmoothSpeed = 15f;
    private Quaternion _leftFootInitialRotationOffset;
    private Quaternion _rightFootInitialRotationOffset;

    [Header("Debug Settings")]
    [SerializeField] private bool _enableConsoleFlooding = true;
    // for gizmos only
    private Vector3 _leftRayStart, _leftRayEnd, _leftHitPoint;
    private Vector3 _rightRayStart, _rightRayEnd, _rightHitPoint;
    private bool _leftHit, _rightHit;

    private void Awake()
    {
        if (_leftFootBone != null)
            _leftFootInitialRotationOffset = Quaternion.Inverse(transform.rotation) * _leftFootBone.rotation;

        if (_rightFootBone != null)
            _rightFootInitialRotationOffset = Quaternion.Inverse(transform.rotation) * _rightFootBone.rotation;
    }

    // runs after Animator component
    private void LateUpdate()
    {
        AdjustFootTarget("RIGHT", _rightFootBone, _rightFootTarget, _rightFootInitialRotationOffset, ref _rightRayStart, ref _rightRayEnd, ref _rightHitPoint, ref _rightHit);
        AdjustFootTarget("LEFT", _leftFootBone, _leftFootTarget, _leftFootInitialRotationOffset, ref _leftRayStart, ref _leftRayEnd, ref _leftHitPoint, ref _leftHit);
    }

    // checks the floor depth relative to the active animation cycle and locks the IK constraint to the ground
    private void AdjustFootTarget(string footName, Transform footBone, Transform ikTarget, Quaternion initialOffset, ref Vector3 debugStart, ref Vector3 debugEnd, ref Vector3 debugHit, ref bool debugDidHit)
    {
        // start raycast slightly above current animation position of the foot
        Vector3 rayOrigin = footBone.position + (Vector3.up * 0.1f);
        Vector3 rayDir = Vector3.down;

        debugStart = rayOrigin;
        debugEnd = rayOrigin + (rayDir * _raycastDist);

        // raycast straight down from the moving animation step coordinate
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hitInfo, _raycastDist, _environmentLayer))
        {
            debugDidHit = true;
            debugHit = hitInfo.point;

            // ground detected - extract hit point from world collision array
            Vector3 targetPos = hitInfo.point + (Vector3.up * _footHeightOffset);

            // blend ik target pos to detected pos
            ikTarget.position = Vector3.Lerp(ikTarget.position, targetPos, Time.deltaTime * _ikSmoothSpeed);

            //// slope alignment - TODO: delete if looks bad with actual model
            //Quaternion groundRotation = Quaternion.FromToRotation(Vector3.up, hitInfo.normal) * footBone.parent.rotation;
            //ikTarget.rotation = Quaternion.Slerp(ikTarget.rotation, groundRotation, Time.deltaTime * _ikSmoothSpeed);

            Quaternion slopeTilt = Quaternion.FromToRotation(Vector3.up, hitInfo.normal);
            Quaternion finalTargetRotation = slopeTilt * transform.rotation * initialOffset;
            ikTarget.rotation = Quaternion.Slerp(ikTarget.rotation, finalTargetRotation, Time.deltaTime * _ikSmoothSpeed);

            if (_enableConsoleFlooding)
            {
                // Core check: Is the actual bone following the green target?
                float distanceToTarget = Vector3.Distance(footBone.position, ikTarget.position);
                float angleToTarget = Quaternion.Angle(footBone.rotation, ikTarget.rotation);

                Debug.Log($"[IK Debug] {footName} Grounded | " +
                          $"Target Pos: {ikTarget.position.ToString("F2")} | " +
                          $"Bone Pos: {footBone.position.ToString("F2")} | " +
                          $"Distance Error: {distanceToTarget.ToString("F3")}m | " +
                          $"Angle Error: {angleToTarget.ToString("F1")}°");

                if (distanceToTarget > 0.1f)
                {
                    Debug.LogWarning($"[IK Debug] {footName} BONE IS NOT FOLLOWING TARGET! Your Two-Bone IK Constraint component might be broken, turned off, or evaluated out of order.");
                }
            }
        }
        else
        {
            debugDidHit = false;
            // airborne fallback
            ikTarget.position = Vector3.Lerp(ikTarget.position, footBone.position, Time.deltaTime * _ikSmoothSpeed);
            ikTarget.rotation = Quaternion.Lerp(ikTarget.rotation, footBone.rotation, Time.deltaTime * _ikSmoothSpeed);

            if (_enableConsoleFlooding)
            {
                Debug.LogWarning($"[IK Debug] {footName} AIRBORNE (Raycast missed completely). Check your raycast distance or _environmentLayer setup.");
            }
        }
    }

    private void OnDrawGizmos()
    {
        // Only draw if the game is running and references exist
        if (!Application.isPlaying || _leftFootBone == null || _rightFootBone == null) return;

        // --- LEFT FOOT GIZMOS ---
        Gizmos.color = _leftHit ? Color.cyan : Color.red;
        Gizmos.DrawLine(_leftRayStart, _leftHit ? _leftHitPoint : _leftRayEnd); // Draw the laser line

        if (_leftHit)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_leftHitPoint, 0.03f); // Draw a small ring exactly where it impacts the ground

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(_leftFootTarget.position, new Vector3(0.06f, 0.02f, 0.06f)); // Draw a small box showing where the IK target is floating
        }

        // --- RIGHT FOOT GIZMOS ---
        Gizmos.color = _rightHit ? Color.cyan : Color.red;
        Gizmos.DrawLine(_rightRayStart, _rightHit ? _rightHitPoint : _rightRayEnd); // Draw the laser line

        if (_rightHit)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_rightHitPoint, 0.03f); // Draw a small ring exactly where it impacts the ground

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(_rightFootTarget.position, new Vector3(0.06f, 0.02f, 0.06f)); // Draw a small box showing where the IK target is floating
        }
    }
}
