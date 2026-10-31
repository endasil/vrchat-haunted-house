using UdonSharp;

using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FogDuringRender : UdonSharpBehaviour
{
    public FogFader fogFader;

    private float _savedDensity;

    void OnPreRender()
    {
        _savedDensity = RenderSettings.fogDensity;
        RenderSettings.fogDensity = fogFader.GetFullDensity();
    }

    void OnPostRender()
    {
        RenderSettings.fogDensity = _savedDensity;
    }
}
