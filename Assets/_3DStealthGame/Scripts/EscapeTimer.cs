using TMPro;

using UdonSharp;

using UnityEngine;

using VRC.SDKBase;
using VRC.SDK3.Persistence;

using Assets._3DStealthGame.Scripts;
using _3DStealthGame.Scripts.Constants;

// Starts a run timer the first time the local player walks through
// the trigger this is attached to. When the player reaches the exit,

[DisallowMultipleComponent]
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class EscapeTimer : Resettable
{
    // Text on the PlayerUI HUD canvas that shows the running time.
    public TextMeshProUGUI display;

    public MusicFader music;

    public FogFader fog;

    // Finishes faster than this is ignored instead of becoming a best time
	// to make it slightly harder to cheat.
    private const float MinPlausibleTime = 40f;

    // Added to the run time while an admin has the cheat on.
    private const float CheatPenaltySeconds = 99f * 60f;

    private bool _isCheatActive;
    private bool _hasUsedPillCheat;
    private bool _started;
    private bool _running;
    private float _elapsed;

    private bool _hasBestTime;
    private float _bestTime;

    public override void OnPlayerRestored(VRCPlayerApi player)
    {
        if (!player.isLocal) return;
        
        // Get the players best run if it is saved.
        if (PlayerData.TryGetFloat(player, PlayerDataKeys.SavedPersonalBestEscapeTime, out _bestTime))
        {
            _hasBestTime = true;
        }
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (!player.isLocal) return;
        // Start the first time the player passes trought eh trigger, 
		// then after that ignore the player going trough until the 
		// die or escape.
        if (_started) return;

        _started = true;
        _running = true;
        _elapsed = 0f;
        if (_isCheatActive || _hasUsedPillCheat)
        {
            _elapsed = CheatPenaltySeconds;
        }

        if (music == null)
        {
            Debug.LogError($"EscapeTimer ({gameObject.name}): music needs to be assigned in the inspector.");
        }
        else
        {
            music.FadeToFull();
        }

        if (fog == null)
        {
            Debug.LogError($"EscapeTimer ({gameObject.name}): fog needs to be assigned in the inspector.");
        }
        else
        {
            fog.FadeIn(music.fadeInDuration);
        }
    }

    // Called by ExitTriggerU when the local player reaches the exit.
    // Returns true when the player beat their old speed record..
    public bool StopAndSaveIfBest()
    {
        if (!_running) return false;

        _running = false;

        if (_elapsed < MinPlausibleTime)
        {
            return false;
        }

        if (_hasBestTime && _elapsed >= _bestTime)
        {
            return false;
        }

        _bestTime = _elapsed;
        _hasBestTime = true;
        PlayerData.SetFloat(PlayerDataKeys.SavedPersonalBestEscapeTime, _bestTime);
        return true;
    }

    // Switching the cheat off keeps the penalty in a running time.
    public void SetCheatActive(bool isActive)
    {
        _isCheatActive = isActive;
        if (_isCheatActive && _running)
        {
            _elapsed += CheatPenaltySeconds;
        }
    }

    // Adds the penalty once per run, at the start of the run if the timer is not running yet.
    public void MarkPillCheatUsed()
    {
        if (_hasUsedPillCheat) return;

        _hasUsedPillCheat = true;
        if (_running)
        {
            _elapsed += CheatPenaltySeconds;
        }
    }

    public float GetElapsed()
    {
        return _elapsed;
    }

    public bool HasBestTime()
    {
        return _hasBestTime;
    }

    public float GetBestTime()
    {
        return _bestTime;
    }

    public void Update()
    {
        if (_running)
        {
            _elapsed += Time.deltaTime;
        }

        if (display == null)
        {
            Debug.LogError($"EscapeTimer ({gameObject.name}): display text is not assigned in the inspector.");
            return;
        }

        if (_hasBestTime)
        {
            display.text = TimeFormatter.Format(_elapsed) + "\nbest " + TimeFormatter.Format(_bestTime);
        }
        else
        {
            display.text = TimeFormatter.Format(_elapsed);
        }
    }

    public override void ResetState()
    {
        _started = false;
        _running = false;
        _elapsed = 0f;
        _hasUsedPillCheat = false;
    }

}
