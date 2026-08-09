using UnityEngine;
using System.Collections;
using TMPro;

public class SuddenDeathUIController : MonoBehaviour
{
    public static SuddenDeathUIController Instance { get; private set; }

    [SerializeField] private GameObject _bannerPanel;   // full-screen cover


    private void Awake()
    {
        Instance = this;
        if (_bannerPanel != null) _bannerPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void OnPhaseChanged(SuddenDeathPhase phase)
    {
        switch (phase)
        {
            case SuddenDeathPhase.Banner:
                if (_bannerPanel != null) _bannerPanel.SetActive(true);
                break;

            // banner gone for countdown + everything after
            case SuddenDeathPhase.Countdown:
            case SuddenDeathPhase.Fighting:
            case SuddenDeathPhase.Done:
                if (_bannerPanel != null) _bannerPanel.SetActive(false);
                break;
        }
    }
}
