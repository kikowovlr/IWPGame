using Fusion;
using UnityEngine;
using System.Collections.Generic;



/// <summary>
/// manages status effects for one player
/// </summary>
public class StatusEffectManager : NetworkBehaviour
{
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

    /// <summary>
    /// !! call this from server/host
    /// </summary>
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
                _activeEffects.Set(i, new StatusEffectState { _type = type, _remainingTime = timer });
                return;
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        // clean up expired effects
        if (Object.HasStateAuthority)
        {
            for (int i = 0; i < _activeEffects.Length; i++)
            {
                if (_activeEffects[i]._type != StatusEffectType.None && _activeEffects[i].IsActive(Runner))
                {
                    _activeEffects.Set(i, default); // clear slot
                }
            }
        }

        UpdateModifiers();
    }

    private void UpdateModifiers()
    {

    }
}
