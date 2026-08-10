using Fusion;
using UnityEngine;

public class PlayerImpactAudio : NetworkBehaviour
{
    [Header("Impact Tuning")]
    [SerializeField] private LayerMask _impactLayers;          
    [SerializeField] private float _minImpactSpeed = 3f;       // below this = ignored (gentle contact)
    [SerializeField] private float _hardImpactSpeed = 12f;     // at/above this = full-strength slam
    [SerializeField] private float _cooldown = 0.15f;          // one slam = one sound (ragdoll hits many bones)
    [Range(0f, 1f)]
    [SerializeField] private float _volumeScale = 0.6f;

    [Header("Knockout")]
    [SerializeField] private bool _boostWhenKnockedOut = true; // limp-body slams feel heavier
    [SerializeField] private float _knockoutBoost = 0.2f;

    private NetworkPlayerController _controller;
    private PlayerCombatAudio _audio;
    private float _lastImpactTime;
    private float _suppressImpactsUntil; // to suppress impact audios shortly after teleports

    [Networked] private byte _impactStrengthQuantized { get; set; }
    [Networked, OnChangedRender(nameof(OnImpactSoundChanged))] private byte _impactTick { get; set; }

    private void Awake()
    {
        PlayerComponentRegistry registry = transform.root.GetComponent<PlayerComponentRegistry>();
        if (registry != null)
        {
            _controller = registry.Controller;
            _audio = registry.CombatAudio;
        }
    }

    public void ReportImpact(Collision other)
    {
        if (Time.time < _suppressImpactsUntil)
            return;

        // layer gate
        if ((_impactLayers.value & (1 << other.gameObject.layer)) == 0)
            return;

        // impact speed
        float impactSpeed = other.relativeVelocity.magnitude;
        if (impactSpeed < _minImpactSpeed)
            return;

        // avoid firing many at once
        if (Time.time - _lastImpactTime < _cooldown) return; // shared debounce across ALL bones
        _lastImpactTime = Time.time;

        // 0..1 strength from impact speed
        float strength = Mathf.InverseLerp(_minImpactSpeed, _hardImpactSpeed, impactSpeed);

        // limp bodies slam harder-feeling
        if (_boostWhenKnockedOut && _controller != null && _controller.IsKnockedOut)
            strength = Mathf.Clamp01(strength + _knockoutBoost);

        // replicate to all clients
        _impactStrengthQuantized = (byte)(Mathf.Clamp01(strength) * 255f);
        _impactTick++;
    }

    // runs on every client (and host)
    private void OnImpactSoundChanged()
    {
        if (_audio == null) return;
        float strength = _impactStrengthQuantized / 255f;
        _audio.PlayImpact(SoundID.BodyImpact, strength, _volumeScale);
    }

    public void SuppressImpacts(float duration = 0.3f)
    {
        _suppressImpactsUntil = Time.time + duration;
    }

}
