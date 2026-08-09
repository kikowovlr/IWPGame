using UnityEngine;
using System.Collections.Generic;

public enum SoundID
{
    None,
    // UI
    ButtonHover,
    ButtonClickGeneric,
    ButtonClickConfirm,
    ButtonClickCancel,
    ButtonClickPositive,   // long positive
    ButtonClickNegative,   // long negative

    // Combat
    HitPunch,
    HitStrongPunch,
    HitKick,
    HitHeadbutt,
    ThrowRelease,
    // whiffs
    WhiffLight, // punch/strong punch miss
    WhiffHeavy, // kick/headbutt miss
    // knockout
    Knockout,
    Oof,

    // Abilities
    // goat
    GoatChargeStompStart,
    GoatChargeSnortStart,
    GoatChargeRumbleLoop,
    GoatRamScream,
    GoatRamGallopLoop,
    GoatRamHitThud,
    GoatRamHitBoing,
    // monkey
    MonkeyScream,
    MonkeySoundwave,
    // red panda
    RedPandaStunShimmer,
    RedPandaChitter,

    // status effects
    StunnedBirdsChirping,

    // Movement
    FootstepWalk,
    FootstepRun,
    BodyImpact,
    GooFootstep,

    // Environment
    BoostLaunch,
    IslandCrumble,
    IslandBreak,
    IslandCrashWater,

    // Flow
    CountdownBeep,
    CountdownGo,
    CharacterSelectCountdown,
    Victory,
    Defeat  
}

[System.Serializable]
public struct SoundEntry
{
    public SoundID id;
    public AudioClip[] clips;   // multiple clips = random variation
    [Range(0f, 1f)] public float volume;
    public Vector2 pitchRange;  // random pitch range e.g. (0.95, 1.05)
}

[CreateAssetMenu(fileName = "SoundLibrary", menuName = "Audio/Sound Library")]
public class SoundLibrary : ScriptableObject
{
    public SoundEntry[] entries;

    private Dictionary<SoundID, SoundEntry> _map;

    public void Init()
    {
        _map = new Dictionary<SoundID, SoundEntry>();
        foreach (var entry in entries)
        {
            if (!_map.ContainsKey(entry.id))
                _map.Add(entry.id, entry);
        }
    }

    public bool TryGet(SoundID id, out SoundEntry entry)
    {
        if (_map == null)
            Init();
        
        return _map.TryGetValue(id, out entry);
    }
}