using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public static class PillCheats
{

    // - % Ctrl.
    // - # Shift.
    // - & Alt.
    // - p P key.
    private const string MenuPath = "Haunted House/Cheats/Give Next Missing Pill %#&p";

    [MenuItem(MenuPath)]
    private static void GiveNextMissingPill()
    {
        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        if (localPlayer == null)
        {
            throw new System.InvalidOperationException("No local player. Enter play mode with ClientSim first.");
        }

        GameObject playerObject = localPlayer.GetPlayerObjects()[0];

        foreach (UdonBehaviour udonBehaviour in playerObject.GetComponents<UdonBehaviour>())
        {
            if (UdonSharpEditorUtility.GetUdonSharpBehaviourType(udonBehaviour) != typeof(PlayerInventory)) continue;

            udonBehaviour.SendCustomEvent(nameof(PlayerInventory.AddNextMissingPill));
            return;
        }

        throw new System.InvalidOperationException($"Player object '{playerObject.name}' has no PlayerInventory UdonBehaviour.");
    }

    [MenuItem(MenuPath, true)]
    private static bool GiveNextMissingPillValidate()
    {
        return EditorApplication.isPlaying;
    }
}
