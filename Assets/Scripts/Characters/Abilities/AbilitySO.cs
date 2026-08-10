using Unity.VisualScripting;
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
    [TextArea(3, 6)] public string _description;

    [Header("Animation Settings")]
    [SerializeField] protected string _skillTrigger = "SkillTrigger";
    [SerializeField] protected string _releaseTrigger = "SkillRelease";
    [SerializeField] protected string _activeBool = "IsSkillActive";
    [SerializeField] protected string _skillTypeString = "SkillType";
    [SerializeField] protected int _skillType = 0; // 0 = instant, 1 = charge
    public bool BlockAllCombatInputs = true;
    public bool BlockJumping = true;
    public bool BlockSprinting = true;

    // indicators
    [SerializeField] protected IndicatorDataSO _indicatorData;
    public IndicatorDataSO IndicatorData => _indicatorData;

    // called on server/host during FixedUpdateNetwork ticks
    public abstract void OnTickPressed(NetworkPlayerController player, ref AbilityState state, Vector2 aimDir); // ref - pass by reference
    public abstract void OnTickHeld(NetworkPlayerController player, ref AbilityState state, Vector2 aimDir); 
    public abstract void OnTickReleased(NetworkPlayerController player, ref AbilityState state, Vector2 aimDir);
    public abstract void UpdateAbilityState(NetworkPlayerController player, ref AbilityState state);

    /// <summary>
    /// override this in subclasses if ability needs to execute hit code when animation event triggers
    /// </summary>
    /// <param name="player"></param>
    public virtual void OnAnimationImpactTriggered(NetworkPlayerController player)
    {
    }

    public virtual void OnAnimationEndTriggered(NetworkPlayerController player)
    {
    }

    public virtual float HandleIncomingDamageCheck(NetworkPlayerController player, ref AbilityState state, float rawDamage)
    {
        return rawDamage;
    }

    public abstract void InitIndicatorVisual(AbilityIndicatorController indicator);

    public virtual void ForceCancel(NetworkPlayerController player, ref AbilityState state)
    {
        if (!player.Object.HasStateAuthority) return;

        state._isCharging = false;
        state._isDashing = false;
        state._isCasting = false;
        state._isVisualShown = false;
        state._customVelocity = Vector3.zero;
        state._hitCount = 0;
    }
}
