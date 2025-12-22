
using System;
using MMMaellon;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.Udon.Common.Interfaces;


[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class Snowball : UdonSharpBehaviour
{

    [Header("Respawn Settings")]
    public Vector3 respawnPosition;
    public float respawnDelay = 2f;

    [Header("Impact Effects")]
    public GameObject impactParticles;
    public float minImpactVelocity = 2f;

    [Header("Collider Setup")]
    public SphereCollider pickupCollider;
    public SphereCollider physicsCollider;

    [Header("Network sync")]
    [UdonSynced] private Vector3 syncedImpactPosition;
    [UdonSynced] private Vector3 syncedImpactNormal;
    [UdonSynced] private int syncedImpactEventId;
    // [UdonSynced] private int syncedRespawnEventId = 0;
    private int lastHandledImpactEventId = 0;
    //     private int lastHandledRespawnEventId = 0;

    private bool hasImpacted = false;
    public VRCPickup pickup;
    private Rigidbody rb;
    private MeshRenderer meshRenderer;
    private bool hasInitialized = false;
    private TextMeshPro textMeshPro;
    private Transform textMeshProTransform;
    private SmartObjectSync smartObjectSync;

    private void Start()
    {
        textMeshPro = GetComponentInChildren<TextMeshPro>();
        lastHandledImpactEventId = syncedImpactEventId;
        // lastHandledRespawnEventId = syncedRespawnEventId;
        Log($"Startup. LastHandledImpactEventId: {lastHandledImpactEventId}");

        pickup = GetComponent<VRCPickup>();
        rb = GetComponent<Rigidbody>();
        meshRenderer = GetComponent<MeshRenderer>();

        respawnPosition = transform.position;

        if (rb)
        {
            rb.isKinematic = true;
        }

        if (pickupCollider)
        {
            pickupCollider.enabled = true;
            pickupCollider.isTrigger = true;
        }

        //if (physicsCollider != null)
        //{
        //    physicsCollider.enabled = false;
        //}

        smartObjectSync = GetComponent<SmartObjectSync>();
        if (textMeshPro)
        {
            textMeshProTransform = textMeshPro.gameObject.transform; // Cache it

        }
    }

    public override void OnPickup()
    {
        Log($"OnPicking");
        Log($"OnPicking");

        hasImpacted = false;

        if (rb != null)
        {
            rb.isKinematic = false;
        }

        if (physicsCollider != null)
        {
            physicsCollider.enabled = true;
        }
    }

    public override void OnPlayerCollisionEnter(VRCPlayerApi vrcPlayer)
    {
        Log($"OnPlayerCollisionEnter: {vrcPlayer.displayName}");

        if (!Networking.IsOwner(gameObject))
        {
            return;
        }

        if (pickup.IsHeld)
        {
            return;
        }

        if (hasImpacted)
        {
            return;
        }

        if (rb.velocity.magnitude < minImpactVelocity)
        {
            return;
        }

        hasImpacted = true;

        if (impactParticles != null)
        {
            // No impact point info available here, so just use current position and make the particles fly opposite to velocity
            syncedImpactPosition = transform.position;
            syncedImpactNormal = -rb.velocity.normalized;
            syncedImpactEventId++;
            lastHandledImpactEventId = syncedImpactEventId;
            RequestSerialization();
            Debug.Log($"OnPlayerCollision: Request serialization syncedImpactEventId: {lastHandledImpactEventId} ");
        }
        Log($"Disabling snowball {gameObject.name} for owner.");
        DisableSnowball();
        CreateSnowballParticles();
        SendCustomEventDelayedSeconds(nameof(Respawn), respawnDelay);
        SendCustomEventDelayedSeconds(nameof(NetworkEnableSnowball), respawnDelay + 1);

    }

    public override void OnDrop()
    {
        Log($"OnDrop - Owner: {Networking.IsOwner(gameObject)}, IsKinematic: {rb.isKinematic}");
    }
    public override void OnDeserialization()
    {
        //  Log("OnDeserialization");
        if (!hasInitialized)
        {
            hasInitialized = true;
            lastHandledImpactEventId = syncedImpactEventId;
            Log($"Initialized lastHandledImpactEventId to {syncedImpactEventId}");
        }
        // Log($"OnDeserialization");

        // Since syncing variables across the network is slower
        // than sending network events, we can't rely on network events
        // to create the snowball particles because it is possible that the
        // function would be called before we have new values for syncedImpactPosition
        // and normal and it would spawn particles on a previous position. instead
        // solve it incrementing a synced syncedImpactCounter variable every time we want
        // to spawn particles and then when a client checks for latest messages on network
        // it checks if the variable has been updated to a newer value. If it ha
        // we know we have received a new impact position that particles has not yet been spawned
        // at yet for this client, and can perform the spawning.
        if (syncedImpactEventId > lastHandledImpactEventId)
        {
            Log($"OnDeserialization: Got network request to disable snowball. syncedImpactEventId: {syncedImpactEventId} LastHandledImpactEventId: {lastHandledImpactEventId} {gameObject.name}");
            DisableSnowball();
            CreateSnowballParticles();
            lastHandledImpactEventId = syncedImpactEventId;
        }
        //if (syncedRespawnEventId != lastHandledRespawnEventId)
        //{
        //    Debug.Log($"syncedRespawnEventId update: {syncedRespawnEventId} lasHandledRespawnEventId: {lastHandledRespawnEventId}");
        //    EnableSnowball();
        //    lastHandledRespawnEventId = syncedRespawnEventId;
        //}
    }

    private void OnCollisionEnter(Collision collision)
    {

        if (!Networking.IsOwner(gameObject))
        {
            return;
        }

        if (pickup.IsHeld)
        {
            return;
        }

        if (hasImpacted)
        {
            return;
        }

        //if (collision.relativeVelocity.magnitude < minImpactVelocity)
        //{
        //    return;
        //}

        hasImpacted = true;

        if (impactParticles != null)
        {
            if (collision.contacts.Length > 0)
            {
                syncedImpactPosition = collision.contacts[0].point;
                syncedImpactNormal = collision.contacts[0].normal;
                syncedImpactEventId++;
                lastHandledImpactEventId = syncedImpactEventId;
                RequestSerialization();
                Debug.Log($"Request serialization syncedImpactEventId: {lastHandledImpactEventId} ");
            }
        }
        Log($"Disabling snowball {gameObject.name} for owner.");
        DisableSnowball();
        CreateSnowballParticles();
        SendCustomEventDelayedSeconds(nameof(Respawn), respawnDelay);
        SendCustomEventDelayedSeconds(nameof(NetworkEnableSnowball), respawnDelay + 1);
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        Log($"Ownership transferred to: {player.displayName}");
    }
    public void CreateSnowballParticles()
    {
        if (impactParticles != null)
        {
            GameObject particles = Instantiate(impactParticles);
            if (particles != null)
            {
                particles.transform.position = syncedImpactPosition;
                particles.transform.rotation = Quaternion.LookRotation(syncedImpactNormal);
            }
        }
    }

    public void DisableSnowball()
    {
        if (meshRenderer != null)
        {
            meshRenderer.enabled = false;
        }

        if (textMeshPro)
        {
            textMeshPro.text = $"{meshRenderer.enabled}";
        }

        if (physicsCollider != null)
        {
            physicsCollider.enabled = false;
        }

        if (pickup != null)
        {
            pickup.pickupable = false;
        }
    }

    public void EnableSnowball()
    {
        Log("Got call to to enable snowball.");
        if (meshRenderer != null)
        {
            meshRenderer.enabled = true;
            if (textMeshPro)
            {
                textMeshPro.text = $"{meshRenderer.enabled}";
            }
        }

        if (pickupCollider != null)
        {
            pickupCollider.enabled = true;
        }

        if (physicsCollider != null)
        {
            physicsCollider.enabled = false;
        }

        if (pickup != null)
        {
            pickup.pickupable = true;
        }
    }
    public void Respawn()
    {
        if (!Networking.IsOwner(gameObject)) return;

        //transform.position = respawnPosition;
        //transform.rotation = Quaternion.identity;

        //if (rb != null)
        //{
        //    rb.velocity = Vector3.zero;
        //    rb.angularVelocity = Vector3.zero;
        //}

        if (smartObjectSync)
        {
            smartObjectSync.Respawn();
            rb.isKinematic = true;

        }
        else
        {
            Log("No SmartObjectSync on snowball.", "Err");
        }
                
        // syncedRespawnEventId++;
        // lastHandledRespawnEventId = syncedRespawnEventId;
        Log("Respawn");
    }

    public void NetworkEnableSnowball()
    {
        Log("NetworkEnableSnowball: Owner sending network event to enable snowball.");
        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(EnableSnowball));
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

    public void Update()
    {
        if (textMeshPro && textMeshProTransform)
        {
            string displayText = "";

            if (rb.velocity.magnitude > 0)
            {
                displayText += $"velocity: {rb.velocity}\n";
            }

            displayText += $"kinematic: {rb.isKinematic}";
            textMeshPro.text = displayText;

            if (Networking.LocalPlayer != null)
            {
                Vector3 headPos = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
                Vector3 direction = headPos - textMeshProTransform.position;
                direction.y = 0;
                textMeshProTransform.rotation = Quaternion.LookRotation(-direction);
                // Position text offset from camera, with minimum height
                Vector3 targetPos = transform.position - direction.normalized * 0.8f;
                targetPos.y = Mathf.Max(targetPos.y, transform.position.y + 0.4f);
                textMeshProTransform.position = targetPos;
            }
        }
    }
}
