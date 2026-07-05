using UnityEngine;
using UnityEngine.Splines.ExtrusionShapes;

/// <summary>
/// -- STUNNED -- 
/// 3D Swirly eyes attached to eyes rig
/// Freeze input, movement/Ragdoll/Knockout without minusing a heart
/// Optional: 3D circle of stars above head
/// </summary>
[CreateAssetMenu(fileName = "Effect_Stunned", menuName = "Status Effects/Stunned")]
public class StunEffectSO : StatusEffectSO
{
    public override void OnEffectAdded(NetworkPlayerController player, ref StatusEffectState state)
    {
        player.Knockout();
    }

    public override void ApplyTickModifiers(NetworkPlayerController player, ref StatusEffectState state)
    {
    }

    public override void OnEffectRemoved(NetworkPlayerController player, ref StatusEffectState state)
    {
        player.Recover();
    }
}
