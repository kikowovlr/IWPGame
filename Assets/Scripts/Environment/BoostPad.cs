using Fusion;
using UnityEngine;
using System.Collections.Generic;

public class BoostPad : NetworkBehaviour
{
    [Header("Launch Settings")]
    [SerializeField] private float _forwardSpeed = 12f;
    [SerializeField] private float _upwardSpeed = 8f;
    [SerializeField] private float _boostDuration = 0.8f;

    [SerializeField] private float _cooldownPerPlayer = 1.0f;

    private readonly Dictionary<PlayerBoost, float> _lastBoostTime = new Dictionary<PlayerBoost, float>();

    // fires on all clients when the pad launches someone
    [Networked, OnChangedRender(nameof(OnLaunchChanged))] private byte _launchTick { get; set; }

    private void OnTriggerEnter(Collider other)
    {
        if (!Object.HasStateAuthority) return;

        if (!other.transform.root.TryGetComponent<PlayerComponentRegistry>(out var registry))
            return;

        PlayerBoost boost = registry.Boost;
        if (boost == null) return;

        // check if cooldown has passed
        float now = Runner.SimulationTime;
        if (_lastBoostTime.TryGetValue(boost, out float last) && now - last < _cooldownPerPlayer)
            return;

        _lastBoostTime[boost] = now;

        boost.ApplyBoost(transform.forward, _forwardSpeed, _upwardSpeed, _boostDuration);

        _launchTick++;

        // tell the tutorial this player hit a boost pad
        if (registry.Controller != null)
            TutorialManager.Instance?.NotifyPlayerAction(registry.Controller.Object.InputAuthority, TutorialActionType.Environment);
    }

    private void OnLaunchChanged()
    {
        SoundManager.Instance?.PlaySFXAtPosition(SoundID.BoostLaunch, transform.position);
    }
}
