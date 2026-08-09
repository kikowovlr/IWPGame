using UnityEngine;

public class IslandBreakWarningIndicator : MonoBehaviour
{
    [SerializeField] private Transform _exclamation;

    [Header("Bounce")]
    [SerializeField] private float _bounceHeight = 0.4f;
    [SerializeField] private float _bounceSpeed = 4f;
    [SerializeField] private float _baseHeight = 1.0f;

    private Vector3 _baseLocalPos;

    private void Start()
    {
        if (_exclamation != null) 
            _baseLocalPos = _exclamation.localPosition;
    }

    private void LateUpdate()
    {
        if (_exclamation == null) return;

        // bounce
        float yOffset = Mathf.Abs(Mathf.Sin(Time.time * _bounceSpeed)) * _bounceHeight;
        Vector3 pos = _baseLocalPos;
        pos.y = _baseHeight + yOffset;
        _exclamation.localPosition = pos;
    }
}
