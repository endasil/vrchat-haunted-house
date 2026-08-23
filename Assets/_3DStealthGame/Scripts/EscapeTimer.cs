using TMPro;

using UdonSharp;

using UnityEngine;

using VRC.SDKBase;
using VRC.SDK3.Persistence;

using Assets._3DStealthGame.Scripts;
using _3DStealthGame.Scripts.Constants;

// Starts a run timer the first time the local player walks through
// the trigger this is attached to. When the player reaches the exit,




// Inherits Resettable so ResetManager.ResetAll() (called after a catch) 
// can clear the timer for the next attempt.
[DisallowMultipleComponent]
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class EscapeTimer : Resettable
{
    // Text on the PlayerUI HUD canvas that shows the running time.
    public TextMeshProUGUI display;

    public MusicFader music;

    // Finishes faster than this is ignored instead of becoming a best time
	// to make it slightly harder to cheat.
    private const float MinPlausibleTime = 40f;

    private bool _started;
    private bool _running;
    private float _elapsed;

    private bool _hasBestTime;
    private float _bestTime;

    public override void OnPlayerRestored(VRCPlayerApi player)
    {
        if (!player.isLocal) return;
        
        // This method is called once a players permanent data has been loaded,
		// Check if their fastest run has been recorded. If so fetch it. 

        _hasBestTime = PlayerData.HasKey(player, PlayerDataKeys.BestEscapeTime);
        Debug.Log($"OnPlayerRestored: _hasBest:  {_hasBestTime}");
        if (_hasBestTime)
        {
            _bestTime = PlayerData.GetFloat(player, PlayerDataKeys.BestEscapeTime);
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
        _elapsed = 0.0f;

        if (music == null)
        {
            Debug.LogError($"EscapeTimer ({gameObject.name}): music is not assigned in the inspector.");
        }
        else
        {
            music.FadeToFull();
        }
    }

    // Called by ExitTriggerU when the local player reaches the exit.
    public void StopTimer()
    {
        if (!_running) return;

        _running = false;

        if (_elapsed < MinPlausibleTime)
        {
            return;
        }

        if (_hasBestTime && _elapsed >= _bestTime)
        {
            Debug.Log("Finish time was worse than best time, doing nothing.");
            return;
        }

        _bestTime = _elapsed;
        _hasBestTime = true;
        Debug.Log($"Setting new best time in key {PlayerDataKeys.BestEscapeTime} to {_bestTime} ");
        PlayerData.SetFloat(PlayerDataKeys.BestEscapeTime, _bestTime);
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
    }

}
