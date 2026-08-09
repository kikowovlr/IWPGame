using System.Collections.Generic;
using UnityEngine;

public enum MusicID
{
    None,
    Menu,
    CharacterSelect,
    Gameplay,
    SuddenDeath,
    Podium,
    Tutorial,
}

[System.Serializable]
public struct MusicEntry
{
    public MusicID id;
    public AudioClip clip;
    [Range(0f, 1f)] public float volume;
}


[CreateAssetMenu(fileName = "MusicLibrary", menuName = "Audio/Music Library")]
public class MusicLibrary : ScriptableObject
{
    public MusicEntry[] entries;

    private Dictionary<MusicID, MusicEntry> _map;

    public void Init()
    {
        _map = new Dictionary<MusicID, MusicEntry>();
        foreach (var e in entries)
            if (!_map.ContainsKey(e.id))
                _map.Add(e.id, e);
    }

    public bool TryGet(MusicID id, out MusicEntry entry)
    {
        if (_map == null) Init();
        return _map.TryGetValue(id, out entry);
    }
}
