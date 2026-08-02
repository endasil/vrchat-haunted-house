using TMPro;

using UdonSharp;

using UnityEngine;

// Shows frames per second on the HUD, the same way EscapeTimer shows the run
// time. PlayerUI pins this text to a fixed screen spot on desktop.
[DisallowMultipleComponent]
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FpsCounter : UdonSharpBehaviour
{
    // Text on the PlayerUI HUD canvas that shows the FPS.
    public TextMeshProUGUI display;

    // How often to refresh the number, in seconds. Updating every frame makes
    // it flicker too fast to read.
    public float refreshInterval = 0.5f;

    private int _frames;
    private float _elapsed;

    public void Update()
    {
        _frames++;
        _elapsed += Time.unscaledDeltaTime;

        if (_elapsed < refreshInterval) return;

        if (display == null)
        {
            Debug.LogError($"FpsCounter ({gameObject.name}): display text is not assigned in the inspector.");
            return;
        }

        int fps = (int)(_frames / _elapsed + 0.5f);
        display.text = fps + " FPS";

        _frames = 0;
        _elapsed = 0f;
    }
}
