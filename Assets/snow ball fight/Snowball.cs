
using MMMaellon;

using System;
using TMPro;

using UdonSharp;

using UnityEngine;

using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;

public class Snowball : SmartObjectSyncListener
{

    [Header("RespawnAndEnable Settings")]
    public Vector3 respawnPosition;
    public float respawnDelay = 2f;

    [Header("Impact Effects")]
    public GameObject impactParticles;
    public float minImpactVelocity = 2f;

    [Header("Collider Setup")]
    public SphereCollider pickupCollider;
    public SphereCollider physicsCollider;

    [Header("Network sync")]
    [UdonSynced] private Vector3 _syncedImpactPosition;
    [UdonSynced] private Vector3 _syncedImpactNormal;
    [UdonSynced] private int _syncedImpactEventId;
    private int _lastHandledImpactEventId = 0;

    private bool _hasImpacted = false;
    public VRCPickup pickup;
    private Rigidbody _rb;
    private MeshRenderer _meshRenderer;
    private bool _hasInitialized = false;
    private TextMeshPro _textMeshPro;
    private Transform textMeshProTransform;
    private SmartObjectSync smartObjectSync;
    private void Start()
    {

        _textMeshPro = GetComponentInChildren<TextMeshPro>();
        _lastHandledImpactEventId = _syncedImpactEventId;
        Log($"Startup. LastHandledImpactEventId: {_lastHandledImpactEventId}");

        pickup = GetComponent<VRCPickup>();
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

        smartObjectSync = GetComponent<SmartObjectSync>();
        if (smartObjectSync)
        {
            // We need to listen to state change events from SmartObjectSync and switch kinematic mode
            // there to have position syncing working correctly for other clients that does not own
            // the object.
            smartObjectSync.AddListener(this);
            Log("Registered listener with SmartObjectSync");
        }
        else
        {
            Log($"No SmartObjectSync found on {gameObject.name}!", "err");
        }


        if (_textMeshPro)
        {
            textMeshProTransform = _textMeshPro.gameObject.transform; // Cache it

        }
    }

    public override void OnPickup()
    {
        Log($"OnPicking");

        _hasImpacted = false;

        // Once a snowball is picked up, we want it to be able to collide with stuff again.
        if (physicsCollider != null) physicsCollider.enabled = true;
    }

    public override void OnPlayerCollisionEnter(VRCPlayerApi player)
    {
        if (!TryRegisterImpact(transform.position, -_rb.velocity.normalized))
            return;

        DisableSnowball();
        CreateSnowballParticles();

        SendCustomEventDelayedSeconds(nameof(RespawnAndEnable), respawnDelay);
    }

    public override void OnDeserialization(DeserializationResult result)
    {
        Log("OnDeserialization");
        if (!_hasInitialized)
        {
            _hasInitialized = true;
            _lastHandledImpactEventId = _syncedImpactEventId;
            Log($"Initialized lastHandledImpactEventId to {_syncedImpactEventId}");
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
            Log($"OnDeserialization: Got network request to disable snowball. syncedImpactEventId: {_syncedImpactEventId} LastHandledImpactEventId: {_lastHandledImpactEventId} {gameObject.name}");
            DisableSnowball();
            CreateSnowballParticles();
            _lastHandledImpactEventId = _syncedImpactEventId;
        }

    }

    private bool TryRegisterImpact(Vector3 pos, Vector3 normal)
    {
        if (!Networking.IsOwner(gameObject)) return false;
        if (pickup && pickup.IsHeld) return false;
        if (_hasImpacted) return false;
        if (_rb && _rb.velocity.magnitude < minImpactVelocity) return false;

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

        if (!TryRegisterImpact(contact.point, contact.normal))
            return;

        DisableSnowball();
        CreateSnowballParticles();

        SendCustomEventDelayedSeconds(nameof(RespawnAndEnable), respawnDelay);
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

        if (pickup) pickup.pickupable = false;
    }

    public void EnableSnowball()
    {
        Log("Got call to to enable snowball.");
        if (_meshRenderer) _meshRenderer.enabled = true;
        if (_textMeshPro) _textMeshPro.text = $"{_meshRenderer.enabled}";
        if (pickupCollider) pickupCollider.enabled = true;
        if (pickup) pickup.pickupable = true;
    }
    public void RespawnAndEnable()
    {
        if (!Networking.IsOwner(gameObject)) return;

        if (smartObjectSync)
        {
            Log("TeleportToWorldSpace");
            smartObjectSync.TeleportToWorldSpace(respawnPosition, Quaternion.identity, Vector3.zero, Vector3.zero);
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
        if (_textMeshPro && textMeshProTransform)
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

    public override void OnChangeOwner(SmartObjectSync sync, VRCPlayerApi oldOwner, VRCPlayerApi newOwner) { }

}
