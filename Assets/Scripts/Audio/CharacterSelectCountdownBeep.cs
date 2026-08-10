using UnityEngine;

public class CharacterSelectCountdownBeep : MonoBehaviour
{
    private int _lastWholeSecond = -1;

    private void Update()
    {
        var ctx = MatchContext.Current;

        // only beep during the final character-select countdown of whichever mode is live
        if (ctx == null || !ctx.IsSpawned || !ctx.IsInFinalCharacterSelectCountdown)
        {
            _lastWholeSecond = -1;
            return;
        }

        int whole = Mathf.CeilToInt(ctx.GetRemainingStateTime());
        if (whole != _lastWholeSecond && whole > 0)
        {
            _lastWholeSecond = whole;
            SoundManager.Instance?.PlaySFX(SoundID.CharacterSelectCountdown);
        }
    }
}
