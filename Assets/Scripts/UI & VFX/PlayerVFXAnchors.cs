using UnityEngine;

public enum VFXAnchorType { Head, LeftEye, RightEye, Chest }

[System.Serializable]
public struct VFXContainer
{
    public GameObject _prefab;
    public VFXAnchorType _anchorType;
    public Vector3 _localOffset;
}

public class PlayerVFXAnchors : MonoBehaviour
{
    [field: SerializeField] public Transform Head {  get; private set; }
    [field: SerializeField] public Transform LeftEye { get; private set; }
    [field: SerializeField] public Transform RightEye { get; private set; }

    // helper to connect anchor type to their corresponding bone, keeps logic inside this component
    public Transform GetAnchor(VFXAnchorType anchorType)
    {
        return anchorType switch
        {
            VFXAnchorType.Head => Head,
            VFXAnchorType.LeftEye => LeftEye,
            VFXAnchorType.RightEye => RightEye,
            _ => transform, //default case
        };
    }
}
