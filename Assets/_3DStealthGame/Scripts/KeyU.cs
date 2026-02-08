using Assets._3DStealthGame.Scripts;

using UnityEngine;

using VRC.SDKBase;

public class KeyU : Resettable
{
    public KeyType keyType;
    public ResetManager resetManager;
    private VRCPlayerApi localPlayer;

    public override void Start()
    {
        localPlayer = Networking.LocalPlayer;
        base.Start();
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player != localPlayer)
            return;

        Debug.Log("Key trigger");
        GameObject[] playerObjects = player.GetPlayerObjects();

        PlayerMovementU playerInfoScript = playerObjects[0].GetComponent<PlayerMovementU>();

        if (playerInfoScript != null)
        {
            playerInfoScript.AddKey(keyType);
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
