using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// heartbased lives UI
/// reads PlayerEliminationHandler.CurrentLives
/// fills hearts from left to right
/// </summary>
public class PlayerLivesUIController : MonoBehaviour
{
    [SerializeField] private Image[] _heartImages; // ordered left to right
    [SerializeField] private Sprite _fullHeart;
    [SerializeField] private Sprite _emptyHeart;

    private PlayerEliminationHandler _elimination;
    private int _lastLives = -1;

    private void OnEnable()
    {
        PlayerRegistry.OnLocalPlayerSpawned += HandleLocalPlayerSpawned;
        TryBindLocal();
    }

    private void OnDisable()
    {
        PlayerRegistry.OnLocalPlayerSpawned -= HandleLocalPlayerSpawned;
    }

    private void HandleLocalPlayerSpawned(Transform _)
    {
        TryBindLocal();
    }

    private void TryBindLocal()
    {
        if (NetworkPlayerController.Local != null)
            _elimination = NetworkPlayerController.Local.Registry.Elimination;
    }

    private void Update()
    {
        if (_elimination == null)
        {
            TryBindLocal();
            return;
        }
    
        int lives = _elimination.CurrentLives;
        if (lives == _lastLives) return; // only redraw on change
        _lastLives = lives;

        Refresh(lives);
    }

    private void Refresh(int lives)
    {
        if (_heartImages == null) return;

        for (int i = 0; i < _heartImages.Length; i++)
        {
            if (_heartImages[i] == null) continue;

            // heart i is full if its index is below the current life count
            bool full = i < lives;
            _heartImages[i].sprite = full ? _fullHeart : _emptyHeart;
        }
    }
}
