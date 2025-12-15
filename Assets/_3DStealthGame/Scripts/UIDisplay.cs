
using System;
using TMPro;

using UdonSharp;

using UnityEngine;

using VRC.SDKBase;
using VRC.Udon;

public class UIDisplay : UdonSharpBehaviour
{
    public PlayerMovementU keyManager;
    public TextMeshProUGUI keyText;
    public Canvas canvas;
    private VRCPlayerApi localPlayer;

    void Start()
    {
        localPlayer = Networking.LocalPlayer;
    }

    private void LateUpdate()
    {
        keyText.text = keyText.text = $"Keys: {keyManager.m_KeyCount}";
    }
}
