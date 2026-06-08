
using UdonSharp;

using UnityEngine;

using VRC.SDKBase;
using VRC.Udon;

// Sits on the exit trigger collider. When the local player walks into it, it
// tells the GameEndingU manager that the exit was reached. Detection lives here
// (on the collider) while the outcome handling lives on the manager.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ExitTriggerU : UdonSharpBehaviour
{
    public GameEndingU gameEnding;

    private VRCPlayerApi _localPlayer;

    void Start()
    {
        _localPlayer = Networking.LocalPlayer;

        if (gameEnding == null)
            Debug.LogError("ExitTriggerU: gameEnding reference is not set in the inspector");
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player != _localPlayer)
            return;

        gameEnding.ReachedExit();
    }
}
