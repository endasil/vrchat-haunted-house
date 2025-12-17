
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.Udon.Common.Interfaces;

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
    [UdonSynced] private int syncedImpactEventId = 0;
    private int lastHandledImpactEventId = 0;
    [UdonSynced] private int syncedRespawnEventId = 0;
    private int lastHandledRespawnEventId = 0;

    private bool hasImpacted = false;
    public VRCPickup pickup;
    private Rigidbody rb;
    private MeshRenderer meshRenderer;


    private void Start()
    {
        lastHandledImpactEventId = syncedImpactEventId;
        lastHandledRespawnEventId = syncedRespawnEventId;
        Debug.Log($"Startup. LastHandledImpactEventId: {lastHandledImpactEventId}");


        pickup = (VRCPickup)GetComponent(typeof(VRCPickup));
        rb = GetComponent<Rigidbody>();
        meshRenderer = GetComponent<MeshRenderer>();

        respawnPosition = transform.position;

        if (rb != null)
        {
            rb.isKinematic = true;
        }

        if (pickupCollider != null)
        {
            pickupCollider.enabled = true;
            pickupCollider.isTrigger = true;
        }

        if (physicsCollider != null)
        {
            physicsCollider.enabled = false;
        }
    }

    public override void OnPickup()
    {
        Debug.Log("OnPicking");
        Debug.LogWarning("OnPicking");
        Debug.LogError("OnPicking");

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
    public override void OnDeserialization()
    {
        Debug.Log("OnDeserialization");

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
        if (syncedImpactEventId != lastHandledImpactEventId)
        {
            Debug.Log($"syncedImpactEventId: {syncedImpactEventId} LastHandledImpactEventId: {lastHandledImpactEventId}");
            DisableSnowball();
            CreateSnowballParticles();
            lastHandledImpactEventId = syncedImpactEventId;
        }
        if (syncedRespawnEventId != lastHandledRespawnEventId)
        {
            Debug.Log($"syncedRespawnEventId update: {syncedRespawnEventId} lasHandledRespawnEventId: {lastHandledRespawnEventId}");
            EnableSnowball();
            lastHandledRespawnEventId = syncedRespawnEventId;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {

        if (!Networking.IsOwner(gameObject))
        {
            Debug.Log("Not owner, returning");
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

        if (collision.relativeVelocity.magnitude < minImpactVelocity)
        {
            return;
        }

        hasImpacted = true;

        if (impactParticles != null)
        {
            if (collision.contacts.Length > 0)
            {
                syncedImpactPosition = collision.contacts[0].point;
                syncedImpactNormal = collision.contacts[0].normal;
                syncedImpactEventId++;
                lastHandledImpactEventId = syncedImpactEventId;
            }
        }

        DisableSnowball();
        CreateSnowballParticles();
        SendCustomEventDelayedSeconds(nameof(Respawn), respawnDelay);

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
        if (meshRenderer != null)
        {
            meshRenderer.enabled = true;
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
        
        transform.position = respawnPosition;
        transform.rotation = Quaternion.identity;

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
        syncedRespawnEventId++;
        lastHandledRespawnEventId = syncedRespawnEventId;
        RequestSerialization();
        EnableSnowball();
    }

    public void NetworkEnableSnowball()
    {
        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(EnableSnowball));
    }
}
