using UnityEngine;

public class IgnoreCollision : MonoBehaviour
{
    [SerializeField] private Collider _thisCollider;
    [SerializeField] private Collider[] _collidersToIgnore;

    private void Start()
    {
        foreach(Collider otherCollider in _collidersToIgnore)
        {
            Physics.IgnoreCollision(_thisCollider, otherCollider, true);
        }
    }
}
