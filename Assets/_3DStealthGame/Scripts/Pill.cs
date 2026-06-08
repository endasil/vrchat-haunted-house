#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable UNT0026 // GetComponent allocatess even if no component found, no longer true in 2019.3+
using Assets._3DStealthGame.Scripts;

using UdonSharp;

using UnityEngine;

using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class Pill : Resettable
{
    public PillColor PillColor;
    private ResetManager _resetManager;
    private VRCPlayerApi _localPlayer;

    public override void Start()
    {
        _localPlayer = Networking.LocalPlayer;
        base.Start();
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player != _localPlayer)
            return;

        Debug.Log("Key trigger");
        GameObject[] playerObjects = player.GetPlayerObjects();

        PlayerInventory playerInfoScript = playerObjects[0].GetComponent<PlayerInventory>();

        if (playerInfoScript != null)
        {
            playerInfoScript.AddKey(PillColor);
            Debug.Log("Adding key");
            gameObject.SetActive(false);
        }
        else
        {
            Debug.LogError("Unable to find PlayerInfoScript");
        }
    }
    public void ResetState()
    {
        Debug.Log(gameObject.name + "ResetState");
        gameObject.SetActive(true);
    }
}
