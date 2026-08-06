using UnityEngine;

public interface IDamageable
{
    void Rpc_TakeDamage(float damageAmount, Vector3 impactForce, Vector3 impactPoint, string hitBoneName);
}
