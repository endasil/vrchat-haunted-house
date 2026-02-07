
using System;

using Assets._3DStealthGame.Scripts;

using UdonSharp;

using UnityEngine;

using VRC.SDKBase;
using VRC.Udon;

public class KeyU : Resettable
{
    public KeyType keyType;
    public ResetManager resetManager;

    public override void Start()
    {
        base.Start();
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        Debug.Log("Key trigger");
        GameObject[] playerObjects = player.GetPlayerObjects();

        PlayerMovementU playerInfoScript = null;
        for (int i = 0; i < playerObjects.Length; i++)
        {
            if (!Utilities.IsValid(playerObjects[i])) continue;
            PlayerMovementU foundScript = playerObjects[i].GetComponent<PlayerMovementU>();
            if (Utilities.IsValid(foundScript)) playerInfoScript = foundScript;
        }

        if (playerInfoScript != null)
        {
            playerInfoScript.AddKey(keyType);
            Debug.Log("Adding key");
            gameObject.SetActive(false);
        }
    }
    public void ResetState()
    {
        Debug.Log(gameObject.name +  "ResetState");
        gameObject.SetActive(true);

    }
}
