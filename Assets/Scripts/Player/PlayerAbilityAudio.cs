using UnityEngine;

public class PlayerAbilityAudio : MonoBehaviour
{
    [SerializeField] private AudioSource _oneShotSource; // 3D, like combat: PlayOneShot mixes overlaps
    [SerializeField] private AudioSource _loopSource;    // 3D, loop=true, playOnAwake=false
    [SerializeField] private SoundLibrary _library;

    private void Awake()
    {
        if (_library != null) _library.Init();
    }


    /// <summary>
    /// Play one or two layers at once (both on the same source, mixed via PlayOneShot)
    /// </summary>
    public void PlayOneShot(SoundID id, SoundID layer = SoundID.None)
    {
        PlayLayer(id);
        if (layer != SoundID.None) PlayLayer(layer);
    }

    private void PlayLayer(SoundID id)
    {
        if (_library == null || _oneShotSource == null) return;
        if (!_library.TryGet(id, out SoundEntry entry) || entry.clips == null || entry.clips.Length == 0) return;
        AudioClip clip = entry.clips[Random.Range(0, entry.clips.Length)];
        _oneShotSource.pitch = entry.pitchRange == Vector2.zero ? 1f : Random.Range(entry.pitchRange.x, entry.pitchRange.y);
        _oneShotSource.PlayOneShot(clip, entry.volume <= 0f ? 1f : entry.volume);
    }


    public void StartLoop(SoundID id)
    {
        if (_library == null || _loopSource == null) return;
        if (!_library.TryGet(id, out SoundEntry entry) || entry.clips == null || entry.clips.Length == 0) return;
        AudioClip clip = entry.clips[Random.Range(0, entry.clips.Length)];
        if (_loopSource.isPlaying && _loopSource.clip == clip) return; // already looping this
        _loopSource.clip = clip;
        _loopSource.pitch = entry.pitchRange == Vector2.zero ? 1f : Random.Range(entry.pitchRange.x, entry.pitchRange.y);
        _loopSource.volume = entry.volume <= 0f ? 1f : entry.volume;
        _loopSource.loop = true;
        _loopSource.Play();
    }

    public void StopLoop()
    {
        if (_loopSource != null && _loopSource.isPlaying) _loopSource.Stop();
    }
}
