using Assets._3DStealthGame.Scripts;

using UnityEngine;

public class PlayerInventory : Resettable
{
    // One count per PillColor, indexed by (int)PillColor.
    private int[] _pillCounts = new int[(int)PillColor.LastEnum];

    // The HUD, handed to us by PlayerUI once it finds the local player's inventory.
    private PlayerUI _playerUI;

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

    public bool HasPill(PillColor pillColor)
    {
        return GetPillCount(pillColor) > 0;
    }


    public bool HasAllPills()
    {
        for (int i = 0; i < (int)PillColor.LastEnum; i++)
        {
            if (_pillCounts[i] <= 0)
            {
                return false;
            }
        }

        return true;
    }

    public void AddPill(PillColor pillColor)
    {
        _pillCounts[(int)pillColor]++;

        if (_pickupAudio != null)
            _pickupAudio.Play();

        if (_playerUI == null)
        {
            Debug.LogError($"PlayerInventory ({gameObject.name}): no PlayerUI is hooked up, the pill icon won't light up.");
            return;
        }

        _playerUI.ShowPillIcon(pillColor);
    }

    public int GetPillCount(PillColor pillColor)
    {
        return _pillCounts[(int)pillColor];
    }

    public override void ResetState()
    {
        for (int i = 0; i < _pillCounts.Length; i++)
            _pillCounts[i] = 0;
    }
}
