using UnityEngine;

public enum VFXAnchorType { Head, LeftEye, RightEye, Chest }

[System.Serializable]
public struct VFXContainer
{
    public GameObject _prefab;
    public VFXAnchorType _anchorType;
    public Vector3 _localPositionOffset;
    public Vector3 _localRotationOffset;
}

public class PlayerVFXAnchors : MonoBehaviour
{
    public Transform Head {  get; private set; }
    public Transform LeftEye { get; private set; }
    public Transform RightEye { get; private set; }

    // helper to connect anchor type to their corresponding bone, keeps logic inside this component
    public Transform GetAnchorTransform(VFXAnchorType anchorType)
    {
        return anchorType switch
        {
            VFXAnchorType.Head => Head,
            VFXAnchorType.LeftEye => LeftEye,
            VFXAnchorType.RightEye => RightEye,
            _ => transform, //default case
        };
    }

    // helper to set up anchors (call when changing character
    public void SetUpAnchors(Transform head, Transform leftEye, Transform rightEye)
    {
        Head = head;
        LeftEye = leftEye;
        RightEye = rightEye;
    }
}
