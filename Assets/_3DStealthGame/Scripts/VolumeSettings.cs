using TMPro;

using UdonSharp;

using UnityEngine;
using UnityEngine.UI;

using VRC.SDKBase;
using VRC.SDK3.Persistence;

using _3DStealthGame.Scripts.Constants;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class VolumeSettings : UdonSharpBehaviour
{
    private const float SaveDelaySeconds = 1f;
    private const float PercentPerVolume = 100f;

    public Slider seekerSlider;
    public Slider musicSlider;
    public Slider otherSlider;

    public TextMeshProUGUI seekerValueText;
    public TextMeshProUGUI musicValueText;
    public TextMeshProUGUI otherValueText;

    [HideInInspector] public AudioSource[] seekerAudio;
    [HideInInspector] public MusicFader musicFader;
    [HideInInspector] public AudioSource[] otherAudio;
    [HideInInspector] public VolumeSettings[] linkedSettings;

    private AudioSource[] _playerObjectAudio;

    private bool _isSetUp;
    private bool _isSyncing;
    private bool _isRestored;
    private bool _isLoading;
    private bool _isSavePending;

    private void Start()
    {
        if (seekerSlider == null || musicSlider == null || otherSlider == null
            || seekerValueText == null || musicValueText == null || otherValueText == null)
        {
            Debug.LogError($"VolumeSettings ({gameObject.name}): the three sliders and the three value texts need to be assigned in the inspector.", gameObject);
            return;
        }

        if (seekerAudio == null || seekerAudio.Length == 0
            || musicFader == null || otherAudio == null || otherAudio.Length == 0)
        {
            Debug.LogError($"VolumeSettings ({gameObject.name}): seekerAudio, musicFader and otherAudio are empty. VolumeSettingsAudioCollector fills them when Unity processes the scene; check the console for its error.", gameObject);
            return;
        }

        _playerObjectAudio = new AudioSource[0];

        seekerSlider.value = seekerAudio[0].volume;
        musicSlider.value = musicFader.defaultVolume;
        otherSlider.value = otherAudio[0].volume;

        _isSetUp = true;

        OnSeekerVolumeChanged();
        OnOtherVolumeChanged();
        ShowValue(musicValueText, musicSlider.value);
    }

    public override void OnPlayerRestored(VRCPlayerApi player)
    {
        if (!player.isLocal) return;
        if (!_isSetUp) return;

        CollectPlayerObjectAudio(player);

        _isLoading = true;
        if (PlayerData.TryGetFloat(player, PlayerDataKeys.SavedSeekerVolume, out float seekerVolume))
        {
            seekerSlider.value = seekerVolume;
        }
        if (PlayerData.TryGetFloat(player, PlayerDataKeys.SavedMusicVolume, out float musicVolume))
        {
            musicSlider.value = musicVolume;
        }
        if (PlayerData.TryGetFloat(player, PlayerDataKeys.SavedOtherVolume, out float otherVolume))
        {
            otherSlider.value = otherVolume;
        }
        _isLoading = false;

        _isRestored = true;

        OnSeekerVolumeChanged();
        OnMusicVolumeChanged();
        OnOtherVolumeChanged();
    }

    public void OnSeekerVolumeChanged()
    {
        if (!_isSetUp) return;

        ShowValue(seekerValueText, seekerSlider.value);
        if (_isSyncing) return;

        ApplyVolume(seekerAudio, seekerSlider.value);
        ScheduleSave();
        SyncLinkedSettings();
    }

    public void OnMusicVolumeChanged()
    {
        if (!_isSetUp) return;

        ShowValue(musicValueText, musicSlider.value);
        if (_isSyncing) return;

        musicFader.SetVolume(musicSlider.value);
        ScheduleSave();
        SyncLinkedSettings();
    }

    public void OnOtherVolumeChanged()
    {
        if (!_isSetUp) return;

        ShowValue(otherValueText, otherSlider.value);
        if (_isSyncing) return;

        ApplyVolume(otherAudio, otherSlider.value);
        ApplyVolume(_playerObjectAudio, otherSlider.value);
        ScheduleSave();
        SyncLinkedSettings();
    }

    public void ShowLinkedVolumes(float seekerVolume, float musicVolume, float otherVolume)
    {
        _isSyncing = true;
        seekerSlider.value = seekerVolume;
        musicSlider.value = musicVolume;
        otherSlider.value = otherVolume;
        _isSyncing = false;
    }

    private void SyncLinkedSettings()
    {
        foreach (VolumeSettings linked in linkedSettings)
        {
            linked.ShowLinkedVolumes(seekerSlider.value, musicSlider.value, otherSlider.value);
        }
    }

    public void _SaveVolumes()
    {
        _isSavePending = false;

        PlayerData.SetFloat(PlayerDataKeys.SavedSeekerVolume, seekerSlider.value);
        PlayerData.SetFloat(PlayerDataKeys.SavedMusicVolume, musicSlider.value);
        PlayerData.SetFloat(PlayerDataKeys.SavedOtherVolume, otherSlider.value);
    }

    private void ScheduleSave()
    {
        if (_isLoading || !_isRestored || _isSavePending) return;

        _isSavePending = true;
        SendCustomEventDelayedSeconds(nameof(_SaveVolumes), SaveDelaySeconds);
    }

    private void CollectPlayerObjectAudio(VRCPlayerApi player)
    {
        GameObject[] playerObjects = player.GetPlayerObjects();

        int count = 0;
        foreach (GameObject playerObject in playerObjects)
        {
            count += playerObject.GetComponentsInChildren<AudioSource>(true).Length;
        }

        _playerObjectAudio = new AudioSource[count];
        int index = 0;
        foreach (GameObject playerObject in playerObjects)
        {
            foreach (AudioSource source in playerObject.GetComponentsInChildren<AudioSource>(true))
            {
                _playerObjectAudio[index] = source;
                index++;
            }
        }
    }

    private void ApplyVolume(AudioSource[] sources, float volume)
    {
        foreach (AudioSource source in sources)
        {
            source.volume = volume;
        }
    }

    private void ShowValue(TextMeshProUGUI valueText, float volume)
    {
        valueText.text = $"{Mathf.RoundToInt(volume * PercentPerVolume)}%";
    }
}
