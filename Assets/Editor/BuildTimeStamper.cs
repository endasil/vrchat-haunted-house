#if UNITY_EDITOR
using System;

using TMPro;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

using UdonSharpEditor;

using VRC.SDKBase.Editor.BuildPipeline;

// Runs right before the VRChat SDK builds/uploads the world. It writes the
// current UTC time into every BuildTimeDisplay in the open scenes, pushes that
// value into the backing UdonBehaviour, and saves the scene so the build picks
// it up.
public class BuildTimeStamper : IVRCSDKBuildRequestedCallback
{
    // Lower runs earlier. Keep it low so we stamp before UdonSharp serializes.
    public int callbackOrder => -100;

    public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
    {
        if (requestedBuildType != VRCSDKRequestedBuildType.Scene)
        {
            return true;
        }

        string stamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC";
        bool stampedAny = false;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (BuildTimeDisplay display in root.GetComponentsInChildren<BuildTimeDisplay>(true))
                {
                    display.buildTime = stamp;
                    UdonSharpEditorUtility.CopyProxyToUdon(display);
                    EditorUtility.SetDirty(display);

                    // Write the text onto the TMP now so the last build time
                    // shows in the editor without entering play mode.
                    TextMeshPro label = display.label;
                    if (label == null)
                    {
                        label = display.GetComponent<TextMeshPro>();
                    }
                    if (label != null)
                    {
                        label.text = "Built " + stamp;
                        EditorUtility.SetDirty(label);
                    }

                    stampedAny = true;
                }
            }

            if (stampedAny)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        if (stampedAny)
        {
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"BuildTimeStamper: stamped build time {stamp}");
        }

        return true;
    }
}
#endif
