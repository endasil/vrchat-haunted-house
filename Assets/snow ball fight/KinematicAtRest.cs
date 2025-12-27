using UnityEngine;
using VRC.SDKBase;
using MMMaellon;

using System;


public class KinematicAtRest : SmartObjectSyncListener
{
    private Rigidbody rb;

    public void Start()
    {
        rb = GetComponent<Rigidbody>();
        SmartObjectSync sync = GetComponent<SmartObjectSync>();
        if (sync)
        {
            sync.AddListener(this);
            Log("Registered listener with SmartObjectSync");
        }
        else
        {
            Log("NO SmartObjectSync FOUND", "err");
        }
    }

    public override void OnChangeState(SmartObjectSync sync, int oldState, int newState)
    {
        // Enable physics while flying
        if (newState == SmartObjectSync.STATE_INTERPOLATING ||
            newState == SmartObjectSync.STATE_FALLING)
        {
            Log($"OnChangeState: {sync.StateToString(newState)} Removing kinematic", "warn");
            rb.isKinematic = false;
            return;
        }

        // Disable physics when respawned / teleported / sleeping
        if (newState == SmartObjectSync.STATE_TELEPORTING ||
            newState == SmartObjectSync.STATE_SLEEPING)
        {
            Log($"OnChangeState: {sync.StateToString(newState)} Setting to kinematic", "warn");

            rb.isKinematic = true;
        }
    }

    private void Log(string message, string level = "Log")
    {
        VRCPlayerApi owner = Networking.GetOwner(gameObject);
        string ownerName = owner != null ? owner.displayName : "None";
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        string formattedMessage = $"<color=cyan>[{timestamp}]</color>[{gameObject.name}][Owner: {ownerName}] {message}";

        string levelLower = level.ToLower();

        if (levelLower == "warning" || levelLower == "warn")
            Debug.LogWarning(formattedMessage);
        else if (levelLower == "error" || levelLower == "err")
            Debug.LogError(formattedMessage);
        else if (levelLower == "log")
            Debug.Log(formattedMessage);
        else
            Debug.LogError($"{levelLower} is an unknown log level! " + formattedMessage);
    }


    public override void OnChangeOwner(SmartObjectSync sync, VRCPlayerApi oldOwner, VRCPlayerApi newOwner) { }
}
