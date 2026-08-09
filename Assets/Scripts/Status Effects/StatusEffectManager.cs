using Fusion;
using System.Collections.Generic;
using UnityEngine;

public struct ActiveEffectInfo
{
    public StatusEffectType Type;
    public Sprite Icon;
    public float RemainingSeconds; // <= 0 if not resolvable
    public bool IsSustained;       // sustained (slowness) vs timed (stun)
}

/// <summary>
/// manages status effects for one player
/// </summary>
public class StatusEffectManager : NetworkBehaviour, IAffectedByStatusEffects
{
    public const int MAX_EFFECTS = 4;

    [SerializeField] private PlayerVFXHandler _vfxHandler;
    [SerializeField] private List<StatusEffectSO> _effectDatabase = new List<StatusEffectSO>();

    // active effects on this current player, stored as a networked array for syncing across clients
    [Networked, Capacity(MAX_EFFECTS)] private NetworkArray<StatusEffectState> _activeEffects => default;

    private readonly HashSet<StatusEffectType> _prevActiveTypes = new HashSet<StatusEffectType>();
    private Dictionary<StatusEffectType, StatusEffectSO> _effectsDictionary;
    private NetworkPlayerController _player;

    public bool SuppressVisuals { get; set; }

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
        // if any system switches this bool, clear all vfx
        if (SuppressVisuals)
        {
            _vfxHandler?.SyncStatusVisualEffects(new HashSet<StatusEffectType>(), _effectsDictionary);
            HandleStatusAudioTransitions(new HashSet<StatusEffectType>()); // clear audio 
            return;
        }

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

        HandleStatusAudioTransitions(activeTypes);
    }

    private void HandleStatusAudioTransitions(HashSet<StatusEffectType> activeTypes)
    {
        var audio = _player != null ? _player.Registry.AbilityAudio : null;
        if (audio == null) 
        { 
            _prevActiveTypes.Clear(); 
            _prevActiveTypes.UnionWith(activeTypes);  // sync
            return; 
        }

        // newly ADDED effects this frame
        foreach (var type in activeTypes)
        {
            if (!_prevActiveTypes.Contains(type))
                OnStatusEffectStarted(type, audio);
        }

        // newly REMOVED effects this frame
        foreach (var type in _prevActiveTypes)
        {
            if (!activeTypes.Contains(type))
                OnStatusEffectEnded(type, audio);
        }

        _prevActiveTypes.Clear();
        _prevActiveTypes.UnionWith(activeTypes);
    }

    private void OnStatusEffectStarted(StatusEffectType type, PlayerAbilityAudio audio)
    {
        switch (type)
        {
            case StatusEffectType.Stunned:
                audio.StartLoop(SoundID.StunnedBirdsChirping);
                break;
            case StatusEffectType.Poisoned:
                break;
        }
    }

    private void OnStatusEffectEnded(StatusEffectType type, PlayerAbilityAudio audio)
    {
        switch (type)
        {
            case StatusEffectType.Stunned:
                audio.StopLoop();
                break;
        }
    }

    public List<ActiveEffectInfo> GetActiveEffectsForUI()
    {
        var list = new List<ActiveEffectInfo>();

        for (int i = 0; i < _activeEffects.Length; i++)
        {
            StatusEffectType type = _activeEffects[i]._type;
            if (type == StatusEffectType.None) continue;
            if (!_activeEffects[i].IsActive(Runner)) continue;
            if (!_effectsDictionary.ContainsKey(type)) continue;

            StatusEffectSO so = _effectsDictionary[type];
            float remaining = _activeEffects[i]._remainingTime.RemainingTime(Runner) ?? 0f;

            list.Add(new ActiveEffectInfo
            {
                Type = type,
                Icon = so.Icon,
                RemainingSeconds = remaining,
                IsSustained = so.IsSustained
            });
        }

        return list;
    }
}
