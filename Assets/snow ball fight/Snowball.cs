
using MMMaellon;

using System;
using TMPro;

using UdonSharp;

using UnityEngine;

using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;

// Inherit from SmartObjectSyncListener so we can subscribe to OnChangeState
// events from SmartObjectSync. This way we can switch to non-kinematic state
// when the snowball is thrown so smart
public class Snowball : SmartObjectSyncListener
{

    [Header("RespawnAndEnable Settings")]
    public Vector3 respawnPosition;
    public float respawnDelay = 2f;

    [Header("Impact Effects")]
    public GameObject impactParticles;
    public float minImpactVelocity = 1.4f;

    [Header("Collider Setup")]
    public SphereCollider pickupCollider;
    public SphereCollider physicsCollider;

    [Header("Network sync")]
    [UdonSynced] private Vector3 _syncedImpactPosition;
    [UdonSynced] private Vector3 _syncedImpactNormal;
    [UdonSynced] private int _syncedImpactEventId;
    private SmartObjectSync _smartObjectSync;
    private int _lastHandledImpactEventId = 0;

    [Header("Ghost Interaction")]
    public GhostAISearching ghostAI;
    [UdonSynced] public Vector3 syncedThrowerPosition;
    [UdonSynced] public int syncedGhostHitEventId;

    private bool _hasImpacted = false;
    private VRCPickup _vrcPickup;
    private Rigidbody _rb;
    private MeshRenderer _meshRenderer;
    private bool _hasInitialized = false;
    private TextMeshPro _textMeshPro;
    private Transform _textMeshProTransform;
    private void Start()
    {
        _textMeshPro = GetComponentInChildren<TextMeshPro>();
        _lastHandledImpactEventId = _syncedImpactEventId;
        //Log($"Startup. LastHandledImpactEventId: {_lastHandledImpactEventId}");

        _vrcPickup = GetComponent<VRCPickup>();
        _rb = GetComponent<Rigidbody>();
        _meshRenderer = GetComponent<MeshRenderer>();

        respawnPosition = transform.position;

        if (_rb) _rb.isKinematic = true;

        if (pickupCollider)
        {
            pickupCollider.enabled = true;
            pickupCollider.isTrigger = true;
        }

        // While the snowball is in idle state, we do not want other snowballs to collide with it.
        if (physicsCollider) physicsCollider.enabled = false;

        _smartObjectSync = GetComponent<SmartObjectSync>();
        if (_smartObjectSync)
        {
            // We need to listen to state change events from SmartObjectSync and switch kinematic mode
            // there to have position syncing working correctly for other clients that does not own
            // the object. 
            _smartObjectSync.AddListener(this);
            //Log("Registered listener with SmartObjectSync");
        }
        else
        {
            //Log($"No SmartObjectSync found on {gameObject.name}!", "err");
        }


        if (_textMeshPro)
        {
            _textMeshProTransform = _textMeshPro.gameObject.transform; // Cache it

        }
    }

    public override void OnPickup()
    {
        Log($"OnPicking");

        _hasImpacted = false;

        // Need to turn off kinematic when picked up so that throwing works correctly.
        if (_rb) _rb.isKinematic = false;

        // Once a snowball is picked up, we want it to be able to collide with stuff again.
        if (physicsCollider != null) physicsCollider.enabled = true;
    }

    public override void OnPlayerCollisionEnter(VRCPlayerApi player)
    {
        // We don't have a collision point or normal when colliding with a player so
        // just use the center of the snowball and make the particles fly in the opposite
        // direction the snowball is traveling.
        if (!TryRegisterImpact(transform.position, -_rb.velocity.normalized))
            return;

        DisableSnowball();
        CreateSnowballParticles();

        SendCustomEventDelayedSeconds(nameof(RespawnAndEnable), respawnDelay);
    }

    // Called whenever there are network updates for this class.
    public override void OnDeserialization(DeserializationResult result)
    {
        //Log("OnDeserialization");
        if (!_hasInitialized)
        {
            _hasInitialized = true;
            _lastHandledImpactEventId = _syncedImpactEventId;
            //Log($"Initialized lastHandledImpactEventId to {_syncedImpactEventId}");
        }

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
        if (_syncedImpactEventId > _lastHandledImpactEventId)
        {
            //Log($"OnDeserialization: Got network request to disable snowball. syncedImpactEventId: {_syncedImpactEventId} LastHandledImpactEventId: {_lastHandledImpactEventId} {gameObject.name}");
            DisableSnowball();
            CreateSnowballParticles();
            _lastHandledImpactEventId = _syncedImpactEventId;
        }

    }

