using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ExitTriggerU : UdonSharpBehaviour
{
    public GameEndingU gameEnding;
    public EscapeTimer escapeTimer;
    public Leaderboard leaderboard;

    void Start()
    {
        if (gameEnding == null)
        {
            Debug.LogError($"ExitTriggerU ({gameObject.name}): gameEnding needs to be assigned in the inspector");
        }

        if (escapeTimer == null)
        {
            Debug.LogError($"ExitTriggerU ({gameObject.name}): escapeTimer needs to be assigned in the inspector");
        }

        if (leaderboard == null)
        {
            Debug.LogError($"ExitTriggerU ({gameObject.name}): leaderboard needs to be assigned in the inspector.");
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
            return;
        }

        float runTime = escapeTimer.GetElapsed();
        bool isNewBest = escapeTimer.StopAndSaveIfBest();
        int leaderboardPosition;
        leaderboardPosition = isNewBest ? 
            leaderboard.GetPositionFor(player.displayName, runTime) : 0;

        gameEnding.ReachedExit(runTime, escapeTimer.HasBestTime(), escapeTimer.GetBestTime(), isNewBest, leaderboardPosition);
    }
}
