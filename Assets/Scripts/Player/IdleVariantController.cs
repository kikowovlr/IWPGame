using UnityEngine;

public class IdleVariantController : MonoBehaviour
{
    [SerializeField] private float _minInterval = 4f;
    [SerializeField] private float _maxInterval = 9f;
    [SerializeField] private float _idleSpeedThreshold = 0.05f; // below this = idle

    private Animator _animator;
    private float _timer;
    private float _nextTime;

    private void OnEnable() => ScheduleNext();

    private void Awake()
    {
        if (_animator == null) _animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (_animator == null) return;

        // only count time towards a variant while genuinely idle
        float moveSpeed = _animator.GetFloat("MovementSpeed");
        bool isIdle = moveSpeed <= _idleSpeedThreshold;

        if (!isIdle)
        {
            // moving -> reset so variants never fire mid-move
            _timer = 0f;
            return;
        }

        _timer += Time.deltaTime;
        if (_timer >= _nextTime)
        {
            PlayRandomVariant();
            ScheduleNext();
        }
    }

    private void ScheduleNext()
    {
        _timer = 0f;
        _nextTime = Random.Range(_minInterval, _maxInterval);
    }

    private void PlayRandomVariant()
    {
        if (Random.value < 0.5f)
            _animator.SetTrigger("PlayIdleVariant1");
        else
            _animator.SetTrigger("PlayIdleVariant2");
    }
}
