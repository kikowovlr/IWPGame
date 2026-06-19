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

    // called on server/host during FixedUpdateNetwork ticks
    public abstract void OnTickPressed(NetworkPlayerController player, ref AbilityState state, Vector2 aimDir); // ref - pass by reference
    public abstract void OnTickHeld(NetworkPlayerController player, ref AbilityState state, Vector2 aimDir); 
    public abstract void OnTickReleased(NetworkPlayerController player, ref AbilityState state, Vector2 aimDir); 
    
}
