using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterInfoPopupController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject _popupRoot;
    [SerializeField] private Button _overlayButton; // make overlay button - if press overlay = exit
    [SerializeField] private Button _closeButton;
    [SerializeField] private TMP_Text _characterNameText;
    [SerializeField] private TMP_Text _characterDescriptionText;
    [SerializeField] private Image _characterSplashArt;
    [SerializeField] private TMP_Text _skillNameText;
    [SerializeField] private Image _skillIcon;
    [SerializeField] private TMP_Text _skillDescriptionText;

    private void Awake()
    {
        if (_overlayButton != null)
            _overlayButton.onClick.AddListener(Hide);

        if (_closeButton != null)
            _closeButton.onClick.AddListener(Hide);

        if (_popupRoot != null)
            _popupRoot.SetActive(false);
    }

    /// <summary>
    /// show popup with updated info according to currently selected character
    /// </summary>
    public void Show(CharacterComponentLinker linker)
    {
        if (linker == null || _popupRoot == null) return;

        CharacterDataSO charData = linker._characterData;
        AbilitySO ability = linker.ability;

        if (charData != null)
        {
            if (_characterNameText != null) _characterNameText.text = charData.CharacterName;
            if (_characterDescriptionText != null) _characterDescriptionText.text = charData.CharacterDescription;
            if (_characterSplashArt != null) _characterSplashArt.sprite = charData.CharacterSplashArt;
        }

        if (ability != null)
        {
            if (_skillNameText != null) _skillNameText.text = ability.name;
            if (_skillIcon != null) _skillIcon.sprite = ability._icon;
            if (_skillDescriptionText != null) _skillDescriptionText.text = ability._description;
        }

        _popupRoot.SetActive(true);
    }

    public void Hide()
    {
        if (_popupRoot != null)
            _popupRoot.SetActive(false);
    }
}
