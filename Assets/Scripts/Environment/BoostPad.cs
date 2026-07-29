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

    private void OnTriggerEnter(Collider other)
    {
        if (!Object.HasStateAuthority) return;

        PlayerComponentRegistry registry = other.transform.root.GetComponent<PlayerComponentRegistry>();
        PlayerBoost boost = registry.Boost;
        if (boost == null) return;

        // check if cooldown has passed
        float now = Runner.SimulationTime;
        if (_lastBoostTime.TryGetValue(boost, out float last) && now - last < _cooldownPerPlayer)
            return;

        _lastBoostTime[boost] = now;

        boost.ApplyBoost(transform.forward, _forwardSpeed, _upwardSpeed, _boostDuration);
    }
}
