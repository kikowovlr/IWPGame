using Fusion;
using UnityEngine;

public class PlayerAbilityAudioNetworked : NetworkBehaviour
{
    private PlayerAbilityAudio _abilityAudio;

    [Networked] private SoundID _abilitySound { get; set; }
    [Networked] private SoundID _abilitySoundLayer { get; set; }
    [Networked, OnChangedRender(nameof(OnAbilityOneShotChanged))] private byte AbilitySoundTick { get; set; }

    [Networked] private SoundID _abilityLoopSound { get; set; }
    [Networked] private NetworkBool _abilityLoopActive { get; set; }
    [Networked, OnChangedRender(nameof(OnAbilityLoopChanged))] private byte AbilityLoopTick { get; set; }

    private void Awake()
    {
        PlayerComponentRegistry registry = transform.root.GetComponent<PlayerComponentRegistry>();
        if (registry != null)
        {
            _abilityAudio = registry.AbilityAudio;
        }
    }

    // required to play 2 audio clips on one network tick
    // if u dont do this, local player can hear btoh clips but other players cant
    public void PlayAbilityOneShot(SoundID id, SoundID layer = SoundID.None)
    {
        if (!Object.HasStateAuthority) return;
        _abilitySound = id;
        _abilitySoundLayer = layer;
        AbilitySoundTick++;
    }

    public void SetAbilityLoop(SoundID id, bool active)
    {
        if (!Object.HasStateAuthority) return;
        _abilityLoopSound = id;
        _abilityLoopActive = active;
        AbilityLoopTick++;
    }

    private void OnAbilityOneShotChanged()
    {
        if (_abilityAudio == null) return;
        _abilityAudio.PlayOneShot(_abilitySound, _abilitySoundLayer);
    }

    private void OnAbilityLoopChanged()
    {
        if (_abilityAudio == null) return;
        if (_abilityLoopActive) _abilityAudio.StartLoop(_abilityLoopSound);
        else _abilityAudio.StopLoop();
    }
}
