using UnityEngine;

/// <summary>
/// holds fixed numbers of abilities
/// </summary>
//[CreateAssetMenu(fileName = "NewAbility", menuName = "Characters/Abilities/AbilitySO")]
public abstract class AbilitySO : ScriptableObject
{
    public string _name;
    public float _baseCooldown;
    public Sprite _icon;

    [Header("Animation Settings")]
    [SerializeField] protected string _skillTrigger = "SkillTrigger";
    [SerializeField] protected string _releaseTrigger = "SkillRelease";
    [SerializeField] protected string _activeBool = "IsSkillActive";
    [SerializeField] protected string _skillTypeString = "SkillType";
    [SerializeField] protected int _skillType = 0; // 0 = instant, 1 = charge


    // called on server/host during FixedUpdateNetwork ticks
    public abstract void OnTickPressed(NetworkPlayerController player, ref AbilityState state, Vector2 aimDir); // ref - pass by reference
    public abstract void OnTickHeld(NetworkPlayerController player, ref AbilityState state, Vector2 aimDir); 
    public abstract void OnTickReleased(NetworkPlayerController player, ref AbilityState state, Vector2 aimDir); 

    /// <summary>
    /// override this in subclasses if ability needs to execute hit code when animation event triggers
    /// </summary>
    /// <param name="player"></param>
    public virtual void OnAnimationImpactTriggered(NetworkPlayerController player)
    {
    }

    public virtual float HandleIncomingDamageCheck(NetworkPlayerController player, ref AbilityState state, float rawDamage)
    {
        return rawDamage;
    }
}
