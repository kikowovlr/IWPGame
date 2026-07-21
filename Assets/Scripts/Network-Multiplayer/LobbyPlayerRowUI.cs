using TMPro;
using UnityEngine;

public class LobbyPlayerRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private GameObject _crownIcon; // if host
    [SerializeField] private TMP_Text _statusText;

    public void SetData(string playerName, bool isHost)
    {
        if (_nameText != null) _nameText.text = playerName;
        if (_crownIcon != null) _crownIcon.SetActive(isHost);
        if (_statusText != null) _statusText.text = "Available"; // TODO: everyone in lobby reads as Available for now
    }
}
