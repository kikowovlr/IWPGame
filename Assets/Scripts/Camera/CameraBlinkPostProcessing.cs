using System.Collections;
using UnityEngine;
using System;

public class CameraBlinkPostProcessing : MonoBehaviour
{
    public static CameraBlinkPostProcessing Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Material _blinkMaterial;

    [Header("Settings")]
    [SerializeField] private float _blinkDuration = 0.15f; // total time for open and close sequence

    private readonly int _blinkProgressID = Shader.PropertyToID("_BlinkProgress");
    private bool _isBlinking = false;

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

        // reset shader to fully open (0)
        if (_blinkMaterial != null)
        {
            _blinkMaterial.SetFloat(_blinkProgressID, 0f);
        }
    }

    private void OnEnable()
    {
        CameraManager.OnCameraSwapRequested += StartBlinkSequence; // starts blink sequence when spectator input detected
    }

    private void OnDisable()
    {
        CameraManager.OnCameraSwapRequested -= StartBlinkSequence;
    }

    /// <summary>
    /// triggers full screen blink, executes cam action at peak darkness
    /// </summary>
    private void StartBlinkSequence()
    {
        if (IsBlinking)
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
            float progress = Mathf.Lerp(0f, 0.5f, elapsed/halfDuration);
            _blinkMaterial.SetFloat(_blinkProgressID, progress);
            yield return null;
        }

        // ensure perfect darkness milestone
        _blinkMaterial.SetFloat(_blinkProgressID, 0.5f);

        // signal to mainly camera manager to swap camera when blinkProgress == 1
        OnPeakDarknessReached?.Invoke();

        yield return null; // wait for one frame to snap to diff cam

        // opening
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Lerp(0.5f, 1f, elapsed / halfDuration);
            _blinkMaterial.SetFloat(_blinkProgressID, progress);
            yield return null;
        }

        // reset back too completely unblinked
        _blinkMaterial.SetFloat(_blinkProgressID, 0f);
        _isBlinking = false;
    }

    private void OnDestroy()
    {
        // reset shader on exit
        if (_blinkMaterial != null)
        {
            _blinkMaterial.SetFloat(_blinkProgressID, 0f);
        }
    }
}
