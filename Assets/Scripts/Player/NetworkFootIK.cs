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

    // runs after Animator component
    private void LateUpdate()
    {
        AdjustFootTarget(_rightFootBone, _rightFootTarget);
        AdjustFootTarget(_leftFootBone, _leftFootTarget);
    }

    // checks the floor depth relative to the active animation cycle and locks the IK constraint to the ground
    private void AdjustFootTarget(Transform originalBone, Transform ikTarget)
    {
        Vector3 rayOrigin = originalBone.position + Vector3.up * 0.1f;
        Debug.DrawRay(rayOrigin, Vector3.down * _raycastDist, Color.blue);

        // raycast straight down from the moving animation step coordinate
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hitInfo, _raycastDist, _environmentLayer))
        {
            Debug.Log($"[FootIK] Successfully hitting: {hitInfo.transform.name} on layer {LayerMask.LayerToName(hitInfo.transform.gameObject.layer)}");
            // ground detected - extract hit point from world collision array
            Vector3 targetPos = hitInfo.point;

            targetPos.y += _footHeightOffset;

            // snapping instead of lerping for now
            ikTarget.position = targetPos;

            // slope alignment - TODO: delete if looks bad with actual model
            //Quaternion groundRotation = Quaternion.FromToRotation(Vector3.up, hitInfo.normal) * transform.rotation;
            //ikTarget.rotation = groundRotation;
        }
        else
        {
            Debug.LogWarning($"[FootIK] Raycast missed everything! Check your Environment Layer Mask. Checked distance: {_raycastDist}");
            // airborne fallback
            ikTarget.position = originalBone.position;
            ikTarget.rotation = originalBone.rotation;
        }
    }
}
