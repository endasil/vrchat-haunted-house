using UdonSharp;

using UnityEngine;

[DisallowMultipleComponent]
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MusicFader : UdonSharpBehaviour
{
    public float fadeInDuration = 5f;
    public float maxVolume = 0.7f;

    private AudioSource _music;
    private bool _paused;

    private bool _fading;
    private float _fadeFrom;
    private float _fadeTo;
    private float _fadeElapsed;

    void Start()
    {
        _music = GetComponent<AudioSource>();
        if (_music == null)
        {
            Debug.LogError($"MusicFader ({gameObject.name}): no AudioSource on this object.");
            return;
        }

        _music.volume = 0f;
    }

    // Called by EscapeTimer when the local player starts a run.
    public void FadeToFull()
    {
        if (_music == null)
        {
            Debug.LogError($"MusicFader ({gameObject.name}): no AudioSource on this object.");
            return;
        }

        if (_paused)
        {
            _music.volume = 0f;
            _music.UnPause();
            _paused = false;
        }
        else if (!_music.isPlaying)
        {
            _music.volume = 0f;
            _music.Play();
        }

        FadeTo(maxVolume);
    }

    public void PauseMusic()
    {
        if (_music == null)
        {
            Debug.LogError($"MusicFader ({gameObject.name}): no AudioSource on this object.");
            return;
        }

        if (!_music.isPlaying) return;

        _fading = false;
        _music.Pause();
        _paused = true;
    }

    private void FadeTo(float target)
    {
        _fadeFrom = _music.volume;
        _fadeTo = target;
        _fadeElapsed = 0f;
        _fading = true;
    }

    public void Update()
    {
        if (!_fading) return;

        _fadeElapsed += Time.deltaTime;

        float t = 1f;
        if (fadeInDuration > 0f)
        {
            t = _fadeElapsed / fadeInDuration;
        }
        _music.volume = Mathf.Lerp(_fadeFrom, _fadeTo, t);

        if (t >= 1f)
        {
            _music.volume = _fadeTo;
            _fading = false;
        }
    }
}
