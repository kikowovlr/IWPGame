using UnityEngine;

public class TestGooTrigger : MonoBehaviour
{
    // use heartbeat to continue inflicting the status as long as in goo
    // 0.1s serves as a small buffer so the status doesnt flicker due to network ticks
    private const float HeartbeatDuration = 0.1f;

    private void OnTriggerStay(Collider other)
    {
        var player = other.GetComponentInParent<NetworkPlayerController>();
            
        if (player != null && player.Registry != null && player.Registry.Status != null)
        {
            if (player.Object != null && player.Object.HasStateAuthority)
            {
                // constantly refresh slowness duration 
                player.Registry.Status.InflictStatus(StatusEffectType.Slowness, HeartbeatDuration);
            }
        }
    }
}
