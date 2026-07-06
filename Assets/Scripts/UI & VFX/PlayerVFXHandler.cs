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

    public void SyncStatusVisualEffects(HashSet<StatusEffectType> activeTypes, Dictionary<StatusEffectType , StatusEffectSO> database)
    {
        // clean up effects that are not active
        List<StatusEffectType> keysToRemove = new List<StatusEffectType>();
       
        // loop thru all active effects
        foreach(var activeEffectType in _spawnedEffectsMap.Keys)
        {
            // compare current active types to spawned effect types, if mismatch = effect ended = remove vfx
            if (!activeTypes.Contains(activeEffectType))
            {
                // destroy the vfx obj in the effect type map
                foreach(var vfxObj in _spawnedEffectsMap[activeEffectType])
                {
                    if (vfxObj != null)
                        Destroy(vfxObj);
                }
                keysToRemove.Add(activeEffectType);
            }
        }

        // remove keys (effect types) from the dictionary => inactive
        foreach(var key in keysToRemove)
            _spawnedEffectsMap.Remove(key);

        // spawn fresh vfx objs for newly added status effects
        foreach(var currentType in activeTypes)
        {
            if (_spawnedEffectsMap.ContainsKey(currentType)) continue; // vfx for this status alrdy playing
            if (!database.TryGetValue(currentType, out var effectSO)) continue;

            // track list of vfx added to add to dictionary 
            List<GameObject> activeInstances = new List<GameObject>();

            // loop thru all vfx containers to be added for this status
            foreach(var container in effectSO.VisualContainers)
            {
                if (container._prefab == null) continue;

                Transform targetAnchor = _anchors.GetAnchorTransform(container._anchorType);
                if (targetAnchor == null) continue;

                // instantiate and parent to anchor
                GameObject vfxInstance = Instantiate(container._prefab, targetAnchor);
                vfxInstance.transform.localPosition = container._localPositionOffset;
                vfxInstance.transform.localRotation = Quaternion.Euler(container._localRotationOffset);

                activeInstances.Add(vfxInstance);
            }

            // add list of vfx to dictionary according to key
            _spawnedEffectsMap.Add(currentType, activeInstances);
        }
    }
}
