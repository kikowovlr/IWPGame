using UnityEngine;

/// <summary>
/// -- SLOWNESS -- 
/// Speed reduction 
/// Causes: goo(slowed as long as inside goo)
/// After leaving slowness source/slowness wears off, speed gradually comes back (lerp)
/// </summary>
[CreateAssetMenu(fileName = "Effect_Slowness", menuName = "Status Effects/Slowness")]
public class SlownessEffectSO : StatusEffectSO
{
    [Range(0f, 1f)] private float _speedMultiplier = 0.5f; 

    public override void OnEffectAdded(NetworkPlayerController player, ref StatusEffectState state)
    {
    }

    public override void ApplyTickModifiers(NetworkPlayerController player, ref StatusEffectState state)
    {

    }

    public override void OnEffectRemoved(NetworkPlayerController player, ref StatusEffectState state)
    {
    }
}
