using System.Collections.Generic;
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
public abstract class StatusEffectSO : ScriptableObject
{
    [SerializeField] protected StatusEffectType _type;
    [SerializeField] protected string _effectName;
    [SerializeField] protected float _defaultDuration = 3f;

    [Header("Modifiers")]
    [Range(0f, 1f)] protected float _movementSpeedModifier = 1f;

    [Header("Visuals")]
    [SerializeField] protected List<VFXContainer> _visualContainers;

    // getters
    public StatusEffectType Type => _type;
    public float DefaultDuration => _defaultDuration;
    public List<VFXContainer> VisualContainers => _visualContainers;

    public abstract void OnEffectAdded(NetworkPlayerController player, ref StatusEffectState state);
    public abstract void ApplyTickModifiers(NetworkPlayerController player, ref StatusEffectState state); // override in subclasses to apply specific modifiers EVERY TICK
    public abstract void OnEffectRemoved(NetworkPlayerController player, ref StatusEffectState state);
}
