using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// shows status effects icon for local player updated in real time
/// pools icon items under a horizontal layout group
/// 
/// timed effects (e.g. stun) -> blink + fade when remaining time drops below _blinkThreshold
/// sustained effects (e.g. slowness) -> show icon until effect is removed
/// </summary>
public class PlayerStatusEffectUIController : MonoBehaviour
{
    [System.Serializable]
    private class EffectIcon
    {
        public GameObject root;
        public Image image;
        public StatusEffectType type;
    }

    [SerializeField] private Transform _iconContainer; // has horizontal layout group
    [SerializeField] private Image _iconPrefab; // simple Image prefab (resize accordingly)
    [SerializeField] private int _poolSize = 4; // max number of icons to show at once
    [SerializeField] private float _refreshInterval = 0.15f; // how often to refresh the UI

    [Header("Blinking")]
    [SerializeField] private float _blinkThreshold = 1.0f; // start blinking when remaining time is below this threshold
    [SerializeField] private float _blinkSpeed = 6f;
    [SerializeField] private float _minBlinkAlpha = 0.25f;

    private StatusEffectManager _status;
    private readonly List<EffectIcon> _pool = new List<EffectIcon>();
    private float _refreshTimer;
    private List<ActiveEffectInfo> _current = new List<ActiveEffectInfo>();

    private void Awake()
    {
        // build pool
        for (int i = 0; i < _poolSize; i++)
        {
            Image img = Instantiate(_iconPrefab, _iconContainer);
            var item = new EffectIcon { root = img.gameObject, image = img, type = StatusEffectType.None };
            item.root.SetActive(false);
            _pool.Add(item);
        }
    }

    private void OnEnable()
    {
        PlayerRegistry.OnLocalPlayerSpawned += HandleLocalPlayerSpawned;
        TryBindLocal();
    }

    private void OnDisable()
    {
        PlayerRegistry.OnLocalPlayerSpawned -= HandleLocalPlayerSpawned;
    }

    private void HandleLocalPlayerSpawned(Transform _) => TryBindLocal();

    private void TryBindLocal()
    {
        if (NetworkPlayerController.Local != null)
            _status = NetworkPlayerController.Local.Registry.StatusManager;
    }

    private void Update()
    {
        if (_status == null)
        {
            TryBindLocal();
            return;
        }

        // rebuild icon set at a modest cadence
        _refreshTimer += Time.deltaTime;
        if (_refreshTimer >= _refreshInterval)
        {
            _refreshTimer = 0f;
            RebuildIcons();
        }

        // blink handling every frame
        UpdateBlink();
    }

    private void RebuildIcons()
    {
        _current = _status.GetActiveEffectsForUI();

        for (int i = 0; i < _pool.Count; i++)
        {
            if (i < _current.Count)
            {
                _pool[i].root.SetActive(true);
                _pool[i].image.sprite = _current[i].Icon;
                _pool[i].type = _current[i].Type;
            }
            else
            {
                _pool[i].root.SetActive(false);
                _pool[i].type = StatusEffectType.None;
            }
        }
    }

    private void UpdateBlink()
    {
        for (int i = 0; i < _pool.Count; i++)
        {
            if (!_pool[i].root.activeSelf || i >= _current.Count) continue;

            ActiveEffectInfo info = _current[i];
            float alpha = 1f;

            // only timed effects blink near expiry
            if (!info.IsSustained && info.RemainingSeconds > 0f && info.RemainingSeconds < _blinkThreshold)
            {
                float t = (Mathf.Sin(Time.time * _blinkSpeed) + 1f) * 0.5f; // 0..1
                alpha = Mathf.Lerp(_minBlinkAlpha, 1f, t);
            }

            Color c = _pool[i].image.color;
            c.a = alpha;
            _pool[i].image.color = c;
        }
    }
}
