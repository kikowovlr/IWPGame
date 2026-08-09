using UnityEngine;

// lightweight script that goes on each impact-relevant collider GO (torso main, head)
public class ImpactColliderRelay : MonoBehaviour
{
    private PlayerImpactAudio _coordinator;

    private void Awake()
    {
        PlayerComponentRegistry registry = transform.root.GetComponent<PlayerComponentRegistry>();
        if (registry != null)
            _coordinator = registry.ImpactAudio;
    }

    private void OnCollisionEnter(Collision other)
    {
        if (_coordinator != null)
            _coordinator.ReportImpact(other);
    }
}
