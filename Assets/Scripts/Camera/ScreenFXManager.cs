using System.Collections;
using UnityEngine;
using System;

public class ScreenFXManager : MonoBehaviour
{
    public static ScreenFXManager Instance { get; private set; }

    [Header("Blink Settings")]
    [SerializeField] private Material _blinkMaterial;
    [SerializeField] private float _blinkDuration = 0.15f; // total time for open and close sequence
    [SerializeField] private float _holdDuration = 0.1f;

    private readonly int _blinkProgressID = Shader.PropertyToID("_BlinkProgress");
    private bool _isBlinking = false;

    [Header("Grayscale Settings")]
    [SerializeField] private Material _blackAndWhiteMaterial;
    [SerializeField] private float _grayFadeDuration = 3.0f;

    private float _targetGrayscale = 0f;
    private float _currentGrayscale = 0f;
    private readonly int _grayscaleIntensityID = Shader.PropertyToID("_Intensity");

    // getters
    public bool IsBlinking => _isBlinking;

    // events
    public static event Action OnPeakDarknessReached; // signals that blinkProgress == 1, screen is completely dark

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        ResetMaterials();
    }

    private void OnEnable()
    {
        CameraManager.OnCameraSwapRequested += StartBlinkSequence; // starts blink sequence when spectator input detected
        PlayerEliminationHandler.OnPlayerEliminated += HandleLocalPlayerEliminated;
        PlayerEliminationHandler.OnPlayerSpectatorReady += HandleSpectatorReady;
    }

    private void OnDisable()
    {
        CameraManager.OnCameraSwapRequested -= StartBlinkSequence;
        PlayerEliminationHandler.OnPlayerEliminated -= HandleLocalPlayerEliminated;
        PlayerEliminationHandler.OnPlayerSpectatorReady -= HandleSpectatorReady;
    }

    private void Update()
    {
        // animate b&w transition
        if (!Mathf.Approximately(_currentGrayscale, _targetGrayscale))
        {
            _currentGrayscale = Mathf.MoveTowards(_currentGrayscale, _targetGrayscale, Time.deltaTime / _grayFadeDuration);
            if (_blackAndWhiteMaterial != null)
                _blackAndWhiteMaterial.SetFloat(_grayscaleIntensityID, Mathf.Clamp01(_currentGrayscale));
        }
    }

    private void HandleLocalPlayerEliminated(PlayerEliminationHandler handler)
    {
        // turn gray when player dies
        if (handler.Object.HasInputAuthority)
        {
            _targetGrayscale = 1f; // for update to start blending b&w
        }    
    }

    private void ResetMaterials()
    {
        _currentGrayscale = 0f;
        _targetGrayscale = 0f;

        if (_blinkMaterial != null) 
            _blinkMaterial.SetFloat(_blinkProgressID, 0f);

        if (_blackAndWhiteMaterial != null) 
            _blackAndWhiteMaterial.SetFloat(_grayscaleIntensityID, 0f);
    }

    /// <summary>
    /// triggers full screen blink, executes cam action at peak darkness
    /// </summary>
    private void StartBlinkSequence()
    {
        if (!IsBlinking)
            StartCoroutine(BlinkRoutine());
    }

    private IEnumerator BlinkRoutine()
    {
        _isBlinking = true;
        float halfDuration = _blinkDuration * 0.5f;
        float elapsed = 0f;

        // closing
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;

            // map elapsed time to blink progress
            float progress = Mathf.Lerp(0f, 1f, elapsed/halfDuration);
            _blinkMaterial.SetFloat(_blinkProgressID, progress);
            yield return null;
        }

        // ensure perfect darkness milestone
        _blinkMaterial.SetFloat(_blinkProgressID, 1f);

        // signal to mainly camera manager to swap camera when blinkProgress == 1
        OnPeakDarknessReached?.Invoke();

        yield return new WaitForSeconds(_holdDuration); // wait for hold duration before opening

        // opening
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Lerp(1f, 0f, elapsed / halfDuration);
            _blinkMaterial.SetFloat(_blinkProgressID, progress);
            yield return null;
        }

        // reset back too completely unblinked
        _blinkMaterial.SetFloat(_blinkProgressID, 0f);
        _isBlinking = false;
    }

    private void HandleSpectatorReady(PlayerEliminationHandler handler)
    {
        // remove black and white for this player
        if (handler.Object.HasInputAuthority)
        {
            _currentGrayscale = 0f;
            _targetGrayscale = 0f;

            if (_blackAndWhiteMaterial != null)
                _blackAndWhiteMaterial.SetFloat(_grayscaleIntensityID, 0f);
        }
    }

    private void OnDestroy()
    {
        // reset shader on exit
        if (_blinkMaterial != null)
            _blinkMaterial.SetFloat(_blinkProgressID, 0f);
        if (_blackAndWhiteMaterial != null)
            _blackAndWhiteMaterial.SetFloat(_grayscaleIntensityID, 0f);
    }
}
