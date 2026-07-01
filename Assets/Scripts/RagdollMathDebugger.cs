using UnityEngine;

public class RagdollMathDebugger : MonoBehaviour
{
    public Transform animatedBone;
    public Transform animatedRoot;
    public Transform physicalRoot;

    private Quaternion _startLocalRotation;
    private Quaternion _worldRotationOffset;
    private bool _isSetupInEditor;

    [ContextMenu("Debug Math Breakdown")]
    public void LogMathBreakdown()
    {
        if (animatedBone == null || animatedRoot == null || physicalRoot == null || transform.parent == null)
        {
            Debug.LogError($"[{gameObject.name} Debug] Missing assignment references!");
            return;
        }

        // Pull setup data from your actual ActiveRagdollMember script on this bone
        ActiveRagdollMember memberScript = GetComponent<ActiveRagdollMember>();
        if (memberScript != null)
        {
            // We use reflection to grab the hidden variables for debugging
            var fields = typeof(ActiveRagdollMember).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            foreach (var f in fields)
            {
                if (f.Name == "_startLocalRotation") _startLocalRotation = (Quaternion)f.GetValue(memberScript);
                if (f.Name == "_worldRotationOffset") _worldRotationOffset = (Quaternion)f.GetValue(memberScript);
                if (f.Name == "_isSetupInEditor") _isSetupInEditor = (bool)f.GetValue(memberScript);
            }
        }

        // 1. Fetch the ConfigurableJoint component dynamically from this bone
        ConfigurableJoint joint = GetComponent<ConfigurableJoint>();

        // 2. Safely find the exact same physical anchor your member script uses
        Transform physicalAnchor = (joint != null && joint.connectedBody != null) ? joint.connectedBody.transform : transform.parent;

        // 3. REWRITTEN RUNTIME SIMULATION MATH: Mirrors your ActiveRagdollMember's exact update loop
        // Step A: Calculate where the animated rig bone wants to be (including structural offset)
        Quaternion targetWorldRotation = animatedBone.rotation * _worldRotationOffset;

        // Step B: Turn that target world rotation into the local space of the physical anchor (e.g. Spine2)
        Quaternion targetLocalRotation = Quaternion.Inverse(physicalAnchor.rotation) * targetWorldRotation;

        // Step C: Calculate current real local position relative to the SAME anchor instead of transform.parent
        Quaternion currentRotationRelativeToAnchor = Quaternion.Inverse(physicalAnchor.rotation) * transform.rotation;

        Debug.LogWarning($"=============================================\n" +
            $"[RAGDOLL MATH DEBUG] FOR {gameObject.name}\n" +
            $"Physics Anchor Detected : {physicalAnchor.name}\n" +
            $"Is Setup In Editor? : {_isSetupInEditor}\n" +
            $"---------------------------------------------\n" +
            $"Captured Start Local Rotation (Euler): {_startLocalRotation.eulerAngles}\n" +
            $"Current Real Anchor Rotation (Euler): {currentRotationRelativeToAnchor.eulerAngles}\n" +
            $"Calculated Joint TargetRotation (Euler): {targetLocalRotation.eulerAngles}\n" +
            $"=============================================");
    }
}