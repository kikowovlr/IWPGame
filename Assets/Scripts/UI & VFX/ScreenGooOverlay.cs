using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ScreenGooOverlay : MonoBehaviour
{
    public static ScreenGooOverlay Instance { get; private set; }

    [Header("Sprites")]
    [SerializeField] private Sprite[] _splatSprites;
    [SerializeField] private Image _splatPrefab;
    [SerializeField] private RectTransform _container;
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("Spawn Behaviour")]
    [SerializeField] private int _maxSplats = 14; // cap on screen
    [SerializeField] private float _minSpawnInterval = 0.15f; // deep in goo = fast spawn
    [SerializeField] private float _maxSpawnInterval = 0.6f;  // shallow = slow spawn
    [SerializeField] private float _spriteFadeInTime = 0.25f;
    [SerializeField] private float _layerFadeOutTime = 1f; // when you leave goo

    [SerializeField] private Color _gooColor = new Color(1f, 0.85f, 0.1f);
    [SerializeField] private float _edgeBias = 0.8f; // how strongly splats hug the edges (0=center ok, 1=edges only)
    [SerializeField] private Vector2 _scaleRange = new Vector2(0.8f, 1.6f); // randomisation

    private readonly List<GooSplat> _activeSplats = new List<GooSplat>();
    private readonly Queue<Image> _pool = new Queue<Image>();

    private float _exposure01; // 0..1 current goo intensity (from GooExposure)
    private bool _inGoo;
    private float _spawnTimer;
    private float _layerFadeVel; // for smoothing

    private class GooSplat
    {
        public Image image;
        public float fadeInTime;
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Call every frame the local player is in goo, passing 0..1 exposure (deeper/longer = higher)
    /// Call SetGooExposure(0) or StopGoo() when they leave
    /// </summary>
    public void SetGooExposure(float exposure01, Color? colorOverride = null)
    {
        _exposure01 = Mathf.Clamp01(exposure01);
        _inGoo = _exposure01 > 0.01f;
        if (colorOverride.HasValue) _gooColor = colorOverride.Value;
    }

    public void StopGoo()
    {
        _inGoo = false;
        _exposure01 = 0f;
    }

    private void Update()
    {
        // master layer alpha tracks exposure while in goo, fades to 0 when out
        float targetAlpha = _inGoo ? Mathf.Lerp(0.3f, 1f, _exposure01) : 0f;
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = Mathf.MoveTowards(
                _canvasGroup.alpha, targetAlpha,
                Time.deltaTime / (_inGoo ? _spriteFadeInTime : _layerFadeOutTime));
        }

        if (_inGoo)
        {
            // spawn accumulation —> interval scales with exposure (longer = faster spawns)
            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer <= 0f && _activeSplats.Count < _maxSplats)
            {
                SpawnSplat();
                float interval = Mathf.Lerp(_maxSpawnInterval, _minSpawnInterval, _exposure01);
                _spawnTimer = interval;
            }
        }
        else
        {
            // left goo —> once layer fully faded, clear all splats back to pool
            if (_canvasGroup != null && _canvasGroup.alpha <= 0.001f && _activeSplats.Count > 0)
                ClearAllSplats();
        }

        // fade in each sprite individually (over the layer alpha)
        for (int i = 0; i < _activeSplats.Count; i++)
        {
            var splat = _activeSplats[i];
            if (splat.fadeInTime < 1f)
            {
                splat.fadeInTime = Mathf.Min(1f, splat.fadeInTime + Time.deltaTime / _spriteFadeInTime);
                Color c = _gooColor;
                c.a = splat.fadeInTime;
                splat.image.color = c;
            }
        }
    }

    private void SpawnSplat()
    {
        Image img = GetFromPool();
        img.sprite = _splatSprites[Random.Range(0, _splatSprites.Length)];

        // random edge-biased position
        Vector2 anchored = GetEdgeBiasedPosition();
        RectTransform rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchored;

        // random rotation, scale, flip
        rt.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
        float scale = Random.Range(_scaleRange.x, _scaleRange.y);
        float flipX = Random.value > 0.5f ? -1f : 1f;
        rt.localScale = new Vector3(scale * flipX, scale, 1f);

        // start transparent, fade in
        Color c = _gooColor; c.a = 0f;
        img.color = c;
        img.gameObject.SetActive(true);

        _activeSplats.Add(new GooSplat { image = img, fadeInTime = 0f });
    }

    private Vector2 GetEdgeBiasedPosition()
    {
        // container size
        Vector2 size = _container.rect.size;
        float halfW = size.x * 0.5f;
        float halfH = size.y * 0.5f;

        // pick a random point, then push it toward the nearest edge based on _edgeBias
        float x = Random.Range(-halfW, halfW);
        float y = Random.Range(-halfH, halfH);

        // bias toward edges: interpolate the coordinate toward its sign*half
        x = Mathf.Lerp(x, Mathf.Sign(x) * halfW, _edgeBias * Random.value);
        y = Mathf.Lerp(y, Mathf.Sign(y) * halfH, _edgeBias * Random.value);

        return new Vector2(x, y);
    }

    private Image GetFromPool()
    {
        if (_pool.Count > 0)
        {
            Image img = _pool.Dequeue();
            return img;
        }
        return Instantiate(_splatPrefab, _container);
    }

    private void ClearAllSplats()
    {
        foreach (var s in _activeSplats)
        {
            s.image.gameObject.SetActive(false);
            _pool.Enqueue(s.image);
        }
        _activeSplats.Clear();
    }
}
