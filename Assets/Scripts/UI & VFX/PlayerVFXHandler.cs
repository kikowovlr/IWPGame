using UnityEngine;
using System.Collections.Generic;

public class PlayerVFXHandler : MonoBehaviour
{
    private PlayerVFXAnchors _anchors;

    // store all active vfx related to status effects
    private Dictionary<StatusEffectType, List<GameObject>> _spawnedEffectsMap = new Dictionary<StatusEffectType, List<GameObject>>();

    private void Awake()
    {
        _anchors = GetComponent<PlayerVFXAnchors>();
    }

    public void SyncVisualEffects(HashSet<StatusEffectType> activeTypes, Dictionary<StatusEffectType , StatusEffectSO> database)
    {

    }
}
