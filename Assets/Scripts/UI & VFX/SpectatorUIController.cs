using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpectatorUIController : MonoBehaviour
{
    [SerializeField] private GameObject _spectatorRoot;
    [SerializeField] private TMP_Text _targetNameText;
    [SerializeField] private Button _prevButton;
    [SerializeField] private Button _nextButton;

    private void Awake()
    {
        if (_prevButton != null) _prevButton.onClick.AddListener(OnPrev);
        if (_nextButton != null) _nextButton.onClick.AddListener(OnNext);

        if (_spectatorRoot != null) _spectatorRoot.SetActive(false);
    }

    private void OnEnable()
    {
        CameraManager.OnSpectatorTargetChanged += UpdateName;
    }

    private void OnDisable()
    {
        CameraManager.OnSpectatorTargetChanged -= UpdateName;
    }

    private void OnPrev()
    {
        if (CameraManager.Instance != null)
            CameraManager.Instance.SpectatePrevious();
    }

    private void OnNext()
    {
        if (CameraManager.Instance != null)
            CameraManager.Instance.SpectateNext();
    }

    private void UpdateName(string targetName)
    {
        if (_targetNameText != null)
            _targetNameText.text = targetName;
    }
}
