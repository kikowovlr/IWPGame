using UnityEngine;
using UnityEngine.UI;

public class AudioSettingsPanel : MonoBehaviour
{
    [SerializeField] private Slider _masterSlider;
    [SerializeField] private Slider _bgmSlider;
    [SerializeField] private Slider _sfxSlider;

    private void OnEnable()
    {
        var sm = SoundManager.Instance;
        if (sm == null) return;

        // set sliders to saved values WITHOUT firing the onValueChanged callback
        if (_masterSlider != null) _masterSlider.SetValueWithoutNotify(sm.GetSavedMaster());
        if (_bgmSlider != null) _bgmSlider.SetValueWithoutNotify(sm.GetSavedBGM());
        if (_sfxSlider != null) _sfxSlider.SetValueWithoutNotify(sm.GetSavedSFX());
    }

    private void Awake()
    {
        if (_masterSlider != null) _masterSlider.onValueChanged.AddListener(OnMasterChanged);
        if (_bgmSlider != null) _bgmSlider.onValueChanged.AddListener(OnBgmChanged);
        if (_sfxSlider != null) _sfxSlider.onValueChanged.AddListener(OnSfxChanged);
    }

    private void OnMasterChanged(float v) => SoundManager.Instance?.SetMasterVolume(v);
    private void OnBgmChanged(float v) => SoundManager.Instance?.SetBGMVolume(v);
    private void OnSfxChanged(float v) => SoundManager.Instance?.SetSFXVolume(v);
}
