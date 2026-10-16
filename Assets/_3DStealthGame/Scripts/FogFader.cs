using UdonSharp;

using UnityEngine;

[DisallowMultipleComponent]
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FogFader : UdonSharpBehaviour
{
    private float _fullDensity;

    private bool _fading;
    private float _fadeDuration;
    private float _fadeElapsed;

    void Start()
    {
        _fullDensity = RenderSettings.fogDensity;
        RenderSettings.fogDensity = 0f;
    }

    public void FadeIn(float duration)
    {
        _fadeDuration = duration;
        _fadeElapsed = 0f;
        _fading = true;
    }

    public void Clear()
    {
        _fading = false;
        RenderSettings.fogDensity = 0f;
    }

    public void Update()
    {
        if (!_fading) return;

        _fadeElapsed += Time.deltaTime;

        float t = 1f;
        if (_fadeDuration > 0f)
        {
            t = Mathf.Clamp01(_fadeElapsed / _fadeDuration);
        }
        // Unity squares the fog value, and the square root undoes that,
        // so the fog grows evenly.
        RenderSettings.fogDensity = _fullDensity * Mathf.Sqrt(t);
        
        if (t >= 1f)
        {
            _fading = false;
        }
    }
}
