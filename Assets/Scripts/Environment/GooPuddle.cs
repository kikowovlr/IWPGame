using UnityEngine;

public class GooPuddle : MonoBehaviour
{
    // use heartbeat to continue inflicting the status as long as in goo
    // 0.1s serves as a small buffer so the status doesnt flicker due to network ticks
    private const float HeartbeatDuration = 0.1f;
    [SerializeField] private float _puddleGooRate = 0.55f;

    private void OnTriggerStay(Collider other)
    {
        if (other.TryGetComponent(out PlayerComponentRegistry registry))
        {
            if (registry.Controller.Object != null && registry.Controller.Object.HasStateAuthority)
            {
                registry.Goo.ApplyExposure(_puddleGooRate);
                // constantly refresh slowness duration 
                registry.Status.InflictStatus(StatusEffectType.Slowness, HeartbeatDuration);
            }
        }
    }
}
