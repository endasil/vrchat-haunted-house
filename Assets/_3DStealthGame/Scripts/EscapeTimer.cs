using TMPro;

using UdonSharp;

using UnityEngine;

using VRC.SDKBase;

using Assets._3DStealthGame.Scripts;

// Starts a run timer the first time the local player walks through,
// stops it when they reach the exit. the time is shown in the 
// hud. 
//
// Inherits Resettable so ResetManager.ResetAll() (called after a catch) clears
// the timer for the next attempt through the same path pills and doors use.
[DisallowMultipleComponent]
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class EscapeTimer : Resettable
{
    // Text on the PlayerUI HUD canvas that shows the running time.
    public TextMeshProUGUI display;

    private bool _started;
    private bool _running;
    private float _elapsed;

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
    }

    // Called by ExitTriggerU when the local player reaches the exit.
    public void Stop()
    {
        _running = false;
    }

    public void Update()
    {
        if (_running)
            _elapsed += Time.deltaTime;

        if (display == null)
        {
            Debug.LogError($"EscapeTimer ({gameObject.name}): display text is not assigned in the inspector.");
            return;
        }
        display.text = FormatTime(_elapsed);
    }

    public override void ResetState()
    {
        _started = false;
        _running = false;
        _elapsed = 0f;
    }

    // mm:ss.hh, built manually because Udon doesn't have ToString("D2").
    private string FormatTime(float seconds)
    {
        int totalHundredths = (int)(seconds * 100f);
        int minutes = totalHundredths / 6000;
        int secs = (totalHundredths / 100) % 60;
        int hundredths = totalHundredths % 100;
        return TwoDigits(minutes) + ":" + TwoDigits(secs) + "." + TwoDigits(hundredths);
    }

    private string TwoDigits(int value)
    {
        if (value < 10) return "0" + value;
        return "" + value;
    }
}
