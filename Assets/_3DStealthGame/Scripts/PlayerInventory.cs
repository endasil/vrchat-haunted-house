using Assets._3DStealthGame.Scripts;
using Assets._3DStealthGame.Scripts.Enums;

using UdonSharp;

using UnityEngine;

// Sync mode None, so other clients can't call AddNextMissingPill or ResetState with a network event.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PlayerInventory : Resettable
{
    // One entry per PillColor, indexed by (int)PillColor.
    private bool[] _hasPill = new bool[(int)PillColor.LastEnum];

    private PlayerUI _playerUI;

    private ExitPillSockets _exitPillSockets;

    private AudioSource _pickupAudio;

    public override void Start()
    {
        base.Start();

        _pickupAudio = GetComponent<AudioSource>();

        if (_pickupAudio == null)
            Debug.LogError($"PlayerInventory ({gameObject.name}): no AudioSource, the pill pickup sound won't play.");
    }

    public void SetPlayerUI(PlayerUI playerUI)
    {
        _playerUI = playerUI;
    }

    public void SetExitPillSockets(ExitPillSockets exitPillSockets)
    {
        _exitPillSockets = exitPillSockets;
    }

    public bool HasPill(PillColor pillColor)
    {
        return _hasPill[(int)pillColor];
    }


    public bool HasAllPills()
    {
        for (int i = 0; i < (int)PillColor.LastEnum; i++)
        {
            if (!_hasPill[i])
            {
                return false;
            }
        }

        return true;
    }

    public void AddPill(PillColor pillColor)
    {
        _hasPill[(int)pillColor] = true;

        if (_pickupAudio != null)
            _pickupAudio.Play();

        if (_exitPillSockets == null)
        {
            Debug.LogError($"PlayerInventory ({gameObject.name}): no ExitPillSockets is hooked up, the exit socket won't show the pill.");
        }
        else
        {
            _exitPillSockets.ShowPill(pillColor);
        }

        if (_playerUI == null)
        {
            Debug.LogError($"PlayerInventory ({gameObject.name}): no PlayerUI is hooked up, the pill icon won't light up.");
            return;
        }

        _playerUI.ShowPillIcon(pillColor);
    }

    public void AddNextMissingPill()
    {
        for (int i = 0; i < (int)PillColor.LastEnum; i++)
        {
            if (!_hasPill[i])
            {
                AddPill((PillColor)i);
                return;
            }
        }
    }

    public override void ResetState()
    {
        for (int i = 0; i < _hasPill.Length; i++)
        {
            _hasPill[i] = false;
        }
    }
}
