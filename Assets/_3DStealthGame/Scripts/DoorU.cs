using Assets._3DStealthGame.Scripts;

using UdonSharp;

using UnityEngine;

using VRC.SDKBase;
using VRC.Udon;

public class DoorU : Resettable
{
    public float openDuration = 1f;
    private bool opened = false;
    private bool opening;
    private float timer;
    private Quaternion startRot;
    private Quaternion targetRot;
    public ResetManager resetManager;
    public KeyType keyType;

    public override void Start()
    {
        startRot = transform.localRotation;
        targetRot = Quaternion.Euler(-90f, startRot.eulerAngles.y, startRot.eulerAngles.z);
        base.Start();
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (!player.isLocal || opened) return;

        GameObject[] playerObjects = player.GetPlayerObjects();

        PlayerMovementU playerInventory = playerObjects[0].GetComponent<PlayerMovementU>();

        if (playerInventory != null)
        {
            if (playerInventory.UseKey(keyType))
            {
                Debug.Log($"Key {keyType} used");
                timer = 0f;
                opening = true;
            }
            else
            {
                Debug.Log($"No {keyType} key in player inventory. ");
                return;
            }
        }
        else
        {
            Debug.LogError("Unable to find PlayerMovementU script on player object");
        }
    }

    void Update()
    {
        if (!opening || opened) return;

        timer += Time.deltaTime;
        float normalizedTime = Mathf.Clamp01(timer / openDuration);

        transform.localRotation = Quaternion.Slerp(startRot, targetRot, normalizedTime );

        if (normalizedTime  >= 1f)
        {
            opened = true;
            opening = false;
        }
    }

    public void ResetState()
    {
        Debug.Log(gameObject.name + "ResetState");
        opening = false;
        opened = false;
        timer = 0f;
        transform.localRotation = startRot;
    }
}