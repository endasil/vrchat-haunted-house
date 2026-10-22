using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.Udon;

public static class TimerCheats
{
    private const string MenuPath = "Haunted House/Cheats/Set Run Time To 99 Minutes";
    private const float CheatTimeSeconds = 99f * 60f;

    [MenuItem(MenuPath)]
    private static void SetRunTimeTo99Minutes()
    {
        foreach (UdonBehaviour udonBehaviour in Object.FindObjectsOfType<UdonBehaviour>())
        {
            if (UdonSharpEditorUtility.GetUdonSharpBehaviourType(udonBehaviour) != typeof(EscapeTimer)) continue;

            // Starts the timer too, so the cheat works before the player passes the start trigger.
            udonBehaviour.SetProgramVariable("_started", true);
            udonBehaviour.SetProgramVariable("_running", true);
            udonBehaviour.SetProgramVariable("_elapsed", CheatTimeSeconds);
            return;
        }

        throw new System.InvalidOperationException("No EscapeTimer UdonBehaviour in the scene.");
    }

    [MenuItem(MenuPath, true)]
    private static bool SetRunTimeTo99MinutesValidate()
    {
        return EditorApplication.isPlaying;
    }
}
