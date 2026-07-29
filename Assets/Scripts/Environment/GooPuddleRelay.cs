using UnityEngine;

/// <summary>
/// sits on same GO as puddle colliders
/// forwards trigger callback to actual goopuddle logic in parent script
/// </summary>
public class GooPuddleRelay : MonoBehaviour
{
    private GooPuddle _puddle;

    private void Awake()
    {
        _puddle = GetComponentInParent<GooPuddle>();
    }

    private void OnTriggerStay(Collider other)
    {
        if (_puddle != null)
            _puddle.HandleTriggerStay(other);
    }
}
