using UnityEngine;

public enum StatusEffectType : byte // store as byte for optimisation -> holds nums from 0 to 255
{
    None = 0,
    Stunned = 1,
    Slowness = 2,
    Poisoned = 3,
    PartiallyBlinded = 4,
}

[CreateAssetMenu(fileName = "NewEffect", menuName = "Combat/New Status Effect")]
public class StatusEffectSO : ScriptableObject
{
    protected StatusEffectType _type;
    protected string _effectName;
    protected float _defaultDuration = 3f;

    [Header("Modifiers")]
    [Range(0f, 1f)] protected float _movementSpeedModifier = 1f;
    public bool _blockActions = false; // blocks input, movement, and rotation

    // getters
    public StatusEffectType Type => _type;
    public float DefaultDuration => _defaultDuration;
}
