#pragma warning disable UNT0026 // GetComponent allocatess even if no component found, no longer true in 2019.3+
using Assets._3DStealthGame.Scripts;
using Assets._3DStealthGame.Scripts.Enums;

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
        if (!player.isLocal) return;

        PlayerInventory playerInventory = player.GetPlayerObjects()[0].GetComponent<PlayerInventory>();

        if (playerInventory == null)
        {
            Debug.LogError($"{gameObject.name}: Unable to find PlayerInventory script on player object");
            return;
        }

        playerInventory.AddPill(PillColor);
        gameObject.SetActive(false);
    }

    public override void ResetState()
    {
        gameObject.SetActive(true);
    }
}
