#pragma warning disable UNT0026 // GetComponent allocatess even if no component found, no longer true in 2019.3+
using Assets._3DStealthGame.Scripts;

using UdonSharp;

using UnityEngine;

using VRC.SDKBase;
[DisallowMultipleComponent]
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class Pill : Resettable
{
    public PillColor PillColor;

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (!player.isLocal)
            return;

        GameObject[] playerObjects = player.GetPlayerObjects();

        PlayerInventory playerInventory = playerObjects[0].GetComponent<PlayerInventory>();

        if (playerInventory != null)
        {
            playerInventory.AddPill(PillColor);
            gameObject.SetActive(false);
        }
        else
        {
            Debug.LogError($"{gameObject.name}: Unable to find PlayerInventory script on player object");
        }
    }

    public override void ResetState()
    {
        gameObject.SetActive(true);
    }
}