    private bool TryRegisterImpact(Vector3 pos, Vector3 normal)
    {
        if (!Networking.IsOwner(gameObject)) return false;
        if (_vrcPickup && _vrcPickup.IsHeld) return false;
        if (_hasImpacted) return false;
        if (_rb && _rb.velocity.magnitude < minImpactVelocity)
        {
            Log($"Impact velocity too low for collision {_rb.velocity.magnitude} < {minImpactVelocity} ");
            return false;
        }
        Log($"Impact velocity {_rb.velocity.magnitude} > {minImpactVelocity} ");

        _hasImpacted = true;

        _syncedImpactPosition = pos;
        _syncedImpactNormal = normal;
        _syncedImpactEventId++;
        _lastHandledImpactEventId = _syncedImpactEventId;
        RequestSerialization();
        return true;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.contacts.Length == 0)
            return;

        var contact = collision.contacts[0];

        bool isGhostHit = ghostAI != null && collision.gameObject == ghostAI.gameObject;

        if (!TryRegisterImpact(contact.point, contact.normal))
            return;

        DisableSnowball();
        CreateSnowballParticles();
        SendCustomEventDelayedSeconds(nameof(RespawnAndEnable), respawnDelay);

        if (isGhostHit)
            NotifyGhostHit();
    }

    private void NotifyGhostHit()
    {
        VRCPlayerApi thrower = Networking.GetOwner(gameObject);
        if (thrower == null || !thrower.IsValid()) return;
        syncedThrowerPosition = thrower.GetPosition();
        syncedGhostHitEventId++;
        RequestSerialization();
    }

    public void CreateSnowballParticles()
    {
        if (impactParticles != null)
        {
            GameObject particles = Instantiate(impactParticles);
            if (particles != null)
            {
                particles.transform.position = _syncedImpactPosition;
                particles.transform.rotation = Quaternion.LookRotation(_syncedImpactNormal);
            }
        }
        else
        {
            Log("No impact particles assigned!", "error");
        }
    }

    public void DisableSnowball()
    {
        if (_meshRenderer) _meshRenderer.enabled = false;

        if (_textMeshPro)
        {
            _textMeshPro.text = $"{_meshRenderer.enabled}";
        }

        if (physicsCollider) physicsCollider.enabled = false;

        if (_vrcPickup) _vrcPickup.pickupable = false;
    }

    public void EnableSnowball()
    {
        Log("Got call to to enable snowball.");
        if (_meshRenderer) _meshRenderer.enabled = true;
        if (_textMeshPro) _textMeshPro.text = $"{_meshRenderer.enabled}";
        if (pickupCollider) pickupCollider.enabled = true;
        if (_vrcPickup) _vrcPickup.pickupable = true;
    }
    public void RespawnAndEnable()
    {
        if (!Networking.IsOwner(gameObject)) return;

        if (_smartObjectSync)
        {
            Log("TeleportToWorldSpace");
            _smartObjectSync.TeleportToWorldSpace(respawnPosition, Quaternion.identity, Vector3.zero, Vector3.zero);
        }
        else
        {
            Log("No SmartObjectSync on snowball.", "Error");
        }

        Log("RespawnAndEnable");
        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(EnableSnowball));
    }


    // Automatically switch off kinematic when the object is moving and switch to kinematic
    // when it is at rest. SmartObjectSync would not correctly update the position of objects
    // that are kinematic on non owners system. Subscribing to the smartobjectsyncs state change
    // in start and toggling kinematic here makes sure that the toggling is happening for clients too.

    public override void OnChangeState(SmartObjectSync sync, int oldState, int newState)
    {
        // Enable physics while flying
        if (newState == SmartObjectSync.STATE_INTERPOLATING ||
            newState == SmartObjectSync.STATE_FALLING)
        {
            Log($"OnChangeState: {sync.StateToString(newState)} Removing kinematic", "warn");
            _rb.isKinematic = false;
            return;
        }

        // Disable physics when respawned / teleported / sleeping
        if (newState == SmartObjectSync.STATE_TELEPORTING ||
            newState == SmartObjectSync.STATE_SLEEPING)
        {
            Log($"OnChangeState: {sync.StateToString(newState)} Setting to kinematic", "warn");

            _rb.isKinematic = true;
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

    // Update is currently only used to display debug info.
    public void Update()
    {
        if (_textMeshPro && _textMeshProTransform)
        {
            string displayText = "";

            if (_rb.velocity.magnitude > 0)
            {
                // displayText += $"velocity: {rb.velocity}\n";
            }

            //displayText += $"kinematic: {rb.isKinematic}";
            displayText += gameObject.name;
            _textMeshPro.text = displayText;

            if (Networking.LocalPlayer != null)
            {
                Vector3 headPos = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
                Vector3 direction = headPos - _textMeshProTransform.position;
                direction.y = 0;
                _textMeshProTransform.rotation = Quaternion.LookRotation(-direction);
                // Position text offset from camera, with minimum height
                Vector3 targetPos = transform.position - direction.normalized * 0.8f;
                targetPos.y = Mathf.Max(targetPos.y, transform.position.y + 0.4f);
                _textMeshProTransform.position = targetPos;
            }
        }
    }

    public override void OnChangeOwner(SmartObjectSync sync, VRCPlayerApi oldOwner, VRCPlayerApi newOwner) { }

}
