using UnityEngine;

public class PlayerCombatAudio : MonoBehaviour
{
    [SerializeField] private AudioSource _source;
    [SerializeField] private AudioSource _footstepSource; // max distance less
    [SerializeField] private SoundLibrary _library;
    [SerializeField] private float _runSpeedThreshold = 4f;

    [Header("Run Dust")]
    [SerializeField] private ParticleSystem _runDust;
    [SerializeField] private int _dustMin = 2;
    [SerializeField] private int _dustMax = 5;

    private PlayerComponentRegistry _registry;

    private void Awake()
    {
        if (_source == null) _source = transform.root.GetComponent<AudioSource>();
        if (_library != null) _library.Init();
        _registry = transform.root.GetComponent<PlayerComponentRegistry>();
    }

    /// <summary>
    /// Plays a combat sound FROM this player's position (3D), on whatever client calls it
    /// </summary>
    public void PlaySound(SoundID id)
    {
        if (_library == null || _source == null) return;
        if (!_library.TryGet(id, out SoundEntry entry) || entry.clips == null || entry.clips.Length == 0) return;

        AudioClip clip = entry.clips[Random.Range(0, entry.clips.Length)];
        _source.pitch = entry.pitchRange == Vector2.zero ? 1f : Random.Range(entry.pitchRange.x, entry.pitchRange.y);
        _source.PlayOneShot(clip, entry.volume <= 0f ? 1f : entry.volume);
    }

    public void UnityEvent_OnFootstep()
    {
        if (_registry.CombatAudio == null) return;

        float speed = _registry.Controller.Animator.GetFloat("MovementSpeed");

        // check goo state
        bool inGoo = _registry.Goo != null && _registry.Goo.IsInGooSource;

        SoundID id;
        if (inGoo)
            id = SoundID.GooFootstep;
        else
            id = speed >= _runSpeedThreshold ? SoundID.FootstepRun : SoundID.FootstepWalk;

        _registry.CombatAudio.PlaySound(id);

        // dust burst on this footplant
        if (_runDust != null && !inGoo) // only spawn if not in goo
        {
            if (speed >= _runSpeedThreshold && _registry.Controller.IsGroundedNetworked)
            {
                int count = Mathf.RoundToInt(Mathf.Lerp(_dustMin, _dustMax, speed)); // more puffs at higher speed
                _runDust.Emit(Mathf.Max(1, count));
            }
        }
    }

    /// <summary>
    /// Plays a body-slam impact, scaling volume + pitch by impact strength (0 -> 1)
    /// Harder slam = louder + lower pitch. Picks a random clip from the entry.
    /// </summary>
    public void PlayImpact(SoundID id, float strength01, float volumeScale = 1f)
    {
        if (_library == null || _source == null) return;
        if (!_library.TryGet(id, out SoundEntry entry) || entry.clips == null || entry.clips.Length == 0) return;

        strength01 = Mathf.Clamp01(strength01);

        // random clip for variation
        AudioClip clip = entry.clips[Random.Range(0, entry.clips.Length)];

        // harder = louder
        float entryVol = entry.volume <= 0f ? 1f : entry.volume;
        float vol = Mathf.Lerp(0.3f, 1f, strength01) * entryVol * volumeScale;

        // harder = lower pitch, with a little random wobble so repeats differ
        float basePitch = Mathf.Lerp(1.1f, 0.85f, strength01);
        float pitch = basePitch + Random.Range(-0.03f, 0.03f);

        _source.pitch = pitch;
        _source.PlayOneShot(clip, vol);
    }
}
