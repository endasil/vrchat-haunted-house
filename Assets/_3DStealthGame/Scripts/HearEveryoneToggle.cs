using TMPro;

using UdonSharp;

using UnityEngine;

using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class HearEveryoneToggle : UdonSharpBehaviour
{
    public TextMeshPro label;
    public ButtonGlow buttonGlow;
    public HearEveryoneToggle[] linkedToggles;

    private const float HearEveryoneNear = 500f;
    private const float HearEveryoneFar = 1000f;

    private const float DefaultVoiceNear = 0f;
    private const float DefaultVoiceFar = 25f;

    private bool _hearEveryone;

    void Start()
    {
        if (label == null || buttonGlow == null)
        {
            Debug.LogError($"HearEveryoneToggle ({gameObject.name}): label and buttonGlow need to be assigned in the inspector.", gameObject);
            return;
        }

        ShowState();
    }

    public override void Interact()
    {
        _hearEveryone = !_hearEveryone;
        ShowState();

        foreach (HearEveryoneToggle linkedToggle in linkedToggles)
        {
            linkedToggle.ShowLinkedState(_hearEveryone);
        }

        VRCPlayerApi[] players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];

        VRCPlayerApi.GetPlayers(players);

        foreach (VRCPlayerApi player in players)
        {
            if (!Utilities.IsValid(player) || player.isLocal)
                continue;

            player.SetVoiceDistanceNear(_hearEveryone ? HearEveryoneNear : DefaultVoiceNear);

            player.SetVoiceDistanceFar(_hearEveryone ? HearEveryoneFar : DefaultVoiceFar);
        }
    }

    public void ShowLinkedState(bool hearEveryone)
    {
        _hearEveryone = hearEveryone;
        ShowState();
    }

    private void ShowState()
    {
        if (_hearEveryone)
        {
            label.text = "Hear everyone\nON";
        }
        else
        {
            label.text = "Hear everyone\nOFF";
        }

        buttonGlow.ShowOn(_hearEveryone);
    }

    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        if (!_hearEveryone)
            return;

        player.SetVoiceDistanceNear(HearEveryoneNear);
        player.SetVoiceDistanceFar(HearEveryoneFar);
    }
}