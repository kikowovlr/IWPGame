using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// manages status effects for one player
/// </summary>
public class StatusEffectManager : NetworkBehaviour, IAffectedByStatusEffects
{
    [SerializeField] private PlayerVFXHandler _vfxHandler;
    [SerializeField] private List<StatusEffectSO> _effectDatabase = new List<StatusEffectSO>();
    private Dictionary<StatusEffectType, StatusEffectSO> _effectsDictionary;

    public const int MAX_EFFECTS = 4;
    // active effects on this current player, stored as a networked array for syncing across clients
    [Networked, Capacity(MAX_EFFECTS)] private NetworkArray<StatusEffectState> _activeEffects => default;
    private NetworkPlayerController _player;

    private void Awake()
    {
        _player = GetComponentInParent<NetworkPlayerController>();

        // map database for quick access
        _effectsDictionary = new Dictionary<StatusEffectType, StatusEffectSO>();
        foreach (var effect in _effectDatabase)
        {
            if (effect != null && !_effectsDictionary.ContainsKey(effect.Type))
                _effectsDictionary.Add(effect.Type, effect);
        }
    }

    public void ApplyEffect(StatusEffectType type, float customDuration = -1f)
    {
        if (!Object.HasStateAuthority) return;
        if (!_effectsDictionary.ContainsKey(type)) return;

        // set data
        float duration = customDuration > 0f ? customDuration : _effectsDictionary[type].DefaultDuration;
        TickTimer timer = TickTimer.CreateFromSeconds(Runner, duration);

        // check if effect alrdy exists
        // if so, refresh duration
        for (int i = 0; i < _activeEffects.Length; i++)
        {
            if (_activeEffects[i]._type == type)
            {
                var existing = _activeEffects[i];
                existing._remainingTime = timer;
                _activeEffects.Set(i, existing);
                return;
            }
        }

        // if not, add effect
        for (int i = 0; i < _activeEffects.Length; i++)
        {
            if (_activeEffects[i]._type == StatusEffectType.None)
            {
                StatusEffectState newState = new StatusEffectState { _type = type, _remainingTime = timer };
                _activeEffects.Set(i, newState);
                _effectsDictionary[type].OnEffectAdded(_player, ref newState);
                return;
            }
        }
    }

    /// <summary>
    /// !! call this from server/host to apply status
    /// dont use applyeffect for modularity
    /// </summary>
    public void InflictStatus(StatusEffectType type, float duration)
    {
        ApplyEffect(type, duration);
    }

    public override void FixedUpdateNetwork()
    {
        // clean up expired effects
        if (Object.HasStateAuthority)
        {
            for (int i = 0; i < _activeEffects.Length; i++)
            {
                if (_activeEffects[i]._type != StatusEffectType.None && !_activeEffects[i].IsActive(Runner))
                {
                    StatusEffectType removedType = _activeEffects[i]._type;
                    StatusEffectState stateCopy = _activeEffects[i];

                    if (_effectsDictionary.ContainsKey(removedType))
                        _effectsDictionary[removedType].OnEffectRemoved(_player, ref stateCopy);

                    _activeEffects.Set(i, default); // clear slot
                }
            }
        }

        // update active modifiers every network tick
        UpdateModifiers();
    }

    private void UpdateModifiers()
    {
        // todo: add speed modifier to player controller cs

        // loop through all slots and tick down continuous modifiers
        for (int i = 0; i < _activeEffects.Length; i++)
        {
            StatusEffectType currentType = _activeEffects[i]._type;

            if (currentType != StatusEffectType.None && _effectsDictionary.ContainsKey(currentType))
            {
                StatusEffectState state = _activeEffects[i];

                _effectsDictionary[currentType].ApplyTickModifiers(_player, ref state);
            }
        }
    }

    public override void Render()
    {
        // gather which types are active this render frame
        HashSet<StatusEffectType> activeTypes = new HashSet<StatusEffectType>();

        // loop through all active effects to get their types
        for (int i = 0; i < _activeEffects.Length; i++)
        {
            if (_activeEffects[i]._type != StatusEffectType.None && _activeEffects[i].IsActive(Runner))
                activeTypes.Add(_activeEffects[i]._type);
        }

        // pass to vfx handler and update active types
        if (_vfxHandler != null)
            _vfxHandler.SyncStatusVisualEffects(activeTypes, _effectsDictionary);
    }
}
