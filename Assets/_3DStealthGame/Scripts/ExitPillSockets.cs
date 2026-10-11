using Assets._3DStealthGame.Scripts;

using UdonSharp;

using UnityEngine;

using VRC.SDKBase;

// Shows a pill in each socket on the exit door when the local player holds a pill of that color.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ExitPillSockets : Resettable
{
    private const int PillTypeCount = (int)PillColor.LastEnum;

    public GameObject[] socketPills;

    public override void Start()
    {
        base.Start();

        if (socketPills == null || socketPills.Length != PillTypeCount)
        {
            Debug.LogError($"ExitPillSockets ({gameObject.name}): socketPills must have {PillTypeCount} entries (one per PillColor), found {(socketPills == null ? 0 : socketPills.Length)}.");
            return;
        }

        for (int i = 0; i < PillTypeCount; i++)
        {
            if (socketPills[i] == null)
            {
                Debug.LogError($"ExitPillSockets ({gameObject.name}): socket pill for {(PillColor)i} is not assigned in the inspector.");
            }
        }
    }

    public override void OnPlayerRestored(VRCPlayerApi player)
    {
        if (!player.isLocal) return;

        PlayerInventory playerInventory = player.GetPlayerObjects()[0].GetComponent<PlayerInventory>();
        if (playerInventory == null)
        {
            Debug.LogError($"ExitPillSockets ({gameObject.name}): local player object has no PlayerInventory component.");
            return;
        }

        playerInventory.SetExitPillSockets(this);

        // The player can hold pills before OnPlayerRestored runs, so show those pills here.
        for (int i = 0; i < PillTypeCount; i++)
        {
            socketPills[i].SetActive(playerInventory.HasPill((PillColor)i));
        }
    }

    public void ShowPill(PillColor pillColor)
    {
        socketPills[(int)pillColor].SetActive(true);
    }

    public override void ResetState()
    {
        for (int i = 0; i < PillTypeCount; i++)
        {
            socketPills[i].SetActive(false);
        }
    }
}
