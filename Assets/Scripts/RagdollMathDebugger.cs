using UnityEngine;

public class RagdollMathDebugger : MonoBehaviour
{
    public Transform animatedBone;
    public Transform animatedRoot;
    public Transform physicalRoot;

    // Add these so we can see if your pristine bind-pose is the problem
    private Quaternion _startLocalRotation;
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
                if (f.Name == "_isSetupInEditor") _isSetupInEditor = (bool)f.GetValue(memberScript);
            }
        }

        // 1. Calculate how the animated bone is rotated relative to its main character root
        Quaternion animatedRootRelative = Quaternion.Inverse(animatedRoot.rotation) * animatedBone.rotation;

        // 2. Convert that root-relative target back into the local space of this specific joint's parent
        Quaternion targetLocalRotation = Quaternion.Inverse(transform.parent.rotation) * (physicalRoot.rotation * animatedRootRelative);

        // 3. Simulate the ConfigurableJoint Extension Math (This is what actually goes into the joint!)
        Quaternion rightToLeftJointSpace = Quaternion.Inverse(targetLocalRotation) * _startLocalRotation;

        Debug.LogWarning($"=============================================\n" +
            $"[RAGDOLL MATH DEBUG] FOR {gameObject.name}\n" +
            $"Is Setup In Editor? : {_isSetupInEditor}\n" +
            $"Captured Start Local Rotation (Euler): {_startLocalRotation.eulerAngles}\n" +
            $"Current Real Local Rotation (Euler): {transform.localRotation.eulerAngles}\n" +
            $"Calculated Joint TargetRotation (Euler): {rightToLeftJointSpace.eulerAngles}\n" +
            $"=============================================");
    }
}