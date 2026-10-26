using Assets._3DStealthGame.Scripts;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.Udon;

public static class GhostCheats
{
    private const string MenuPath = "Haunted House/Cheats/Toggle Ghosts Can't Catch";

    // The same cheat as the admin trigger combo: blue fog, no catching and 99 minutes added to the run time.
    [MenuItem(MenuPath)]
    private static void ToggleGhostsCantCatch()
    {
        foreach (UdonBehaviour udonBehaviour in Object.FindObjectsOfType<UdonBehaviour>())
        {
            if (UdonSharpEditorUtility.GetUdonSharpBehaviourType(udonBehaviour) != typeof(AdminCheat)) continue;

            udonBehaviour.SendCustomEvent(nameof(AdminCheat._ToggleCheat));
            return;
        }

        throw new System.InvalidOperationException("No AdminCheat UdonBehaviour in the scene.");
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleGhostsCantCatchValidate()
    {
        return EditorApplication.isPlaying;
    }
}
