using UnityEngine;

public class HoveringCrown : MonoBehaviour
{
    [SerializeField] private float _heightOffset = 0.5f; // above head
    [SerializeField] private float _hoverAmplitude = 0.1f;
    [SerializeField] private float _hoverFrequency = 1.5f;
    [SerializeField] private float _spinSpeed = 30f;

    private Transform _headTarget;
    private float _bobTimer;

    private void LateUpdate()   // after animation/physics, so it tracks the final head pose
    {
        if (_headTarget == null) return;

        _bobTimer += Time.deltaTime;
        float bob = Mathf.Sin(_bobTimer * _hoverFrequency) * _hoverAmplitude;

        // follow head position, offset upward + bob, but keep upright
        Vector3 pos = _headTarget.position + Vector3.up * (_heightOffset + bob);
        transform.position = pos;

        // stay level (upright), slow spin around Y
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y + _spinSpeed * Time.deltaTime, 0f);
    }

    public void SetTarget(Transform head) => _headTarget = head;
}
