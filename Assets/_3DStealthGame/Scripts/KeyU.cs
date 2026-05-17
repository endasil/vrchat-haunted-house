using Assets._3DStealthGame.Scripts;

using UnityEngine;

using VRC.SDKBase;

public class KeyU : Resettable
{
    public KeyType pillColor;
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

        PlayerInvenory playerInfoScript = playerObjects[0].GetComponent<PlayerInvenory>();

        if (playerInfoScript != null)
        {
            playerInfoScript.AddKey(pillColor);
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
