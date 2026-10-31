using System.Linq;

using UdonSharpEditor;

using UnityEditor.Build;
using UnityEditor.Build.Reporting;

using UnityEngine;
using UnityEngine.SceneManagement;

public class VolumeSettingsAudioCollector : IProcessSceneWithReport
{
    public int callbackOrder => 0;

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        GameObject[] roots = scene.GetRootGameObjects();

        VolumeSettings[] volumeSettings = roots.SelectMany(root => root.GetComponentsInChildren<VolumeSettings>(true)).ToArray();
        if (volumeSettings.Length == 0) return;

        MusicFader[] musicFaders = roots.SelectMany(root => root.GetComponentsInChildren<MusicFader>(true)).ToArray();
        if (musicFaders.Length != 1)
        {
            Debug.LogError($"VolumeSettingsAudioCollector: the scene needs exactly one MusicFader, it has {musicFaders.Length}.");
            return;
        }

        MusicFader musicFader = musicFaders[0];
        AudioSource musicAudio = musicFader.GetComponent<AudioSource>();

        AudioSource[] allAudio = roots
            .SelectMany(root => root.GetComponentsInChildren<AudioSource>(true))
            .Where(source => source != musicAudio && source.GetComponentInParent<PlayerInventory>(true) == null)
            .ToArray();

        AudioSource[] seekerAudio = allAudio.Where(source => source.GetComponentInParent<SeekerGhost>(true) != null).ToArray();
        AudioSource[] otherAudio = allAudio.Except(seekerAudio).ToArray();

        ReportDifferentVolumes("seeker", seekerAudio);
        ReportDifferentVolumes("other", otherAudio);

        foreach (VolumeSettings settings in volumeSettings)
        {
            settings.seekerAudio = seekerAudio;
            settings.musicFader = musicFader;
            settings.otherAudio = otherAudio;
            settings.linkedSettings = volumeSettings.Where(other => other != settings).ToArray();
            UdonSharpEditorUtility.CopyProxyToUdon(settings);
        }

        Debug.Log($"VolumeSettingsAudioCollector: {volumeSettings.Length} volume signs, {seekerAudio.Length} seeker sources,{otherAudio.Length} other sources, music on {musicFader.gameObject.name}.");
    }

    private static void ReportDifferentVolumes(string group, AudioSource[] sources)
    {
        if (sources.Length == 0) return;

        float expectedVolume = sources[0].volume;
        AudioSource[] different = sources.Where(source => !Mathf.Approximately(source.volume, expectedVolume)).ToArray();
        if (different.Length == 0) return;

        string differentList = string.Join(", ", different.Select(source => $"{GetPath(source.transform)} ({source.volume})"));
        Debug.LogError($"VolumeSettingsAudioCollector: all {group} sources need the same volume as {GetPath(sources[0].transform)} ({expectedVolume}). These differ: {differentList}");
    }

    private static string GetPath(Transform transform)
    {
        if (transform.parent == null)
        {
            return transform.name;
        }

        return GetPath(transform.parent) + "/" + transform.name;
    }
}
