using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Mixer")]
    [SerializeField] private AudioMixer _mixer;
    [SerializeField] private AudioMixerGroup _bgmGroup;
    [SerializeField] private AudioMixerGroup _sfxGroup;
    private const string MIXER_MASTER = "MasterVolume";
    private const string MIXER_BGM = "BGMVolume";
    private const string MIXER_SFX = "SFXVolume";
    private const string MIXER_MUFFLE = "MuffleCutoff";
    private const string MIXER_DUCK = "DuckVolume";
    private const string PREFS_MASTER = "vol_master";
    private const string PREFS_BGM = "vol_bgm";
    private const string PREFS_SFX = "vol_sfx";

    [SerializeField] private float _muffleMinCutoff = 900f;    // fully muffled (eliminated)
    [SerializeField] private float _muffleMaxCutoff = 22000f;  // fully clear (normal)
    [SerializeField] private float _duckMaxAttenuation = -8f; // dB reduction at full elimination

    [Header("BGM")]
    [SerializeField] private MusicLibrary _musicLibrary;
    [SerializeField] private AudioSource _bgmSourceA;
    [SerializeField] private AudioSource _bgmSourceB;
    [SerializeField] private float _defaultCrossfadeTime = 1.5f;
    [SerializeField] private float _bgmMasterVolume = 0.6f;   // global BGM ceiling -> per-track volume multiplies this

    [Header("SFX")]
    [SerializeField] private SoundLibrary _soundLibrary;
    [SerializeField] private int _sfxPoolSize = 12; // so multiple sfx can play at once

    private AudioSource _activeBgm;
    private AudioSource _inactiveBgm;
    private Coroutine _crossfadeRoutine;
    private MusicID _currentMusic = MusicID.None;

    private readonly List<AudioSource> _sfxPool = new List<AudioSource>();
    private int _sfxIndex;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (_soundLibrary != null) _soundLibrary.Init();
        if (_musicLibrary != null) _musicLibrary.Init();

        _activeBgm = _bgmSourceA;
        _inactiveBgm = _bgmSourceB;
        SetupBgmSource(_bgmSourceA);
        SetupBgmSource(_bgmSourceB);

        InitSfxPool();
        LoadSavedVolumes();
    }

    private void SetupBgmSource(AudioSource src)
    {
        if (src == null) return;

        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        if (_bgmGroup != null) 
            src.outputAudioMixerGroup = _bgmGroup;
    }

    private void InitSfxPool()
    {
        for (int i = 0; i < _sfxPoolSize; i++)
        {
            AudioSource src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            if (_sfxGroup != null) src.outputAudioMixerGroup = _sfxGroup;
            _sfxPool.Add(src);
        }
    }

    #region BGM

    /// <summary>
    /// plays music with tunable crossfading
    /// </summary>
    public void PlayMusic(MusicID id, float fadeTime = -1f)
    {
        if (id == _currentMusic) return;

        if (id == MusicID.None)
        {
            StopMusic(fadeTime < 0 ? _defaultCrossfadeTime : fadeTime);
            return;
        }

        if (_musicLibrary == null || !_musicLibrary.TryGet(id, out MusicEntry entry) || entry.clip == null)
            return;

        _currentMusic = id;
        if (fadeTime < 0f) 
            fadeTime = _defaultCrossfadeTime;

        float targetVol = _bgmMasterVolume * (entry.volume <= 0f ? 1f : entry.volume);

        if (_crossfadeRoutine != null) StopCoroutine(_crossfadeRoutine);
        _crossfadeRoutine = StartCoroutine(CrossfadeRoutine(entry.clip, targetVol, fadeTime));
    }

    private IEnumerator CrossfadeRoutine(AudioClip newClip, float targetVol, float fadeTime)
    {
        // play new bgm
        _inactiveBgm.clip = newClip;
        _inactiveBgm.volume = 0f;
        _inactiveBgm.Play();

        // crossfade -> lower current bgm, increase new bgm
        float t = 0f;
        float startActiveVol = _activeBgm.isPlaying ? _activeBgm.volume : 0f;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            float k = t / fadeTime;
            _activeBgm.volume = Mathf.Lerp(startActiveVol, 0f, k);
            _inactiveBgm.volume = Mathf.Lerp(0f, targetVol, k);
            yield return null;
        }

        // stop old bgm
        _activeBgm.Stop();
        _activeBgm.volume = 0f;
        _inactiveBgm.volume = targetVol;

        (_activeBgm, _inactiveBgm) = (_inactiveBgm, _activeBgm); // tuple swap -> swaps values without needing third value as a median
        _crossfadeRoutine = null;
    }
    
    /// <summary>
    /// to stop bgm with fading without playing another bgm
    /// </summary>
    public void StopMusic(float fadeTime = 1f)
    {
        _currentMusic = MusicID.None;
        if (_crossfadeRoutine != null) StopCoroutine(_crossfadeRoutine);
        _crossfadeRoutine = StartCoroutine(FadeOutRoutine(fadeTime));
    }

    private IEnumerator FadeOutRoutine(float fadeTime)
    {
        float startVol = _activeBgm.volume;
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            _activeBgm.volume = Mathf.Lerp(startVol, 0f, t / fadeTime);
            yield return null;
        }
        _activeBgm.Stop();
        _crossfadeRoutine = null;
    }

    #endregion

    #region SFX
        
    public void PlaySFX(SoundID id)
    {
        if (_soundLibrary == null || !_soundLibrary.TryGet(id, out SoundEntry entry)) return;
        if (entry.clips == null || entry.clips.Length == 0) return;

        // adjust clip to soundid settings
        AudioClip clip = entry.clips[UnityEngine.Random.Range(0, entry.clips.Length)];
        float pitch = entry.pitchRange == Vector2.zero ? 1f : UnityEngine.Random.Range(entry.pitchRange.x, entry.pitchRange.y);
        float vol = entry.volume <= 0f ? 1f : entry.volume;

        // plays sfx
        AudioSource src = _sfxPool[_sfxIndex];
        _sfxIndex = (_sfxIndex + 1) % _sfxPool.Count;
        src.pitch = pitch;
        src.PlayOneShot(clip, vol);
    }

    /// <summary>
    /// instantiates new game object with audio source in run time to play sound at a specific position
    /// </summary>
    public void PlaySFXAtPosition(SoundID id, Vector3 position)
    {
        if (_soundLibrary == null || !_soundLibrary.TryGet(id, out SoundEntry entry)) return;
        if (entry.clips == null || entry.clips.Length == 0) return;

        AudioClip clip = entry.clips[UnityEngine.Random.Range(0, entry.clips.Length)];
        float vol = entry.volume <= 0f ? 1f : entry.volume;

        GameObject go = new GameObject($"SFX_{id}");
        go.transform.position = position;
        AudioSource src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.spatialBlend = 1f; // needed for spatial audio (audio w positioning)
        src.volume = vol;
        if (_sfxGroup != null) src.outputAudioMixerGroup = _sfxGroup;
        src.Play();
        Destroy(go, clip.length + 0.1f); // + buffer
    }

    /// <summary>
    /// Plays a SoundID on a caller-owned AudioSource so the caller can Stop()/loop it
    /// Use for positional, interruptible sounds (island crumble, ability loops on world objects, etc)
    /// </summary>
    public void PlayOnSource(SoundID id, AudioSource src, bool loop = false)
    {
        if (src == null || _soundLibrary == null) return;
        if (!_soundLibrary.TryGet(id, out SoundEntry entry)) return;
        if (entry.clips == null || entry.clips.Length == 0) return;

        src.clip = entry.clips[Random.Range(0, entry.clips.Length)];
        src.volume = entry.volume <= 0f ? 1f : entry.volume;
        src.pitch = entry.pitchRange == Vector2.zero
            ? 1f : Random.Range(entry.pitchRange.x, entry.pitchRange.y);
        src.loop = loop;
        src.Play();
    }

    #endregion

    #region Volume

    public void SetMasterVolume(float v) 
    { 
        SetMixerVolume(MIXER_MASTER, v); 
        PlayerPrefs.SetFloat(PREFS_MASTER, v); 
    
    }
    public void SetBGMVolume(float v) 
    { 
        SetMixerVolume(MIXER_BGM, v); 
        PlayerPrefs.SetFloat(PREFS_BGM, v); 
    }

    public void SetSFXVolume(float v) 
    { 
        SetMixerVolume(MIXER_SFX, v); 
        PlayerPrefs.SetFloat(PREFS_SFX, v); 
    }

    private void SetMixerVolume(string param, float v01)
    {
        if (_mixer == null) return;
        float dB = v01 <= 0.0001f ? -80f : Mathf.Log10(Mathf.Clamp01(v01)) * 20f;
        _mixer.SetFloat(param, dB);
    }

    private void LoadSavedVolumes()
    {
        SetMasterVolume(PlayerPrefs.GetFloat(PREFS_MASTER, 1f));
        SetBGMVolume(PlayerPrefs.GetFloat(PREFS_BGM, 1f));
        SetSFXVolume(PlayerPrefs.GetFloat(PREFS_SFX, 1f));
    }

    // saved-value getters so the sliders can initialise to the right position
    public float GetSavedMaster() => PlayerPrefs.GetFloat(PREFS_MASTER, 1f);
    public float GetSavedBGM() => PlayerPrefs.GetFloat(PREFS_BGM, 1f);
    public float GetSavedSFX() => PlayerPrefs.GetFloat(PREFS_SFX, 1f);

    #endregion

    #region MIXER EFFECTS

    /// <summary>0 = normal (clear), 1 = fully muffled.</summary>
    public void SetMuffle(float amount01)
    {
        if (_mixer == null) return;
        amount01 = Mathf.Clamp01(amount01);
        // lerp in log space -> frequency perception is logarithmic
        float t = Mathf.Clamp01(amount01);
        float cutoff = Mathf.Lerp(Mathf.Log10(_muffleMaxCutoff), Mathf.Log10(_muffleMinCutoff), t);
        cutoff = Mathf.Pow(10f, cutoff);
        _mixer.SetFloat(MIXER_MUFFLE, cutoff);
    }

    /// <summary>0 = normal, 1 = fully ducked.</summary>
    public void SetDuck(float amount01)
    {
        if (_mixer == null) return;
        amount01 = Mathf.Clamp01(amount01);
        float dB = Mathf.Lerp(0f, _duckMaxAttenuation, amount01); // 0dB -> -8dB
        _mixer.SetFloat(MIXER_DUCK, dB);
    }

    #endregion
}
