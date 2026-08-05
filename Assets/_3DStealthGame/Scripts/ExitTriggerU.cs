
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
    public EscapeTimer escapeTimer;

    void Start()
    {
        if (gameEnding == null)
        {
            Debug.LogError($"ExitTriggerU ({gameObject.name}): gameEnding reference is not set in the inspector");
        }

        if (escapeTimer == null)
        {
            Debug.LogError($"ExitTriggerU ({gameObject.name}): escapeTimer reference is not set in the inspector");
        }
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (!player.isLocal) return;

        PlayerInventory playerInventory = player.GetPlayerObjects()[0].GetComponent<PlayerInventory>();

        if (playerInventory == null)
        {
            Debug.LogError($"ExitTriggerU ({gameObject.name}): unable to find PlayerInventory on the local player object");
            return;
        }

        if (!playerInventory.HasAllPills())
        {
            Debug.Log($"ExitTriggerU ({gameObject.name}): player is missing pills, the exit stays shut.");
            return;
        }

        gameEnding.ReachedExit();
        escapeTimer.StopTimer();
    }
}
