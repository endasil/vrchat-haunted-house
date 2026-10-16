using UdonSharp;

using UnityEngine;

using VRC.SDKBase;

// Voice settings are local: each client sets them for every other player it hears.
[DisallowMultipleComponent]
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class VoiceAmplifier : UdonSharpBehaviour
{
    // Full volume up to voiceNear metres, silent at voiceFar.
    public float voiceGain = 15f;
    public float voiceNear = 500f;
    public float voiceFar = 1000f;

    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        if (player.isLocal) return;

        player.SetVoiceGain(voiceGain);
        player.SetVoiceDistanceNear(voiceNear);
        player.SetVoiceDistanceFar(voiceFar);
    }
}
